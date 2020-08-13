using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NLua;
using ReactiveUI;
using Splat;

namespace Primordially.PluginCore.Data
{
    public class DataSetLoader : IEnableLogger
    {
        private readonly string _rootPath;
        private readonly DataSetStrictness _strictness;

        public DataSetLoader(string rootPath, DataSetStrictness strictness = DataSetStrictness.Strict)
        {
            _rootPath = rootPath;
            _strictness = strictness;
        }

        public IDataSet LoadData(string path)
        {
            DataSetBuilder dataSet = PrepareEnvironment(out Lua _, out DataSetInteractions interactions);
            interactions.ImportFile(path);
            return dataSet.Build();
        }

        public IDataSet LoadString(string data)
        {
            DataSetBuilder dataSet = PrepareEnvironment(out Lua lua, out DataSetInteractions _);
            lua.DoString(data);
            return dataSet.Build();
        }

        private DataSetBuilder PrepareEnvironment(out Lua lua, out DataSetInteractions interactions)
        {
            lua = new Lua();
            DataSetBuilder dataSet = new DataSetBuilder(lua);
            interactions = new DataSetInteractions(dataSet, lua, this);
            lua.State.Encoding = Encoding.UTF8;
            lua.DoString("import = function() end");
            LuaRegistrationHelper.TaggedInstanceMethods(lua, interactions);
            return dataSet;
        }

        private class DataSetInteractions : IEnableLogger
        {
            private static readonly ImmutableHashSet<string> s_knownClassKeys = ImmutableHashSet.Create(
                "Name",
                "Fact",
                "SourcePage",
                "Condition",
                "Definitions",
                "Bonuses",
                "Types",
                "Roles",
                "HitDice",
                "MaxLevel",
                "ExClass",
                "Levels"
            );

            private readonly DataSetBuilder _dataSet;
            private readonly DataSetLoader _dataSetLoader;

            private readonly Stack<string> _file = new Stack<string>();
            private readonly Lua _lua;
            private DataSourceInformation? _sourceInfo;

            public DataSetInteractions(DataSetBuilder dataSet, Lua lua, DataSetLoader dataSetLoader)
            {
                _dataSet = dataSet;
                _lua = lua;
                _dataSetLoader = dataSetLoader;
            }

            [LuaGlobal]
            public void ImportFile(string path)
            {
                var basePath = _file.Count == 0 ? _dataSetLoader._rootPath : _file.Peek();
                var newPath = ResolvePath(Path.GetDirectoryName(basePath)!, path);
                if (!File.Exists(newPath))
                {
                    ReportError($"Could not find ImportFile target at {newPath}");
                    return;
                }

                _file.Push(newPath);
                _lua.DoFile(newPath);
                _sourceInfo = null;
                _file.Pop();
            }

            private string ResolvePath(string currentFile, string path)
            {
                if (path.StartsWith("@"))
                {
                    return Path.Combine(_dataSetLoader._rootPath, path.Substring(2));
                }

                return Path.Combine(currentFile, path);
            }

            [LuaGlobal]
            public void HideObjects(string key, LuaTable table)
            {
                // Not sure what this means... ignore it
            }

            [LuaGlobal]
            public void AddAvailableCompanions(string key, LuaTable a, LuaTable b)
            {
                // This is so horribly broken in PCGen that it's not useful to deal with
            }

            [LuaGlobal]
            public void DefineClass(LuaTable table)
            {
                string name = (string) table["Name"];
                if (_dataSet.Classes.ContainsKey(name))
                {
                    ReportError($"Ability with DefineClass with Name '{name}' is called more than once");
                    _dataSet.Classes.Remove(name);
                }

                foreach (var unknownKey in table.Keys.Cast<string>().Where(k => !s_knownClassKeys.Contains(k)))
                {
                    ReportError($"Unknown key {unknownKey} in DefineClass");
                }

                UnboundClass dataSetClass = ParseClassLevel(table);

                _dataSet.Classes.Add(name, dataSetClass);
            }

            [LuaGlobal]
            public void DefineAlignment(LuaTable table)
            {
                string key = (string) table["Key"];
                var alignment = new DataSetAlignment(
                    (string) table["Name"],
                    (string) table["Abbreviation"],
                    (string) table["SortKey"]
                );
                _dataSet.Alignments.Add(key, alignment);
            }

            [LuaGlobal]
            public void DefineDomain(LuaTable table)
            {
                string name = (string) table["Name"];
                _dataSet.Domains.Add(
                    name,
                    new UnboundDomain(
                        name,
                        (string) table["Description"],
                        ParseList(table["Definitions"], ParseVariableDefinition),
                        ParseList(table["SpellLists"], ParseSpellList),
                        ParseSingleCondition<CharacterInterface>(table["Conditions"]),
                        ParseList(table["ClassSkills"], Stringify),
                        (string) table["SourcePage"],
                        ParseAbilityGrants(table["Abilities"])
                    )
                );
            }

            [LuaGlobal]
            public void ModifyDomain(LuaTable table)
            {
                string name = (string) table["Name"];
                var existing = _dataSet.Domains[name];
                _dataSet.Domains[name] = 
                    existing.MergeWith(
                    new UnboundDomain(
                        name,
                        (string) table["Description"],
                        ParseList(table["Definitions"], ParseVariableDefinition),
                        ParseList(table["SpellLists"], ParseSpellList),
                        ParseSingleCondition<CharacterInterface>(table["Conditions"]),
                        ParseList(table["ClassSkills"], Stringify),
                        (string) table["SourcePage"],
                        ParseAbilityGrants(table["Abilities"])
                    )
                );
            }

            [LuaGlobal]
            public void DefineFact(LuaTable table)
            {
                var subKey = (string) table["Key"];
                string category = (string) table["Category"];
                var key = category + "|" + subKey;

                var fact = new DataSetFact(
                    category,
                    (bool?) table["Selectable"] ?? true,
                    (bool?) table["Visible"] ?? true,
                    (bool?) table["Required"] ?? true,
                    (string) table["DataFormat"],
                    (string?) table["DisplayName"],
                    (string?) table["Explanation"]
                );
                if (_dataSet.Facts.ContainsKey(key))
                {
                    ReportError($"Fact {key} is defined twice");
                    _dataSet.Facts.Remove(key);
                }

                _dataSet.Facts.Add(key, fact);
            }

            [LuaGlobal]
            public void DefineEquipment(LuaTable table)
            {
                string name = (string) table["Name"];
                _dataSet.Equipment.Add(
                    name,
                    new UnboundEquipment(
                        name,
                        ParseCost(table["Cost"]) ?? 0,
                        ParseInt(table["BaseQuantity"]),
                        ParseInt(table["EffectiveDamageResistance"]),
                        ParseContainer(table["Contains"]),
                        ((bool?) table["CanHaveMods"]) ?? true,
                        ((bool?) table["ModsRequired"]) ?? false,
                        ParseInt(table["SpellBookPageCount"]),
                        (DataSetFormula?) table["PagesPerSpell"],
                        (string?) table["Size"],
                        ParseInt(table["UsedSlots"]),
                        ParseDouble(table["Weight"]),
                        ParseInt(table["ArmorCheckPenalty"]),
                        ParseAttackDefinition(table["SecondAttack"]),
                        ParseAttackDefinition(table["Attack"]),
                        (string?) table["FumbleRange"],
                        ParseInt(table["MaxDex"]),
                        (string?) table["Proficiency"],
                        ParseInt(table["Range"]),
                        ParseInt(table["Reach"]),
                        ParseInt(table["ReachMultiplier"]),
                        ParseInt(table["ArcaneSpellFailureChance"]),
                        (string?) table["WieldCategory"],
                        (bool?) table["Visible"],
                        ParseDict(table["Qualities"], Stringify),
                        ParseList(table["Bonuses"], ParseBonus),
                        ParseList(table["Types"], Stringify),
                        ParseList(table["SpecialProperties"], ParseFormattable),
                        ParseFormattable(table["Description"]),
                        ParseList(table["EquipmentModifiers"], ParseAddModifier),
                        (string?)table["BaseItem"]
                    )
                );
            }

            [LuaGlobal]
            public DataSetDiceFormula DiceFormula(string desc)
            {
                return new DataSetDiceFormula(desc);
            }

            private int? ParseCost(object value) => (int?) (ParseDouble(value) * 100);

            private double? ParseDouble(object value)
            {
                switch (value)
                {
                    case null:
                        return null;
                    case double d:
                        return d;
                    case long l:
                        return l;
                    default:
                        ReportError($"{value} is not a valid number");
                        return null;
                }
            }

            private int? ParseInt(object value)
            {
                #nullable disable
                return (int?) (long?) value;
                #nullable restore
            }

            private UnboundEquipmentAddModifier ParseAddModifier(object arg)
            {
                LuaTable table = (LuaTable) arg;
                return new UnboundEquipmentAddModifier(
                    ParseList(table["Parameters"], o => (DataSetFormula) o),
                    (string) table["Key"]
                );
            }

            private DataSetAttackDefinition? ParseAttackDefinition(object obj)
            {
                return null;
            }

            private DataSetEquipmentContainer? ParseContainer(object obj)
            {
                return null;
            }

            [LuaGlobal]
            public void CopyEquipment(string baseItemName, LuaTable table)
            {
            }

            [LuaGlobal]
            public void DefineSave(LuaTable table)
            {
                var name = (string) table["Name"];
                _dataSet.Saves.Add(
                    name,
                    new DataSetSave(
                        name,
                        (string) table["SortKey"] ?? name,
                        ParseBonus(table["Bonus"])
                    )
                );
            }

            [LuaGlobal]
            public void DefineStat(LuaTable table)
            {
                var key = (string) table["Key"];
                _dataSet.AbilityScores.Add(
                    key,
                    new UnboundAbilityScore(
                        (string) table["Name"],
                        (string) table["SortKey"],
                        (string) table["Abbreviation"],
                        ParseFormula(table["StatModFormula"]),
                        ParseList(
                            (LuaTable) table["Modifications"],
                            o =>
                            {
                                var t = (LuaTable) o;
                                return new DataSetModDefinition(
                                    (string) t["Target"],
                                    (string) t["Action"],
                                    ParseFormula(t["Value"])
                                );
                            }
                        ),
                        ParseList(table["Definitions"], ParseVariableDefinition),
                        ParseList(table["Bonuses"], ParseBonus),
                        ParseAbilityGrants(table["Abilities"])
                    )
                );
            }

            [LuaGlobal]
            public void DefineVariable(LuaTable table)
            {
                _dataSet.Variables.Add(
                    (string) table["Name"],
                    new DataSetVariable(
                        (string) table["Name"],
                        (string) table["Type"],
                        (string?) table["Channel"],
                        (string?) table["Scope"]
                    )
                );
            }

            [LuaGlobal]
            public void DefineAbilityCategory(LuaTable table)
            {
                string name = (string) table["Name"];

                if (_dataSet.AbilityCategories.ContainsKey(name))
                {
                    ReportError($"Ability Category {name} is defined twice");
                    _dataSet.AbilityCategories.Remove(name);
                }

                _dataSet.AbilityCategories.Add(
                    name,
                    new DataSetAbilityCategory(
                        name,
                        (string) table["Category"],
                        (string) (table["Plural"] ?? table["Name"]),
                        (string?) table["DisplayLocation"],
                        (bool?) table["Visible"] ?? false,
                        (bool?) table["Editable"] ?? false,
                        (bool?) table["EditPool"] ?? false,
                        (bool?) table["FractionalPool"] ?? false,
                        (string?) table["Pool"],
                        (string?) table["AbilityList"],
                        ParseList(table["Types"], Stringify)
                    )
                );
            }

            [LuaGlobal]
            public IChooser<SkillInterface> ChooseSkill(object funcOrListOfFunc)
            {
                return ParseChooser<SkillInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public object? ChooseAbilitySelection(object funcOrListOfFunc)
            {
                return null;
            }

            [LuaGlobal]
            public UserInputChooser ChooseUserInput(int count, string prompt)
            {
                return new UserInputChooser(count, prompt);
            }

            [LuaGlobal]
            public IChooser<SpellInterface> ChooseSpell(object funcOrListOfFunc)
            {
                return ParseChooser<SpellInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public IChooser<LanguageInterface> ChooseLanguage(object funcOrListOfFunc)
            {
                return ParseChooser<LanguageInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public IChooser<ClassInterface> ChooseClass(object funcOrListOfFunc)
            {
                return ParseChooser<ClassInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public IChooser<SchoolInterface> ChooseSchool(object funcOrListOfFunc)
            {
                return ParseChooser<SchoolInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public IChooser<string> ChooseString(LuaTable list)
            {
                return new StringChooser(ParseList(list, Stringify));
            }

            [LuaGlobal]
            public IChooser<WeaponProficiencyInterface> ChooseWeaponProficiency(object funcOrListOfFunc)
            {
                return ParseChooser<WeaponProficiencyInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public object? ChooseNothing() => null;

            [LuaGlobal]
            public DataSetFormula Formula(string text)
            {
                return ParseFormula(text);
            }

            private void ReportError(string message)
            {
                switch (_dataSetLoader._strictness)
                {
                    case DataSetStrictness.Strict:
                        _lua.State.Error(message);
                        // Unreachable, "Error" throws
                        break;
                    case DataSetStrictness.Lax:
                        _lua.State.Warning(message, true);
                        var trc = _lua.GetDebugTraceback();
                        this.Log().Error(message, "LUA exception at: {0}", trc);
                        return;
                }
            }

            private DataSetSpellList? ParseSpellList(object arg)
            {
                if (arg == null)
                    return null;
                LuaTable table = (LuaTable) arg;
                return new DataSetSpellList(
                    (string) table["Kind"],
                    (string) table["Name"],
                    ParseList(table["Levels"], ParseSpellListLevel)
                );
            }

            private DataSetSpellListLevel ParseSpellListLevel(object arg)
            {
                LuaTable table = (LuaTable) arg;
                return new DataSetSpellListLevel(
                    (int) (long) table["SpellLevel"],
                    ParseList(table["Spells"], Stringify)
                );
            }

            private UnboundClass ParseClassLevel(LuaTable table)
            {
                var levels = ParseList(
                    (LuaTable) table["Levels"],
                    obj =>
                    {
                        LuaTable t = (LuaTable) obj;
                        (int level, int repeat) = ParseLevelString(t["Level"]);
                        return new UnboundRepeatingClassLevel(
                            level,
                            repeat,
                            new UnboundClassLevel(
                                ParseList(
                                    (LuaTable) t["AddedSpellCasterLevels"],
                                    l =>
                                    {
                                        LuaTable lt = (LuaTable) l;
                                        if ((bool?) lt["Any"] == true)
                                        {
                                            return new DataSetAddedCasterLevel(null);
                                        }

                                        return new DataSetAddedCasterLevel((string) lt["Type"]);
                                    }
                                ),
                                ParseAbilityGrants(t["Abilities"])
                            )
                        );
                    }
                );

                var dataSetClass = new UnboundClass(
                    (string) table["Name"],
                    _sourceInfo,
                    ParseDict(table["Fact"], Stringify),
                    (string?) table["SourcePage"],
                    ParseSingleCondition<CharacterInterface>(table["Condition"], "DefineClass"),
                    ParseList((LuaTable?) table["Definitions"], ParseVariableDefinition),
                    ParseList((LuaTable?) table["Bonuses"], ParseBonus),
                    ParseList((LuaTable?) table["Types"], Stringify),
                    ParseList((LuaTable?) table["Roles"], Stringify),
                    ParseInt(table["HitDice"]),
                    ParseInt(table["MaxLevel"]),
                    levels,
                    (string?) table["ExClass"]
                );
                return dataSetClass;
            }

            private static (int level, int repeat) ParseLevelString(object obj)
            {
                var levelString = (string) obj;
                Match repeatMatch = Regex.Match(levelString, @"^Start=(\d+),Repeat=(\d+)$");
                if (repeatMatch.Success)
                {
                    return (int.Parse(repeatMatch.Groups[1].Value), int.Parse(repeatMatch.Groups[2].Value));
                }

                return (int.Parse(levelString), 0);
            }

            private static DataSetVariableDefinition ParseVariableDefinition(object obj)
            {
                LuaTable t = (LuaTable) obj;
                return new DataSetVariableDefinition(
                    (string) t["Name"],
                    (DataSetFormula) t["InitialValue"]
                );
            }

            private static ImmutableList<UnboundAddAbility> ParseAbilityGrants(object obj)
            {
                var abilities = (LuaTable) obj;
                ImmutableList<IntermediateGrantAbility> addedAbilities = abilities == null
                    ? ImmutableList<IntermediateGrantAbility>.Empty
                    : ParseList(
                        abilities,
                        a =>
                        {
                            LuaTable at = (LuaTable) a;
                            return new IntermediateGrantAbility(
                                (string) at["Category"],
                                (string) at["Nature"],
                                ParseList(at["Names"], Stringify)
                            );
                        }
                    );
                var expandedAbilities = addedAbilities.SelectMany(
                        ab => ab.Names.Select(
                            n => new UnboundAddAbility(
                                ab.Category,
                                ab.Nature,
                                n
                            )
                        )
                    )
                    .ToImmutableList();
                return expandedAbilities;
            }

            private static DataSetBonus ParseBonus(object obj)
            {
                var table = (LuaTable) obj;
                return new DataSetBonus(
                    (string) table["Category"],
                    ParseList(table["Variables"], Stringify),
                    (DataSetFormula) table["Formula"],
                    ParseSingleCondition<CharacterInterface>(table["Conditions"])
                );
            }

            private static DataSetFormula ParseFormula(object obj)
            {
                switch (obj)
                {
                    case long l:
                        return new DataSetFormula((int) l);
                    case string s when int.TryParse(s, out int i):
                        return new DataSetFormula(i);
                    case string s:
                        return new DataSetFormula(-666);
                    default:
                        throw new ArgumentException("Unparsable formula value");
                }
            }

            private static DataSetCondition<T> ParseSingleCondition<T>(
                object condition,
                [CallerMemberName] string? name = null)
            {
                if (condition == null)
                {
                    return DataSetConditions<T>.Empty;
                }

                if (condition is LuaFunction func)
                {
                    return c => SingleCall<bool>(func, c);
                }

                if (condition is LuaTable table)
                {
                    return c => table.Values.Cast<LuaFunction>().All(f => SingleCall<bool>(f, c));
                }

                throw new ArgumentException($"{name} is incorrectly defined");
            }

            private static IChooser<T> ParseChooser<T>(object choose, [CallerMemberName] string? name = null)
            {
                ChooserFilter<T> filter;
                if (choose is LuaFunction func)
                {
                    filter = (c, s) => SingleCall<bool>(func, c, s);
                }
                else if (choose is LuaTable table)
                {
                    filter = (c, s) => table.Values.Cast<LuaFunction>().All(f => SingleCall<bool>(f, c, s));
                }
                else
                {
                    throw new ArgumentException($"{name} is incorrectly defined");
                }

                return new Chooser<T>(filter);
            }

            private static DataSourceInformation ParseSource(LuaTable table)
            {
                return new DataSourceInformation(
                    (string) table["SourceLong"],
                    (string) table["SourceShort"],
                    (string) table["SourceWeb"],
                    DateTimeOffset.Parse((string) table["SourceDate"])
                );
            }

            private static DataSetLink ParseLink(object arg)
            {
                LuaTable table = (LuaTable) arg;
                return new DataSetLink((string) table["Name"], (string) table["Url"], (string) table["Text"]);
            }

            private static DataSetPublisherInformation ParsePublisherInformation(LuaTable info)
            {
                return new DataSetPublisherInformation(
                    (string) info["NameShort"],
                    (string) info["NameLong"],
                    (string) info["Url"]
                );
            }

            private static ImmutableDictionary<string, T> ParseDict<T>(object? obj, Func<object, T> valueSelector)
            {
                if (obj == null)
                {
                    return ImmutableDictionary<string, T>.Empty;
                }

                var b = ImmutableDictionary.CreateBuilder<string, T>();
#nullable disable
                foreach (KeyValuePair<object, object> factPair in (LuaTable) obj)
#nullable restore
                {
                    b.Add((string) factPair.Key, valueSelector(factPair.Value));
                }

                return b.ToImmutable();
            }

            private static ImmutableList<T> ParseList<T>(object? obj, Func<object, T> selector)
            {
                if (obj == null)
                {
                    return ImmutableList<T>.Empty;
                }

                LuaTable table = (LuaTable) obj;

                int limit = table.Values.Count;
                var builder = ImmutableList.CreateBuilder<T>();
                for (int i = 1; i <= limit; i++)
                {
                    builder.Add(selector(table[i]));
                }

                return builder.ToImmutable();
            }

            private static ImmutableList<T> MergeList<T>(
                ImmutableList<T> initial,
                LuaTable table,
                Func<object, T> selector)
            {
                ImmutableList<T>.Builder b = initial.ToBuilder();
                b.AddRange(ParseList(table, selector));
                return b.ToImmutable();
            }

            private static T SingleCall<T>(LuaFunction func, params object?[] args)
            {
                var result = func.Call(args);
                if (result.Length != 1)
                {
                    throw new ArgumentException("Lua function returned multiple (or no) arguments");
                }

                return (T) result[0];
            }

            public class IntermediateGrantAbility
            {
                public IntermediateGrantAbility(string category, string nature, ImmutableList<string> names)
                {
                    Category = category;
                    Nature = nature;
                    Names = names;
                }

                public string Category { get; }
                public string Nature { get; }
                public ImmutableList<string> Names { get; }
            }

            #region data set basic info

            [LuaGlobal]
            public void SetSource(LuaTable table)
            {
                _sourceInfo = ParseSource(table);
            }

            [LuaGlobal]
            public void SetDataSetInfo(LuaTable table)
            {
                ImmutableList<LuaFunction> conditionList = ParseList(
                    (LuaTable) table["Conditions"],
                    arg => (LuaFunction) arg
                );

                _dataSet.DataSetInformation = new DataSetInformation(
                    (string) table["Name"],
                    ParseSource((LuaTable) table["SourceInfo"]),
                    (string) table["GameMode"],
                    ParseList(table["BookTypes"], x => (string) x),
                    ParseList(table["Types"], x => (string) x),
                    (string) table["Status"],
                    (string) table["Copyright"],
                    (string) table["Description"],
                    (string) table["Genre"],
                    (string) table["InfoText"],
                    (string) table["HelpUrl"],
                    (bool) table["IsMature"],
                    (bool) table["IsOGL"],
                    (bool) table["IsLicensed"],
                    ds => conditionList.All(c => SingleCall<bool>(c, ds)),
                    ParsePublisherInformation((LuaTable) table["PublisherInfo"]),
                    (int) (long) table["Rank"],
                    (bool) table["ShowInMenu"],
                    (string) table["Setting"],
                    ParseList(table["Links"], ParseLink)
                );
            }

            #endregion

            #region Abilities

            [LuaGlobal]
            public void DefineAbility(LuaTable table)
            {
                string? key = (string) table["Key"];
                string name = (string) table["Name"];
                if (key != null &&
                    _dataSet.GetAbility(key) != null ||
                    _dataSet.GetAbility(name) != null)
                {
                    ReportError($"Ability with DefineAbility with key '{key}' is called more than once");
                    _dataSet.ClearAbility(name, key);
                }

                _dataSet.AddAbility(ParseAbility(table), name, key);
            }

            [LuaGlobal]
            public void ModifyAbility(LuaTable table)
            {
                string name = (string) table["Name"];
                string? key = (string) table["Key"];
                UnboundAbility ability;
                if (key != null)
                {
                    UnboundAbility? foundAbility = _dataSet.GetAbility(key);
                    if (foundAbility == null)
                    {
                        ReportError($"No ability with Key '{key}' found");
                        DefineAbility(table);
                        foundAbility = UnboundAbility.Empty;
                    }
                    
                    ability = foundAbility ?? UnboundAbility.Empty;
                }
                else
                {
                    UnboundAbility? foundAbility = _dataSet.GetAbility(name);
                    if (foundAbility == null)
                    {
                        ReportError($"No ability with Name '{table["Name"]}' found");
                        DefineAbility(table);
                    }

                    ability = foundAbility ?? UnboundAbility.Empty;
                }

                UnboundAbility newAbility = ParseAbility(table);

                _dataSet.ClearAbility(name, key);
                _dataSet.AddAbility(ability.MergedWith(newAbility), name, key);
            }

            private static readonly ImmutableHashSet<string> s_knownAbilityKeys = ImmutableHashSet.Create(
                "Name",
                "Key",
                "Category",
                "Description",
                "Stackable",
                "AllowMultiple",
                "Visible",
                "Types",
                "Definitions",
                "Bonuses",
                "Abilities",
                "Aspects",
                "Cost",
                "SourcePage"
            );

            private UnboundAbility ParseAbility(LuaTable table)
            {
                foreach (var unknownKey in table.Keys.Cast<string>().Where(k => !s_knownAbilityKeys.Contains(k)))
                {
                    ReportError($"Unknown key {unknownKey} in DefineAbility/ModifyAbility");
                }

                return new UnboundAbility(
                    (string) table["Name"],
                    (string?) table["Key"],
                    _sourceInfo,
                    ParseList((LuaTable?) table["Bonuses"], ParseBonus),
                    (bool?) table["Stackable"],
                    (string?) table["Category"],
                    (bool?) table["AllowMultiple"],
                    (bool?) table["Visible"],
                    ParseList((LuaTable?) table["Definitions"], ParseVariableDefinition),
                    ParseList((LuaTable?) table["Aspects"], ParseAspect),
                    ParseList((LuaTable?) table["Types"], Stringify),
                    ParseInt(table["Cost"]),
                    ParseFormattable(table["Description"]),
                    (string?) table["SourcePage"],
                    ParseChoice(table["Choice"], ParseInt(table["Selection"])),
                    ParseAbilityGrants(table["Abilities"])
                );
            }

            private Choice? ParseChoice(object obj, int? selection)
            {
                if (obj == null)
                    return null;
                LuaTable table = (LuaTable) obj;
                IChooser chooser = (IChooser) table["Choose"];
                return Choice.Build(chooser, selection, ParseInt(table["MaxTimes"]));
            }

            private DataSetAspect ParseAspect(object o)
            {
                var t = (LuaTable) o;
                return new DataSetAspect(
                    (string) t["Name"],
                    (string) t["FormatString"],
                    ParseList((LuaTable?) t["ArgumentList"], o => (DataSetFormula) o)
                );
            }

            private DataSetFormattable ParseFormattable(object value)
            {
                return value switch
                {
                    DataSetFormattable f => f,
                    null => new DataSetFormattable("0", ImmutableList<DataSetFormula>.Empty),
                    string str => new DataSetFormattable(str, ImmutableList<DataSetFormula>.Empty),
                    LuaTable table => new DataSetFormattable(
                        (string) table["FormatString"],
                        ParseList((LuaTable?) table["Arguments"], o => (DataSetFormula) o)
                    ),
                    _ => throw new ArgumentException("Formattable object is not correctly defined")
                };
            }

            private static string Stringify(object o)
            {
                return (string) o;
            }

            #endregion
        }

        private sealed class DataSetBuilder
        {
            private readonly ImmutableDictionary<string, UnboundAbility>.Builder _keyedAbilities =
                ImmutableDictionary.CreateBuilder<string, UnboundAbility>(StringComparer.OrdinalIgnoreCase);

            private readonly Lua _luaContext;

            private readonly ImmutableDictionary<string, UnboundAbility>.Builder _namedAbilities =
                ImmutableDictionary.CreateBuilder<string, UnboundAbility>(StringComparer.OrdinalIgnoreCase);

            internal DataSetBuilder(Lua luaContext)
            {
                _luaContext = luaContext;
            }

            public DataSetInformation? DataSetInformation { get; set; }

            public Dictionary<string, UnboundClass> Classes { get; } =
                new Dictionary<string, UnboundClass>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetAlignment>.Builder Alignments { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetAlignment>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetFact>.Builder Facts { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetFact>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetSave>.Builder Saves { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetSave>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, UnboundAbilityScore> AbilityScores { get; } =
                new Dictionary<string, UnboundAbilityScore>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetVariable>.Builder Variables { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetVariable>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetAbilityCategory>.Builder AbilityCategories { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetAbilityCategory>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, UnboundDomain> Domains { get; } =
                new Dictionary<string, UnboundDomain>(StringComparer.OrdinalIgnoreCase);
            
            public Dictionary<string, UnboundEquipment> Equipment { get; } =
                new Dictionary<string, UnboundEquipment>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, UnboundEquipmentModifier> EquipmentModifiers { get; } =
                new Dictionary<string, UnboundEquipmentModifier>(StringComparer.OrdinalIgnoreCase);

            public UnboundAbility GetAbility(string nameOrKey)
            {
                return _keyedAbilities.GetValueOrDefault(nameOrKey) ??
                    _namedAbilities.GetValueOrDefault(nameOrKey);
            }

            public void AddAbility(UnboundAbility ability, string name, string? key = null)
            {
                _namedAbilities.Add(name, ability);
                if (key != null)
                {
                    _keyedAbilities.Add(key, ability);
                }
            }

            public void ClearAbility(string name, string? key)
            {
                _namedAbilities.Remove(name);
                if (key != null)
                {
                    _keyedAbilities.Remove(key);
                }
            }

            public DataSet Build()
            {
                Binder binder = new Binder(this);
                return binder.Build();
            }

            private class Binder : IBinder
            {
                private readonly DataSetBuilder _builder;
                private readonly ImmutableDictionary<string, DataSetAbility>.Builder _namedAbilities = ImmutableDictionary.CreateBuilder<string, DataSetAbility>();
                private readonly ImmutableDictionary<string, DataSetAbility>.Builder _keyedAbilities = ImmutableDictionary.CreateBuilder<string, DataSetAbility>();

                public Binder(DataSetBuilder builder)
                {
                    _builder = builder;
                    _classes = ImmutableDictionary.CreateBuilder<string, DataSetClass>(_builder.Classes.Comparer);
                    _abilityScores = ImmutableDictionary.CreateBuilder<string, DataSetAbilityScore>(_builder.Classes.Comparer);
                    _domains = ImmutableDictionary.CreateBuilder<string, DataSetDomain>(_builder.Classes.Comparer);
                    _equipmentModifiers = ImmutableDictionary.CreateBuilder<string, DataSetEquipmentModifier>(_builder.Classes.Comparer);
                    _equipment = ImmutableDictionary.CreateBuilder<string, DataSetEquipment>(_builder.Classes.Comparer);

                    _binders = new Dictionary<Type, Func<string, object>>
                    {
                        {typeof(DataSetAbility), BindAbility},
                        {typeof(DataSetClass), s => Bind<DataSetClass>(s)},
                        {typeof(DataSetAbilityScore), s => Bind<DataSetAbilityScore>(s)},
                        {typeof(DataSetAbilityScore), s => Bind<DataSetDomain>(s)},
                        {typeof(DataSetEquipment), s => Bind<DataSetEquipment>(s)},
                        {typeof(DataSetEquipmentModifier), s => Bind<DataSetEquipmentModifier>(s)},
                    };
                }

                private readonly Dictionary<Type, Func<string, object>> _binders;
                private readonly ImmutableDictionary<string, DataSetClass>.Builder _classes;
                private readonly ImmutableDictionary<string, DataSetAbilityScore>.Builder _abilityScores;
                private readonly ImmutableDictionary<string, DataSetDomain>.Builder _domains;
                private readonly ImmutableDictionary<string, DataSetEquipmentModifier>.Builder _equipmentModifiers;
                private readonly ImmutableDictionary<string, DataSetEquipment>.Builder _equipment;

                [return: NotNullIfNotNull("key")]
                public TBound? Bind<TBound>(string? key) where TBound : class
                {
                    if (key == null)
                        return null;
                    return Unsafe.As<TBound>(_binders[typeof(TBound)](key));
                }

                private DataSetAbility BindAbility(string key)
                {
                    UnboundAbility u = _builder.GetAbility(key);
                    if (u == null)
                        throw new KeyNotFoundException();

                    _builder._namedAbilities.Remove(key);
                    _builder._keyedAbilities.Remove(key);
                    var b = u.Bind(this);
                    _namedAbilities.Add(b.Name, b);
                    if (b.Key != null)
                    {
                        _namedAbilities.Add(b.Key, b);
                    }

                    return b;
                }

                private ImmutableDictionary<string, TBound> BindAll<TUnbound, TBound>(
                    Dictionary<string, TUnbound> unbound,
                    ImmutableDictionary<string, TBound>.Builder bound) where TUnbound : IRequiresBinding<TBound>
                {
                    while (unbound.Count != 0)
                    {
                        string key = unbound.Keys.First();
                        Bind(key, unbound, bound);
                    }
                    return bound.ToImmutable();
                }

                private TBound Bind<TUnbound, TBound>(string key, Dictionary<string, TUnbound> unbound, ImmutableDictionary<string, TBound>.Builder bound) where TUnbound : IRequiresBinding<TBound>
                {
                    TUnbound u = unbound[key];
                    unbound.Remove(key);
                    TBound b = u.Bind(this);
                    bound.Add(key, b);
                    return b;
                }

                public DataSet Build()
                {
                    while (_builder._keyedAbilities.Count != 0)
                    {
                        BindAbility(_builder._keyedAbilities.Keys.First());
                    }
                    while (_builder._namedAbilities.Count != 0)
                    {
                        BindAbility(_builder._namedAbilities.Keys.First());
                    }

                    return new DataSet(
                        _builder._luaContext,
                        _builder.DataSetInformation,
                        new AbilityDictionary(_keyedAbilities.ToImmutable(), _namedAbilities.ToImmutable()),
                        BindAll(_builder.Classes, _classes),
                        _builder.Alignments.ToImmutable(),
                        _builder.Facts.ToImmutable(),
                        _builder.Saves.ToImmutable(),
                        BindAll(_builder.AbilityScores, _abilityScores),
                        _builder.Variables.ToImmutable(),
                        _builder.AbilityCategories.ToImmutable(),
                        BindAll(_builder.Domains, _domains),
                        BindAll(_builder.EquipmentModifiers, _equipmentModifiers),
                        BindAll(_builder.Equipment, _equipment)
                    );
                }
            }
        }

        private sealed class DataSet : IDataSet
        {
            private readonly Lua _luaContext;

            public DataSet(
                Lua luaContext,
                DataSetInformation? dataSetInformation,
                IImmutableDictionary<string, DataSetAbility> abilities,
                ImmutableDictionary<string, DataSetClass> classes,
                ImmutableDictionary<string, DataSetAlignment> alignments,
                ImmutableDictionary<string, DataSetFact> facts,
                ImmutableDictionary<string, DataSetSave> saves,
                ImmutableDictionary<string, DataSetAbilityScore> abilityScores,
                ImmutableDictionary<string, DataSetVariable> variables,
                ImmutableDictionary<string, DataSetAbilityCategory> abilityCategories,
                ImmutableDictionary<string, DataSetDomain> domains,
                ImmutableDictionary<string, DataSetEquipmentModifier> equipmentModifiers,
                ImmutableDictionary<string, DataSetEquipment> equipment)
            {
                _luaContext = luaContext;
                DataSetInformation = dataSetInformation;
                Abilities = abilities;
                Classes = classes;
                Alignments = alignments;
                Facts = facts;
                Saves = saves;
                AbilityScores = abilityScores;
                Variables = variables;
                AbilityCategories = abilityCategories;
                Domains = domains;
                EquipmentModifiers = equipmentModifiers;
                Equipment = equipment;
            }

            public ImmutableDictionary<string, DataSetClass> Classes { get; }
            public ImmutableDictionary<string, DataSetAlignment> Alignments { get; }
            public ImmutableDictionary<string, DataSetFact> Facts { get; }
            public ImmutableDictionary<string, DataSetSave> Saves { get; }
            public ImmutableDictionary<string, DataSetAbilityScore> AbilityScores { get; }
            public ImmutableDictionary<string, DataSetVariable> Variables { get; }
            public ImmutableDictionary<string, DataSetAbilityCategory> AbilityCategories { get; }
            public ImmutableDictionary<string, DataSetDomain> Domains { get; }
            public ImmutableDictionary<string, DataSetEquipmentModifier> EquipmentModifiers { get; }
            public ImmutableDictionary<string, DataSetEquipment> Equipment { get; }

            public DataSetInformation? DataSetInformation { get; }
            public IImmutableDictionary<string, DataSetAbility> Abilities { get; }

            public void Dispose()
            {
                _luaContext.Dispose();
            }
        }

        private sealed class AbilityDictionary : IImmutableDictionary<string, DataSetAbility>
        {
            private readonly ImmutableDictionary<string, DataSetAbility> _keyedAbilities;
            private readonly ImmutableDictionary<string, DataSetAbility> _namedAbilities;

            public AbilityDictionary(
                ImmutableDictionary<string, DataSetAbility> keyedAbilities,
                ImmutableDictionary<string, DataSetAbility> namedAbilities)
            {
                _keyedAbilities = keyedAbilities;
                _namedAbilities = namedAbilities;
            }

            public IEnumerator<KeyValuePair<string, DataSetAbility>> GetEnumerator()
            {
                return _namedAbilities.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int Count => _namedAbilities.Count;

            public bool ContainsKey(string key)
            {
                return _keyedAbilities.ContainsKey(key) || _namedAbilities.ContainsKey(key);
            }

            public bool TryGetValue(string key, out DataSetAbility value)
            {
#nullable disable
                return _keyedAbilities.TryGetValue(key, out value) || _namedAbilities.TryGetValue(key, out value);
#nullable restore
            }

            public DataSetAbility this[string key] => _keyedAbilities.TryGetValue(key, out var value)
                ? value
                : _namedAbilities[key];

            public IEnumerable<string> Keys => _namedAbilities.Keys;
            public IEnumerable<DataSetAbility> Values => _namedAbilities.Values;

            public IImmutableDictionary<string, DataSetAbility> Add(string key, DataSetAbility value)
            {
                throw new NotSupportedException();
            }

            public IImmutableDictionary<string, DataSetAbility> AddRange(
                IEnumerable<KeyValuePair<string, DataSetAbility>> pairs)
            {
                throw new NotSupportedException();
            }

            public IImmutableDictionary<string, DataSetAbility> Clear()
            {
                return new AbilityDictionary(_keyedAbilities.Clear(), _namedAbilities.Clear());
            }

            public bool Contains(KeyValuePair<string, DataSetAbility> pair)
            {
                return _keyedAbilities.Contains(pair) || _namedAbilities.Contains(pair);
            }

            public IImmutableDictionary<string, DataSetAbility> Remove(string key)
            {
                return new AbilityDictionary(_keyedAbilities.Remove(key), _namedAbilities.Remove(key));
            }

            public IImmutableDictionary<string, DataSetAbility> RemoveRange(IEnumerable<string> keys)
            {
                return new AbilityDictionary(_keyedAbilities.RemoveRange(keys), _namedAbilities.RemoveRange(keys));
            }

            public IImmutableDictionary<string, DataSetAbility> SetItem(string key, DataSetAbility value)
            {
                throw new NotSupportedException();
            }

            public IImmutableDictionary<string, DataSetAbility> SetItems(
                IEnumerable<KeyValuePair<string, DataSetAbility>> items)
            {
                throw new NotSupportedException();
            }

            public bool TryGetKey(string equalKey, out string actualKey)
            {
                return _keyedAbilities.TryGetKey(equalKey, out actualKey) ||
                    _namedAbilities.TryGetKey(equalKey, out actualKey);
            }
        }

        private class UnboundAddAbility : DataSetAddAbilityBase, IRequiresBinding<DataSetAddAbility>
        {
            private readonly string _name;

            public UnboundAddAbility(string category, string nature, string name) : base(category, nature)
            {
                _name = name;
            }

            public DataSetAddAbility Bind(IBinder binder)
            {
                return new DataSetAddAbility(Category, Nature, binder.Bind<DataSetAbility>(_name));
            }
        }

        private class UnboundAbility : DataSetAbilityBase, IRequiresBinding<DataSetAbility>
        {
            public static UnboundAbility Empty { get; } = new UnboundAbility(
                "",
                null,
                null,
                ImmutableList<DataSetBonus>.Empty,
                null,
                null,
                null,
                null,
                ImmutableList<DataSetVariableDefinition>.Empty,
                ImmutableList<DataSetAspect>.Empty,
                ImmutableList<string>.Empty,
                null,
                null,
                null,
                null,
                ImmutableList<UnboundAddAbility>.Empty
            );

            private readonly IImmutableList<UnboundAddAbility> _abilities;

            public UnboundAbility(
                string name,
                string? key,
                DataSourceInformation? sourceInfo,
                ImmutableList<DataSetBonus> bonuses,
                bool? stackable,
                string? category,
                bool? allowMultiple,
                bool? visible,
                ImmutableList<DataSetVariableDefinition> definitions,
                ImmutableList<DataSetAspect> aspects,
                ImmutableList<string> types,
                int? cost,
                DataSetFormattable? description,
                string? sourcePage,
                Choice? choice,
                IImmutableList<UnboundAddAbility> abilities)
                : base(
                    name,
                    key,
                    sourceInfo,
                    bonuses,
                    stackable,
                    category,
                    allowMultiple,
                    visible,
                    definitions,
                    aspects,
                    types,
                    cost,
                    description,
                    sourcePage,
                    choice
                )
            {
                _abilities = abilities;
            }

            public DataSetAbility Bind(IBinder lookup)
            {
                return new DataSetAbility(
                    Name,
                    Key,
                    SourceInfo,
                    Bonuses,
                    Stackable,
                    Category,
                    AllowMultiple,
                    Visible,
                    Definitions,
                    Aspects,
                    Types,
                    Cost,
                    Description,
                    SourcePage,
                    Choice,
                    _abilities.Select(a => a.Bind(lookup)).ToImmutableList()
                );
            }

            public UnboundAbility MergedWith(UnboundAbility other)
            {
                return new UnboundAbility(
                    other.Name ?? Name,
                    other.Key ?? Key,
                    SourceInfo ?? other.SourceInfo,
                    Bonuses.AddRange(other.Bonuses),
                    other.StackableSet ?? StackableSet,
                    other.Category ?? Category,
                    other.AllowMultipleSet ?? AllowMultipleSet,
                    other.VisibleSet ?? VisibleSet,
                    Definitions.AddRange(other.Definitions),
                    Aspects.AddRange(other.Aspects),
                    Types.AddRange(other.Types),
                    other.CostSet ?? CostSet,
                    other.Description ?? Description,
                    other.SourcePage ?? SourcePage,
                    Choice,
                    _abilities.AddRange(other._abilities)
                );
            }
        }

        private class UnboundEquipment : DataSetEquipmentBase, IRequiresBinding<DataSetEquipment>
        {
            private readonly ImmutableList<UnboundEquipmentAddModifier> _equipmentModifiers;
            private readonly string? _baseItemName;

            public UnboundEquipment(
                string name,
                int cost,
                int? baseQuantity,
                int? effectiveDamageResistance,
                DataSetEquipmentContainer? contains,
                bool canHaveMods,
                bool modsRequired,
                int? spellBookPageCount,
                DataSetFormula? pagesPerSpell,
                string? size,
                int? usedSlots,
                double? weight,
                int? armorCheckPenalty,
                DataSetAttackDefinition? secondAttack,
                DataSetAttackDefinition? attack,
                string? fumbleRange,
                int? maxDex,
                string? proficiency,
                int? range,
                int? reach,
                int? reachMultiplier,
                int? arcaneSpellFailureChance,
                string? wieldCategory,
                bool? visible,
                ImmutableDictionary<string, string> qualities,
                ImmutableList<DataSetBonus> bonuses,
                ImmutableList<string> types,
                ImmutableList<DataSetFormattable> specialProperties,
                DataSetFormattable? description,
                ImmutableList<UnboundEquipmentAddModifier> equipmentModifiers,
                string? baseItemName)
                : base(
                    name,
                    cost,
                    baseQuantity,
                    effectiveDamageResistance,
                    contains,
                    canHaveMods,
                    modsRequired,
                    spellBookPageCount,
                    pagesPerSpell,
                    size,
                    usedSlots,
                    weight,
                    armorCheckPenalty,
                    secondAttack,
                    attack,
                    fumbleRange,
                    maxDex,
                    proficiency,
                    range,
                    reach,
                    reachMultiplier,
                    arcaneSpellFailureChance,
                    wieldCategory,
                    visible,
                    qualities,
                    bonuses,
                    types,
                    specialProperties,
                    description
                )
            {
                _equipmentModifiers = equipmentModifiers;
                _baseItemName = baseItemName;
            }

            public DataSetEquipment Bind(IBinder binder)
            {
                return new DataSetEquipment(
                    Name,
                    Cost,
                    BaseQuantity,
                    EffectiveDamageResistance,
                    Contains,
                    CanHaveMods,
                    ModsRequired,
                    SpellBookPageCount,
                    PagesPerSpell,
                    Size,
                    UsedSlots,
                    Weight,
                    ArmorCheckPenalty,
                    SecondAttack,
                    Attack,
                    FumbleRange,
                    MaxDex,
                    Proficiency,
                    Range,
                    Reach,
                    ReachMultiplier,
                    ArcaneSpellFailureChance,
                    WieldCategory,
                    Visible,
                    Qualities,
                    Bonuses,
                    Types,
                    SpecialProperties,
                    Description,
                    _equipmentModifiers.Select(m => m.Bind(binder)).ToImmutableList(),
                    binder.Bind<DataSetEquipment>(_baseItemName));
            }
        }

        private class UnboundEquipmentAddModifier : DataSetEquipmentAddModifierBase, IRequiresBinding<DataSetEquipmentAddModifier>
        {
            private string _key;

            public UnboundEquipmentAddModifier(ImmutableList<DataSetFormula> parameters, string key)
                : base(parameters)
            {
                _key = key;
            }

            public DataSetEquipmentAddModifier Bind(IBinder binder)
            {
                return new DataSetEquipmentAddModifier(
                    Parameters,
                    binder.Bind<DataSetEquipmentModifier>(_key)
                );
            }
        }

        private class UnboundEquipmentModifier : DataSetEquipmentModifierBase, IRequiresBinding<DataSetEquipmentModifier>
        {
            private readonly string _nameModifier;
            private readonly string _nameModifierLocation;
            private readonly ImmutableList<string> _replaces;
            private readonly UnboundEquipment? _automaticEquipment;

            private protected UnboundEquipmentModifier(
                ImmutableList<DataSetBonus> bonuses,
                string key,
                ImmutableList<string> types,
                ImmutableList<DataSetFormattable> specialProperties,
                DataSetFormattable? description,
                string? name,
                DataSetFormula? cost,
                ImmutableList<string> grantedItemTypes,
                bool? visible,
                bool? affectsBothHeads,
                string nameModifier,
                string nameModifierLocation,
                IChooser? choice,
                ImmutableList<string> replaces,
                DataSetArmorTypeChange? armorTypeChange,
                DataSetChargeRange? charges,
                int? equivalentEnhancementBonus,
                UnboundEquipment? automaticEquipment)
                : base(
                    name,
                    key,
                    bonuses,
                    types,
                    specialProperties,
                    description,
                    cost,
                    grantedItemTypes,
                    visible,
                    affectsBothHeads,
                    choice,
                    armorTypeChange,
                    charges,
                    equivalentEnhancementBonus
                )
            {
                _nameModifier = nameModifier;
                _nameModifierLocation = nameModifierLocation;
                _replaces = replaces;
                _automaticEquipment = automaticEquipment;
            }

            public DataSetEquipmentModifier Bind(IBinder binder)
            {
                return new DataSetEquipmentModifier(
                    NameSet ?? "<<NO NAME>>",
                    Key,
                    Bonuses,
                    Types,
                    SpecialProperties,
                    Description,
                    Cost,
                    GrantedItemTypes,
                    Visible,
                    AffectsBothHeads,
                    BuildNameModifier(),
                    Choice,
                    ArmorTypeChange,
                    Charges,
                    EquivalentEnhancementBonus,
                    _replaces.Select(key => binder.Bind<DataSetEquipment>(key)).ToImmutableList(),
                    _automaticEquipment == null ? null : binder.Bind<DataSetEquipment>(_automaticEquipment.Name)
                );
            }

            private ModifyName BuildNameModifier()
            {
                string modType = _nameModifier.ToLowerInvariant();
                string loc = _nameModifierLocation.ToLowerInvariant();
                Func<string?,string> text;
                switch (modType)
                {
                    case "normal":
                        text = _ => NameSet!;
                        break;
                    case "noname":
                    case "spell":
                        text = choiceText => choiceText!;
                        break;
                    default:
                        if (modType.StartsWith("TEXT="))
                        {
                            text = _ => modType.Substring(0);
                            break;
                        }

                        text = _ => modType;
                        break;
                }

                return loc switch
                {
                    "parentheses" => (baseText, choiceText) => $"{baseText} ({text(choiceText)})",
                    "prefix" => (baseText, choiceText) => $"{text(choiceText)} {baseText}",
                    "suffix" => (baseText, choiceText) => $"{baseText} {text(choiceText)}",
                    _ => (baseText, choiceText) => $"{baseText} [[[{text(choiceText)}]]]"
                };
            }
        }

        private class UnboundAbilityScore : DataSetAbilityScoreBase, IRequiresBinding<DataSetAbilityScore>
        {
            private readonly ImmutableList<UnboundAddAbility> _abilities;

            public UnboundAbilityScore(
                string name,
                string sortKey,
                string abbreviation,
                DataSetFormula statModFormula,
                ImmutableList<DataSetModDefinition> modifications,
                ImmutableList<DataSetVariableDefinition> definitions,
                ImmutableList<DataSetBonus> bonuses,
                ImmutableList<UnboundAddAbility> abilities)
                : base(name, sortKey, abbreviation, statModFormula, modifications, definitions, bonuses)
            {
                _abilities = abilities;
            }

            public DataSetAbilityScore Bind(IBinder binder)
            {
                return new DataSetAbilityScore(
                    Name,
                    SortKey,
                    Abbreviation,
                    StatModFormula,
                    Modifications,
                    Definitions,
                    Bonuses,
                    _abilities.Select(a => a.Bind(binder)).ToImmutableList()
                );
            }
        }

        private class UnboundClass : DataSetClassBase, IRequiresBinding<DataSetClass>
        {
            private readonly ImmutableList<UnboundRepeatingClassLevel> _levels;
            private readonly string? _exClass;

            internal UnboundClass(
                string name,
                DataSourceInformation? sourceInfo,
                ImmutableDictionary<string, string> facts,
                string? sourcePage,
                DataSetCondition<CharacterInterface> condition,
                ImmutableList<DataSetVariableDefinition> definitions,
                ImmutableList<DataSetBonus> bonuses,
                ImmutableList<string> types,
                ImmutableList<string> roles,
                int? hitDie,
                int? maxLevel,
                ImmutableList<UnboundRepeatingClassLevel> levels,
                string? exClass)
                : base(
                    name,
                    sourceInfo,
                    facts,
                    sourcePage,
                    condition,
                    definitions,
                    bonuses,
                    types,
                    roles,
                    hitDie,
                    maxLevel
                )
            {
                _exClass = exClass;
                _levels = levels;
            }

            public DataSetClass Bind(IBinder binder)
            {
                return new DataSetClass(
                    Name,
                    SourceInfo,
                    Facts,
                    SourcePage,
                    Condition,
                    Definitions,
                    Bonuses,
                    Types,
                    Roles,
                    HitDie,
                    MaxLevel,
                    _levels.Select(l => l.Bind(binder)).ToImmutableList(),
                    binder.Bind<DataSetClass>(_exClass)
                );
            }

            public UnboundClass MergedWith(UnboundClass other)
            {
                return new UnboundClass(
                    other.Name ?? Name,
                    other.SourceInfo ?? SourceInfo,
                    Facts.AddRange(other.Facts),
                    other.SourcePage ?? SourcePage,
                    Condition.CombineWith(other.Condition),
                    Definitions.AddRange(other.Definitions),
                    Bonuses.AddRange(other.Bonuses),
                    Types.AddRange(other.Types),
                    Roles.AddRange(other.Roles),
                    other.HitDie ?? HitDie,
                    other.MaxLevel ?? MaxLevel,
                    _levels.AddRange(other._levels),
                    other._exClass ?? _exClass
                );
            }
        }

        private class UnboundRepeatingClassLevel
            : RepeatingDataSetClassLevelBase, IRequiresBinding<RepeatingDataSetClassLevel>
        {
            private UnboundClassLevel _info;

            public UnboundRepeatingClassLevel(int start, int repeat, UnboundClassLevel info)
                : base(start, repeat)
            {
                _info = info;
            }

            public RepeatingDataSetClassLevel Bind(IBinder binder)
            {
                return new RepeatingDataSetClassLevel(Start, Repeat, _info.Bind(binder));
            }
        }

        private class UnboundClassLevel : DataSetClassLevelBase, IRequiresBinding<DataSetClassLevel>
        {
            private ImmutableList<UnboundAddAbility> _grantedAbilities;

            public UnboundClassLevel(ImmutableList<DataSetAddedCasterLevel> addedCasterLevels, ImmutableList<UnboundAddAbility> grantedAbilities)
                : base(addedCasterLevels)
            {
                _grantedAbilities = grantedAbilities;
            }

            public DataSetClassLevel Bind(IBinder binder)
            {
                return new DataSetClassLevel(
                    AddedCasterLevels,
                    _grantedAbilities.Select(a => a.Bind(binder)).ToImmutableList()
                );
            }
        }

        private class UnboundDomain : DataSetDomainBase, DataSetLoader.IRequiresBinding<DataSetDomain>
        {
            private readonly ImmutableList<UnboundAddAbility> _grantAbilities;

            public UnboundDomain(
                string name,
                string description,
                ImmutableList<DataSetVariableDefinition> definitions,
                ImmutableList<DataSetSpellList?> spellLists,
                DataSetCondition<CharacterInterface> condition,
                ImmutableList<string> classSkills,
                string sourcePage,
                ImmutableList<UnboundAddAbility> grantAbilities)
                : base(name, description, definitions, spellLists, condition, classSkills, sourcePage)
            {
                _grantAbilities = grantAbilities;
            }

            public UnboundDomain MergeWith(UnboundDomain other)
            {
                return new UnboundDomain(
                    other.Name ?? Name,
                    other.Description ?? Description,
                    Definitions.AddRange(other.Definitions),
                    SpellLists.AddRange(other.SpellLists),
                    Condition.CombineWith(other.Condition),
                    ClassSkills.AddRange(other.ClassSkills),
                    other.SourcePage ?? SourcePage,
                    _grantAbilities.AddRange(other._grantAbilities)
                );
            }

            public DataSetDomain Bind(IBinder binder)
            {
                return new DataSetDomain(
                    Name,
                    Description,
                    Definitions,
                    SpellLists,
                    Condition,
                    ClassSkills,
                    SourcePage,
                    _grantAbilities.Select(a => a.Bind(binder)).ToImmutableList()
                );
            }
        }

        internal interface IUnbound
        {
            string BindKey { get; }
        }

        private interface IBinder
        {
            [return: NotNullIfNotNull("key")]
            TBound? Bind<TBound>(string? key) where TBound : class;
        }

        private interface IRequiresBinding<out TResult>
        {
            TResult Bind(IBinder binder);
        }
    }

    internal class StringInterface
    {
    }
}