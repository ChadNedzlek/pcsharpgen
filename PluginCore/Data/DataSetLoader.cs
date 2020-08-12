using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NLua;
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

                DataSetClass dataSetClass = ParseClassLevel(table);

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
                    new DataSetDomain(
                        name,
                        (string) table["Description"],
                        ParseList(table["Definitions"], ParseVariableDefinition),
                        ParseAbilityGrants(table["Abilities"]),
                        ParseList(table["SpellLists"], ParseSpellList),
                        ParseSingleCondition<CharacterInterface>(table["Conditions"]),
                        ParseList(table["ClassSkills"], Stringify),
                        (string) table["SourcePage"]
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
                    new DataSetAbilityScore(
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
            public Chooser<SkillInterface> ChooseSkill(object funcOrListOfFunc)
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
            public Chooser<SpellInterface> ChooseSpell(object funcOrListOfFunc)
            {
                return ParseChooser<SpellInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public Chooser<LanguageInterface> ChooseLanguage(object funcOrListOfFunc)
            {
                return ParseChooser<LanguageInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public Chooser<ClassInterface> ChooseClass(object funcOrListOfFunc)
            {
                return ParseChooser<ClassInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public Chooser<SchoolInterface> ChooseSchool(object funcOrListOfFunc)
            {
                return ParseChooser<SchoolInterface>(funcOrListOfFunc);
            }

            [LuaGlobal]
            public StringChooser ChooseString(LuaTable list)
            {
                return new StringChooser(ParseList(list, Stringify));
            }

            [LuaGlobal]
            public Chooser<WeaponProficiencyInterface> ChooseWeaponProficiency(object funcOrListOfFunc)
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

            private DataSetClass ParseClassLevel(LuaTable table)
            {
                var levels = ParseList(
                    (LuaTable) table["Levels"],
                    obj =>
                    {
                        LuaTable t = (LuaTable) obj;
                        (int level, int repeat) = ParseLevelString(t["Level"]);
                        return new RepeatingDataSetClassLevel(
                            level,
                            repeat,
                            new DataSetClassLevel(
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

                var dataSetClass = new DataSetClass(
                    (string) table["Name"],
                    _sourceInfo,
                    ParseDict(table["Fact"], Stringify),
                    (string?) table["SourcePage"],
                    ParseSingleCondition<CharacterInterface>(table["Condition"], "DefineClass"),
                    ParseList((LuaTable?) table["Definitions"], ParseVariableDefinition),
                    ParseList((LuaTable?) table["Bonuses"], ParseBonus),
                    ParseList((LuaTable?) table["Types"], Stringify),
                    ParseList((LuaTable?) table["Roles"], Stringify),
                    (int?) (long?) table["HitDice"],
                    (int?) (long?) table["MaxLevel"],
                    (string?) table["ExClass"],
                    levels
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

            private static ImmutableList<DataSetAddAbility> ParseAbilityGrants(object obj)
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
                            n => new DataSetAddAbility(
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

            private static Func<T, bool>? ParseSingleCondition<T>(
                object condition,
                [CallerMemberName] string? name = null)
            {
                if (condition == null)
                {
                    return null;
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

            private static Chooser<T> ParseChooser<T>(object choose, [CallerMemberName] string? name = null)
            {
                Func<CharacterInterface, T, bool> filter;
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
                DataSetAbility ability;
                if (key != null)
                {
                    DataSetAbility? foundAbility = _dataSet.GetAbility(key);
                    if (foundAbility == null)
                    {
                        ReportError($"No ability with Key '{key}' found");
                        DefineAbility(table);
                        foundAbility = DataSetAbility.Empty;
                    }

                    ability = foundAbility;
                }
                else
                {
                    DataSetAbility? foundAbility = _dataSet.GetAbility(name);
                    if (foundAbility == null)
                    {
                        ReportError($"No ability with Name '{table["Name"]}' found");
                        DefineAbility(table);
                        foundAbility = DataSetAbility.Empty;
                    }

                    ability = foundAbility;
                }

                DataSetAbility newAbility = ParseAbility(table);

                _dataSet.ClearAbility(name, key);
                _dataSet.AddAbility(ability.MergedWith(newAbility), name, key);
            }

            private static readonly ImmutableHashSet<string> _knownAbilityKeys = ImmutableHashSet.Create(
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

            private DataSetAbility ParseAbility(LuaTable table)
            {
                foreach (var unknownKey in table.Keys.Cast<string>().Where(k => !_knownAbilityKeys.Contains(k)))
                {
                    ReportError($"Unknown key {unknownKey} in DefineAbility/ModifyAbility");
                }

                return new DataSetAbility(
                    (string) table["Name"],
                    _sourceInfo,
                    ParseList((LuaTable?) table["Bonuses"], ParseBonus),
                    ParseAbilityGrants(table["Abilities"]),
                    (bool?) table["Stackable"],
                    (string?) table["Category"],
                    (bool?) table["AllowMultiple"],
                    (bool?) table["Visible"],
                    ParseList((LuaTable?) table["Definitions"], ParseVariableDefinition),
                    ParseList((LuaTable?) table["Aspects"], ParseAspect),
                    ParseList((LuaTable?) table["Types"], Stringify),
                    (int?) (long?) table["Cost"],
                    ParseFormattable(table["Description"]),
                    (string?) table["SourcePage"]
                );
            }

            private DataSetAspect ParseAspect(object o)
            {
                var t = (LuaTable) o;
                return new DataSetAspect(
                    (string) t["Name"],
                    (string) t["FormatString"],
                    ParseList((LuaTable?) t["ArgumentList"], Stringify)
                );
            }

            private DataSetFormattable ParseFormattable(object value)
            {
                return value switch
                {
                    DataSetFormattable f => f,
                    null => new DataSetFormattable("0", ImmutableList<string>.Empty),
                    string str => new DataSetFormattable(str, ImmutableList<string>.Empty),
                    LuaTable table => new DataSetFormattable(
                        (string) table["FormatString"],
                        ParseList((LuaTable?) table["Arguments"], Stringify)
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
            private readonly ImmutableDictionary<string, DataSetAbility>.Builder _keyedAbilities =
                ImmutableDictionary.CreateBuilder<string, DataSetAbility>(StringComparer.OrdinalIgnoreCase);

            private readonly Lua _luaContext;

            private readonly ImmutableDictionary<string, DataSetAbility>.Builder _namedAbilities =
                ImmutableDictionary.CreateBuilder<string, DataSetAbility>(StringComparer.OrdinalIgnoreCase);

            internal DataSetBuilder(Lua luaContext)
            {
                _luaContext = luaContext;
            }

            public DataSetInformation? DataSetInformation { get; set; }

            public ImmutableDictionary<string, DataSetClass>.Builder Classes { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetClass>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetAlignment>.Builder Alignments { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetAlignment>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetFact>.Builder Facts { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetFact>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetSave>.Builder Saves { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetSave>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetAbilityScore>.Builder AbilityScores { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetAbilityScore>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetVariable>.Builder Variables { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetVariable>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetAbilityCategory>.Builder AbilityCategories { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetAbilityCategory>(StringComparer.OrdinalIgnoreCase);

            public ImmutableDictionary<string, DataSetDomain>.Builder Domains { get; } =
                ImmutableDictionary.CreateBuilder<string, DataSetDomain>(StringComparer.OrdinalIgnoreCase);

            public DataSetAbility? GetAbility(string nameOrKey)
            {
                return _keyedAbilities.GetValueOrDefault(nameOrKey) ??
                    _namedAbilities.GetValueOrDefault(nameOrKey);
            }

            public void AddAbility(DataSetAbility ability, string name, string? key = null)
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
                return new DataSet(
                    _luaContext,
                    DataSetInformation,
                    new AbilityDictionary(_keyedAbilities.ToImmutable(), _namedAbilities.ToImmutable()),
                    Classes.ToImmutable(),
                    Alignments.ToImmutable(),
                    Facts.ToImmutable(),
                    Saves.ToImmutable(),
                    AbilityScores.ToImmutable(),
                    Variables.ToImmutable(),
                    AbilityCategories.ToImmutable(),
                    Domains.ToImmutable()
                );
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
                ImmutableDictionary<string, DataSetDomain> domains)
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
            }

            public ImmutableDictionary<string, DataSetClass> Classes { get; }
            public ImmutableDictionary<string, DataSetAlignment> Alignments { get; }
            public ImmutableDictionary<string, DataSetFact> Facts { get; }
            public ImmutableDictionary<string, DataSetSave> Saves { get; }
            public ImmutableDictionary<string, DataSetAbilityScore> AbilityScores { get; }
            public ImmutableDictionary<string, DataSetVariable> Variables { get; }
            public ImmutableDictionary<string, DataSetAbilityCategory> AbilityCategories { get; }
            public ImmutableDictionary<string, DataSetDomain> Domains { get; }

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
    }

    internal class DataSetDomain
    {
        public string Name { get; }
        public string Description { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetAddAbility> GrantAbilities { get; }
        public ImmutableList<DataSetSpellList?> SpellLists { get; }
        public Func<CharacterInterface, bool>? Condition { get; }
        public ImmutableList<string> ClassSkills { get; }
        public string SourcePage { get; }

        public DataSetDomain(
            string name,
            string description,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAddAbility> grantAbilities,
            ImmutableList<DataSetSpellList?> spellLists,
            Func<CharacterInterface, bool>? condition,
            ImmutableList<string> classSkills,
            string sourcePage)
        {
            Name = name;
            Description = description;
            Definitions = definitions;
            GrantAbilities = grantAbilities;
            SpellLists = spellLists;
            Condition = condition;
            ClassSkills = classSkills;
            SourcePage = sourcePage;
        }
    }

    public class DataSetSpellListLevel
    {
        public int Level { get; }
        public ImmutableList<string> Spells { get; }

        public DataSetSpellListLevel(int level, ImmutableList<string> spells)
        {
            Level = level;
            Spells = spells;
        }
    }

    public class DataSetSpellList
    {
        public string Kind { get; }
        public string Name { get; }
        public ImmutableList<DataSetSpellListLevel> Levels { get; }

        public DataSetSpellList(string kind, string name, ImmutableList<DataSetSpellListLevel> levels)
        {
            Kind = kind;
            Name = name;
            Levels = levels;
        }
    }
}