using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using KeraLua;
using NLua;
using NLua.Exceptions;
using Splat;
using Lua = NLua.Lua;
using LuaFunction = NLua.LuaFunction;

namespace Primordially.PluginCore.Data
{
    public enum DataSetStrictness
    {
        Strict,
        Lax,
    }

    public class DataSetLoader : IEnableLogger
    {
        private readonly string _rootPath;
        private readonly DataSetStrictness _strictness;

        public DataSetLoader(string rootPath, DataSetStrictness strictness = DataSetStrictness.Strict)
        {
            _rootPath = rootPath;
            _strictness = strictness;
        }
        
        public DataSet LoadData(string path)
        {
            DataSet dataSet = PrepareEnvironment(out Lua _, out DataSetInteractions interactions);
            interactions.ImportFile(path);
            return dataSet;
        }

        private DataSet PrepareEnvironment(out Lua lua, out DataSetInteractions interactions)
        {
            lua = new Lua();
            DataSet dataSet = new DataSet(lua);
            interactions = new DataSetInteractions(dataSet, lua, this);
            lua.State.Encoding = Encoding.UTF8;
            lua.DoString("import = function() end");
            LuaRegistrationHelper.TaggedInstanceMethods(lua, interactions);
            return dataSet;
        }

        public DataSet LoadString(string data)
        {
            DataSet dataSet = PrepareEnvironment(out Lua lua, out DataSetInteractions _);
            lua.DoString(data);
            return dataSet;
        }

        public class DataSetInteractions
            : IEnableLogger
        {
            private readonly DataSet _dataSet;
            private readonly DataSetLoader _dataSetLoader;

            private readonly Stack<string> _file = new Stack<string>();
            private readonly Lua _lua;
            private DataSourceInformation? _sourceInfo;

            public DataSetInteractions(DataSet dataSet, Lua lua, DataSetLoader dataSetLoader)
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
                    ReportError(new FileNotFoundException("Could not find ImportFile target", newPath));
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
                DataSetClass def = new DataSetClass((string) table["Name"], _sourceInfo);
                if (_dataSet.Classes.ContainsKey(def.Name))
                {
                    ReportError(
                        new ArgumentException(
                            $"Ability with DefineClass with Name '{def.Name}' is called more than once"
                        )
                    );
                    _dataSet.Classes.Remove(def.Name);
                }

#nullable disable
                foreach (KeyValuePair<object, object> pair in table)
#nullable restore
                {
                    string key = (string) pair.Key;
                    switch (key)
                    {
                        case "Fact":
                        {
                            ImmutableDictionary<string, string>.Builder b = def.Facts.ToBuilder();
#nullable disable
                            foreach (KeyValuePair<object, object> factPair in (LuaTable) pair.Value)
#nullable restore
                            {
                                b.Add((string) factPair.Key, (string) factPair.Value);
                            }

                            def.Facts = b.ToImmutable();
                            break;
                        }
                        case "SourcePage":
                            def.SourcePage = (string) table["SourcePage"];
                            break;
                        case "Conditions":
                            def.Condition = ParseSingleCondition<CharacterInterface>(pair.Value);
                            break;
                        case "Definitions":
                            def.Definitions = MergeList(
                                def.Definitions,
                                (LuaTable) pair.Value,
                                ParseVariableDefinition
                            );
                            break;
                        case "Bonuses":
                            def.Bonuses = MergeList(
                                def.Bonuses,
                                (LuaTable) pair.Value,
                                ParseBonus
                            );
                            break;
                        case "Types":
                            def.Types = MergeList(def.Types, (LuaTable) pair.Value, obj => (string) obj);
                            break;
                        case "Roles":
                            def.Roles = MergeList(def.Roles, (LuaTable) pair.Value, obj => (string) obj);
                            break;
                        case "HitDice":
                            def.HitDie = (int) (long) pair.Value;
                            break;
                        case "MaxLevel":
                            def.MaxLevel = (int) (long) pair.Value;
                            break;
                        case "ExClass":
                            def.ExClass = (string) pair.Value;
                            break;
                        case "Levels":
                            def.Levels = MergeList(
                                def.Levels,
                                (LuaTable) pair.Value,
                                obj =>
                                {
                                    LuaTable t = (LuaTable) obj;
                                    (int level, int repeat) = ParseLevelString(t["Level"]);

                                    var addedCasterLevel = (LuaTable) t["AddedSpellCasterLevels"];
                                    var abilities = t["Abilities"];
                                    ImmutableList<DataSetAddAbility> expandedAbilities = ParseAbilityGrants(abilities);

                                    return new DataSetClassLevel(
                                        level,
                                        repeat,
                                        addedCasterLevel == null
                                            ? ImmutableList<DataSetAddedCasterLevel>.Empty
                                            : ParseList(
                                                addedCasterLevel,
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
                                        expandedAbilities
                                    );
                                }
                            );
                            break;
                        default:
                            ReportError(new ArgumentException($"Unknown class key '{key}'"));
                            break;
                    }
                }

                if (table["Facts"] is LuaTable facts)
                {
                    ImmutableDictionary<string, string>.Builder b = def.Facts.ToBuilder();
#nullable disable
                    foreach (KeyValuePair<object, object> pair in facts)
#nullable restore
                    {
                        b.Add((string) pair.Key, (string) pair.Value);
                    }

                    def.Facts = b.ToImmutable();
                }

                def.SourcePage = table["SourcePage"] as string ?? def.SourcePage;
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
                    ParseFormula(t["InitialValue"])
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
                                ParseList((LuaTable) at["Names"], o => (string) o)
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
                    ParseList((LuaTable) table["Variables"], o => (string) o),
                    (string) table["Formula"],
                    ParseSingleCondition<CharacterInterface>(table["Conditions"])
                );
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
                    ReportError(new ArgumentException($"Fact {key} is defined twice"));
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
                        ParseList((LuaTable) table["Definitions"], ParseVariableDefinition),
                        ParseList((LuaTable) table["Bonuses"], ParseBonus),
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
                    ReportError(new ArgumentException($"Ability Category {name} is defined twice"));
                    _dataSet.AbilityCategories.Remove(name);
                }

                _dataSet.AbilityCategories.Add(
                    name,
                    new DataSetAbilityCategory(
                        name,
                        (string) table["Category"],
                        (string) (table["Plural"] ?? table["Name"]),
                        (string?) table["DisplayLocation"],
                        ((bool?) table["Visible"]) ?? false,
                        ((bool?) table["Editable"]) ?? false,
                        ((bool?) table["EditPool"]) ?? false,
                        ((bool?) table["FractionalPool"]) ?? false,
                        (string?) table["Pool"],
                        (string?) table["AbilityList"],
                        ParseList((LuaTable) table["Types"], o => (string) o)
                    )
                );
            }

            [LuaGlobal]
            public Chooser<SkillInterface> ChooseSkill(object funcOrListOfFunc)
            {
                return ParseChooser<SkillInterface>(funcOrListOfFunc);
            }

            [LuaGlobal(Name = "ChooseAbilityselection")]
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
            public Chooser<LanguageInterface> ChooseLang(object funcOrListOfFunc)
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
                return new StringChooser(ParseList(list, o => (string) o));
            }

            [LuaGlobal(Name = "ChooseWeaponproficiency")]
            public Chooser<WeaponProficiencyInterface> ChooseWeaponProficiency(object funcOrListOfFunc)
            {
                return ParseChooser<WeaponProficiencyInterface>(funcOrListOfFunc);
            }

            private void ReportError(Exception error)
            {
                switch (_dataSetLoader._strictness)
                {
                    case DataSetStrictness.Strict:
                        _lua.State.Error(error.Message);
                        // Unreachable, "Error" throws
                        break;
                    case DataSetStrictness.Lax:
                        _lua.State.Warning(error.Message, true);
                        var trc = _lua.GetDebugTraceback();
                        this.Log().Error(error, "LUA exception at: {0}", trc);
                        return;
                }
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

            private static Func<T, bool>? ParseSingleCondition<T>(object choose, [CallerMemberName] string? name = null)
            {
                if (choose == null)
                {
                    return null;
                }

                if (choose is LuaFunction func)
                {
                    return c => SingleCall<bool>(func, c);
                }

                if (choose is LuaTable table)
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

            private static ImmutableList<T> ParseList<T>(LuaTable? table, Func<object, T> selector)
            {
                if (table == null)
                {
                    return ImmutableList<T>.Empty;
                }

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
                    ParseList((LuaTable) table["BookTypes"], x => (string) x),
                    ParseList((LuaTable) table["Types"], x => (string) x),
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
                    ParseList((LuaTable) table["Links"], ParseLink)
                );
            }

            #endregion

            #region Abilities

            [LuaGlobal]
            public void DefineAbility(LuaTable table)
            {
                string? abilityKey = (string) table["Key"];
                string abilityName = (string) table["Name"];
                if (abilityKey != null &&
                    _dataSet.GetAbility(abilityKey) != null ||
                    _dataSet.GetAbility(abilityName) != null)
                {
                    ReportError(
                        new ArgumentException(
                            $"Ability with DefineAbility with key '{abilityKey}' is called more than once"
                        )
                    );
                    _dataSet.ClearAbility(abilityName, abilityKey);
                }

                var ability = new DataSetAbility(abilityName, _sourceInfo);
                _dataSet.AddAbility(ability, abilityName, abilityKey);
                UpdateAbility(ability, table);
            }

            [LuaGlobal]
            public void ModifyAbility(LuaTable table)
            {
                string? key = (string) table["Key"];
                DataSetAbility ability;
                if (key != null)
                {
                    DataSetAbility? foundAbility = _dataSet.GetAbility(key);
                    if (foundAbility == null)
                    {
                        ReportError(new ArgumentException($"No ability with Key '{key}' found"));
                        DefineAbility(table);
                        foundAbility = _dataSet.GetAbility(key)!;
                    }

                    ability = foundAbility;
                }
                else
                {
                    DataSetAbility? foundAbility = _dataSet.GetAbility((string) table["Name"]);
                    if (foundAbility == null)
                    {
                        ReportError(new ArgumentException($"No ability with Name '{table["Name"]}' found"));
                        DefineAbility(table);
                        foundAbility = _dataSet.GetAbility((string) table["Name"])!;
                    }

                    ability = foundAbility;
                }

                UpdateAbility(ability, table);
            }

            private void UpdateAbility(DataSetAbility ability, LuaTable table)
            {
#nullable disable
                foreach (KeyValuePair<object, object> pair in table)
#nullable restore
                {
                    string key = (string) pair.Key;
                    switch (key)
                    {
                        case "Name":
                        case "Key":
                            break;
                        case "Category":
                            ability.Category = (string) pair.Value;
                            break;
                        case "Description":
                            ability.Description = ParseFormatable(pair.Value);
                            break;
                        case "Stackable":
                            ability.Stackable = (bool) pair.Value;
                            break;
                        case "AllowMultiple":
                            ability.AllowMultiple = (bool) pair.Value;
                            break;
                        case "Visible":
                            ability.Visible = (bool) pair.Value;
                            break;
                        case "Types":
                            ability.Types = MergeList(ability.Types, (LuaTable) pair.Value, o => (string)o);
                            break;
                        case "Definitions":
                            ability.Definitions = MergeList(ability.Definitions, (LuaTable) pair.Value, ParseVariableDefinition);
                            break;
                        case "Bonuses":
                            ability.Bonuses = MergeList(ability.Bonuses, (LuaTable) pair.Value, ParseBonus);
                            break;
                        case "Abilities":
                            var addAbility = ability.Abilities.ToBuilder();
                            addAbility.AddRange(ParseAbilityGrants(pair.Value));
                            ability.Abilities = addAbility.ToImmutable();
                            break;
                        case "Aspects":
                            ability.Aspects = MergeList(ability.Aspects, (LuaTable) pair.Value,
                                o =>
                                {
                                    var t = (LuaTable) o;
                                    return new DataSetAspect(
                                        (string) t["Name"],
                                        (string) t["FormatString"],
                                        ParseList((LuaTable?) t["ArgumentList"], o => (string) o)
                                    );
                                });
                            break;
                        case "Cost":
                            ability.Cost = (int) (long) pair.Value;
                            break;
                        case "SourcePage":
                            ability.SourcePage = (string) pair.Value;
                            break;
                        default:
                            ReportError(new ArgumentException($"Unknown ability key '{key}'"));
                            break;
                    }
                }
            }

            private DataSetFormattable ParseFormatable(object value)
            {
                if (value is null)
                {
                    return new DataSetFormattable("", ImmutableList<string>.Empty);
                }
                if (value is string str)
                {
                    return new DataSetFormattable(str, ImmutableList<string>.Empty);
                }

                if (value is LuaTable table)
                {
                    return new DataSetFormattable((string) table["FormatString"], ParseList((LuaTable?) table["Arguments"], o => (string)o));
                }

                throw new ArgumentException("Formatable object is not correctly defined");
            }

            #endregion
        }
    }

    public class DataSetFormattable
    {
        public DataSetFormattable(string formatString, ImmutableList<string> arguments)
        {
            FormatString = formatString;
            Arguments = arguments;
        }

        public string FormatString { get; }
        public ImmutableList<string> Arguments { get; }
    }

    public class DataSetAspect
        : DataSetFormattable
    {
        public DataSetAspect(string name, string formatString, ImmutableList<string> arguments) : base(formatString, arguments)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class DataSetAbilityCategory
    {
        public DataSetAbilityCategory(
            string name,
            string category,
            string plural,
            string? displayLocation,
            bool visible,
            bool editable,
            bool editPool,
            bool fractionalPool,
            string? pool,
            string? abilityList,
            ImmutableList<string> types)
        {
            Name = name;
            Category = category;
            Plural = plural;
            DisplayLocation = displayLocation;
            Visible = visible;
            Editable = editable;
            EditPool = editPool;
            FractionalPool = fractionalPool;
            Pool = pool;
            AbilityList = abilityList;
            Types = types;
        }

        public string Name { get; }
        public string Category { get; }
        public string Plural { get; }
        public string? DisplayLocation { get; }
        public bool Visible { get; }
        public bool Editable { get; }
        public bool EditPool { get; }
        public bool FractionalPool { get; }
        public string? Pool { get; }
        public string? AbilityList { get; }
        public ImmutableList<string> Types { get; }
    }

    public class DataSetVariable
    {
        public string Name { get; }
        public string Type { get; }
        public string? Channel { get; }
        public string? Scope { get; }

        public DataSetVariable(string name, string type, string? channel, string? scope)
        {
            Name = name;
            Type = type;
            Channel = channel;
            Scope = scope;
        }
    }

    public class DataSetAbilityScore
    {
        public DataSetAbilityScore(
            string name,
            string sortKey,
            string abbreviation,
            DataSetFormula statModFormula,
            ImmutableList<DataSetModDefinition> modifications,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<DataSetAddAbility> abilities)
        {
            Name = name;
            SortKey = sortKey;
            Abbreviation = abbreviation;
            StatModFormula = statModFormula;
            Modifications = modifications;
            Definitions = definitions;
            Bonuses = bonuses;
            Abilities = abilities;
        }

        public string Name { get; }
        public string SortKey { get; }
        public string Abbreviation { get; }
        public DataSetFormula StatModFormula { get; }
        public ImmutableList<DataSetModDefinition> Modifications { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<DataSetAddAbility> Abilities { get; }
    }

    public class DataSetModDefinition
    {
        public DataSetModDefinition(string target, string action, DataSetFormula value)
        {
            Target = target;
            Action = action;
            Value = value;
        }

        public string Target { get; }
        public string Action { get; }
        public DataSetFormula Value { get; }
    }

    public class DataSetSave
    {
        public string Name { get; }
        public string SortKey { get; }
        public DataSetBonus Bonus { get; }

        public DataSetSave(string name, string sortKey, DataSetBonus bonus)
        {
            Name = name;
            SortKey = sortKey;
            Bonus = bonus;
        }
    }

    public class DataSetFact
    {
        public DataSetFact(
            string category,
            bool selectable,
            bool visible,
            bool required,
            string dataFormat,
            string? displayName,
            string? explanation)
        {
            Category = category;
            Selectable = selectable;
            Visible = visible;
            Required = required;
            DataFormat = dataFormat;
            DisplayName = displayName;
            Explanation = explanation;
        }

        public string Category { get; }
        public bool Selectable { get; }
        public bool Visible { get; }
        public bool Required { get; }
        public string DataFormat { get; }
        public string? DisplayName { get; }
        public string? Explanation { get; }
    }

    public class DataSetAlignment
    {
        public DataSetAlignment(string name, string abbreviation, string sortKey)
        {
            Name = name;
            Abbreviation = abbreviation;
            SortKey = sortKey;
        }

        public string Name { get; }
        public string Abbreviation { get; }
        public string SortKey { get; }
    }

    public class DataSetFormula
    {
        private readonly int _staticValue;

        public DataSetFormula(int staticValue)
        {
            _staticValue = staticValue;
        }
    }

    public class DataSetAddAbility
    {
        public DataSetAddAbility(string category, string nature, string name)
        {
            Category = category;
            Nature = nature;
            Name = name;
        }

        public string Category { get; }
        public string Nature { get; }
        public string Name { get; }
    }

    public class DataSetAddedCasterLevel
    {
        public DataSetAddedCasterLevel(string? typeRestriction)
        {
            TypeRestriction = typeRestriction;
        }

        public string? TypeRestriction { get; }
    }

    public class DataSetClassLevel
    {
        public DataSetClassLevel(
            int level,
            int repeat,
            ImmutableList<DataSetAddedCasterLevel> addedCasterLevels,
            ImmutableList<DataSetAddAbility> grantedAbilities)
        {
            Level = level;
            Repeat = repeat;
            AddedCasterLevels = addedCasterLevels;
            GrantedAbilities = grantedAbilities;
        }

        public int Level { get; }
        public int Repeat { get; }
        public ImmutableList<DataSetAddedCasterLevel> AddedCasterLevels { get; }
        public ImmutableList<DataSetAddAbility> GrantedAbilities { get; }
    }

    public class DataSetBonus
    {
        public DataSetBonus(
            string category,
            ImmutableList<string> variables,
            string formula,
            Func<CharacterInterface, bool>? condition)
        {
            Category = category;
            Variables = variables;
            Formula = formula;
            Condition = condition;
        }

        public string Category { get; }
        public ImmutableList<string> Variables { get; }
        public string Formula { get; }
        public Func<CharacterInterface, bool>? Condition { get; }
    }

    public class DataSetVariableDefinition
    {
        public DataSetVariableDefinition(string name, DataSetFormula initialValue)
        {
            Name = name;
            InitialValue = initialValue;
        }

        public string Name { get; }
        public DataSetFormula InitialValue { get; }
    }

    public class StringChooser
    {
        public StringChooser(ImmutableList<string> options)
        {
            Options = options;
        }

        public ImmutableList<string> Options { get; }
    }

    public class SchoolInterface
    {
    }

    public class WeaponProficiencyInterface
    {
    }

    public class ClassInterface
    {
    }

    public class LanguageInterface
    {
    }

    public class Chooser<T>
    {
        private readonly Func<CharacterInterface, T, bool> _filter;

        public Chooser(Func<CharacterInterface, T, bool> filter)
        {
            _filter = filter;
        }

        public ImmutableList<T> Filter(
            CharacterInterface character,
            ImmutableList<T> all)
        {
            return all.Where(s => _filter(character, s)).ToImmutableList();
        }
    }

    public class AbilityInterface
    {
    }

    public class SpellInterface
    {
    }

    public class UserInputChooser
    {
        public UserInputChooser(int count, string prompt)
        {
            Count = count;
            Prompt = prompt;
        }

        public int Count { get; }
        public string Prompt { get; }
    }

    public class SkillInterface
    {
    }

    public class CharacterInterface
    {
    }

    public sealed class DataSet : IDisposable
    {
        private readonly Lua _luaContext;
        private readonly Dictionary<string, DataSetAbility> _keyedAbilities = new Dictionary<string, DataSetAbility>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataSetAbility> _namedAbilities = new Dictionary<string, DataSetAbility>(StringComparer.OrdinalIgnoreCase);

        internal DataSet(Lua luaContext)
        {
            _luaContext = luaContext;
        }

        public DataSetInformation? DataSetInformation { get; set; }

        public Dictionary<string, DataSetClass> Classes { get; } = new Dictionary<string, DataSetClass>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetAlignment> Alignments { get; } = new Dictionary<string, DataSetAlignment>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetFact> Facts { get; } = new Dictionary<string, DataSetFact>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetSave> Saves { get; set; } = new Dictionary<string, DataSetSave>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetAbilityScore> AbilityScores { get; } =  new Dictionary<string, DataSetAbilityScore>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetVariable> Variables { get; } = new Dictionary<string, DataSetVariable>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DataSetAbilityCategory> AbilityCategories { get; } = new Dictionary<string, DataSetAbilityCategory>(StringComparer.OrdinalIgnoreCase);

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

        public void Dispose()
        {
            _luaContext.Dispose();
        }
    }

    public class DataSetClass
    {
        public DataSetClass(string name, DataSourceInformation? sourceInfo)
        {
            Name = name;
            SourceInfo = sourceInfo;
        }

        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableDictionary<string, string> Facts { get; set; } = ImmutableDictionary<string, string>.Empty;
        public string? SourcePage { get; set; }
        public Func<CharacterInterface, bool>? Condition { get; set; }

        public ImmutableList<DataSetVariableDefinition> Definitions { get; set; } =
            ImmutableList<DataSetVariableDefinition>.Empty;

        public ImmutableList<DataSetBonus> Bonuses { get; set; } = ImmutableList<DataSetBonus>.Empty;
        public ImmutableList<string> Types { get; set; } = ImmutableList<string>.Empty;
        public ImmutableList<string> Roles { get; set; } = ImmutableList<string>.Empty;
        public int HitDie { get; set; }
        public int MaxLevel { get; set; }
        public string? ExClass { get; set; }
        public ImmutableList<DataSetClassLevel> Levels { get; set; } = ImmutableList<DataSetClassLevel>.Empty;
    }

    public class DataSetAbility
    {
        public DataSetAbility(string name, DataSourceInformation? sourceInfo)
        {
            Name = name;
            SourceInfo = sourceInfo;
        }

        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; set; } = ImmutableList<DataSetBonus>.Empty;
        public ImmutableList<DataSetAddAbility> Abilities { get; set; } = ImmutableList<DataSetAddAbility>.Empty;
        public bool Stackable { get; set; }
        public string? Category { get; set; }
        public bool AllowMultiple { get; set; }

        public ImmutableList<DataSetVariableDefinition> Definitions { get; set; } = ImmutableList<DataSetVariableDefinition>.Empty;
        public ImmutableList<DataSetAspect> Aspects { get; set; } = ImmutableList<DataSetAspect>.Empty;
        public ImmutableList<string> Types { get; set; } = ImmutableList<string>.Empty;
        public int Cost { get; set; }
        public DataSetFormattable? Description { get; set; }
        public string? SourcePage { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class DataSourceInformation
    {
        public DataSourceInformation(string l, string s, string web, DateTimeOffset date)
        {
            Long = l;
            Short = s;
            Web = web;
            Date = date;
        }

        public string Long { get; }
        public string Short { get; }
        public string Web { get; }
        public DateTimeOffset Date { get; }
    }

    public class DataSetInformation
    {
        public DataSetInformation(
            string name,
            DataSourceInformation sourceInformation,
            string gameMode,
            ImmutableList<string> bookTypes,
            ImmutableList<string> types,
            string status,
            string copyright,
            string description,
            string genre,
            string infoText,
            string helpUrl,
            bool isMature,
            bool isOgl,
            bool isLicensed,
            Predicate<DataSetInformation> conditions,
            DataSetPublisherInformation publisherInfo,
            int rang,
            bool showInMenu,
            string setting,
            ImmutableList<DataSetLink> lInks)
        {
            Name = name;
            SourceInformation = sourceInformation;
            GameMode = gameMode;
            BookTypes = bookTypes;
            Types = types;
            Status = status;
            Copyright = copyright;
            Description = description;
            Genre = genre;
            InfoText = infoText;
            HelpUrl = helpUrl;
            IsMature = isMature;
            IsOgl = isOgl;
            IsLicensed = isLicensed;
            Conditions = conditions;
            PublisherInfo = publisherInfo;
            Rang = rang;
            ShowInMenu = showInMenu;
            Setting = setting;
            LInks = lInks;
        }

        public string Name { get; }
        public DataSourceInformation SourceInformation { get; }
        public string GameMode { get; }
        public ImmutableList<string> BookTypes { get; }
        public ImmutableList<string> Types { get; }
        public string Status { get; }
        public string Copyright { get; }
        public string Description { get; }
        public string Genre { get; }
        public string InfoText { get; }
        public string HelpUrl { get; }
        public bool IsMature { get; }
        public bool IsOgl { get; }
        public bool IsLicensed { get; }
        public Predicate<DataSetInformation> Conditions { get; }
        public DataSetPublisherInformation PublisherInfo { get; }
        public int Rang { get; }
        public bool ShowInMenu { get; }
        public string Setting { get; }
        public ImmutableList<DataSetLink> LInks { get; }
    }

    public class DataSetLink
    {
        public DataSetLink(string name, string url, string text)
        {
            Name = name;
            Url = url;
            Text = text;
        }

        public string Name { get; }
        public string Url { get; }
        public string Text { get; }
    }

    public class DataSetPublisherInformation
    {
        public DataSetPublisherInformation(string shortName, string longName, string url)
        {
            ShortName = shortName;
            LongName = longName;
            Url = url;
        }

        public string ShortName { get; }
        public string LongName { get; }
        public string Url { get; }
    }
}