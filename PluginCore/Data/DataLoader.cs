using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NLua;
using Primordially.Core;

namespace Primordially.PluginCore.Data
{
    public enum DataSetStrictness
    {
        Strict,
        Lax,
    }

    public class DataSetLoader
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
            DataSet dataSet = new DataSet();
            using var lua = new Lua();
            DataSetInteractions interactions = new DataSetInteractions(dataSet, lua, this);
            lua.State.Encoding = Encoding.UTF8;
            lua.DoString("import = function() end");
            LuaRegistrationHelper.TaggedInstanceMethods(lua, interactions);
            interactions.ImportFile(path);
            return dataSet;
        }

        public class DataSetInteractions
        {
            private readonly Lua _lua;
            private readonly DataSetLoader _dataSetLoader;
            private readonly DataSet _dataSet;

            private readonly Stack<string> _file = new Stack<string>();
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
                var newPath = ResolvePath(Path.GetDirectoryName(basePath), path);
                if (!File.Exists(newPath))
                {
                    switch (_dataSetLoader._strictness)
                    {
                        case DataSetStrictness.Strict:
                            throw new FileNotFoundException("Could not find ImportFile target", newPath);
                        case DataSetStrictness.Lax: return;
                    }
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

            public class IntermediateGrandAbility
            {
                public string Category { get; }
                public string Nature { get; }
                public ImmutableList<string> Names { get; }

                public IntermediateGrandAbility(string category, string nature, ImmutableList<string> names)
                {
                    Category = category;
                    Nature = nature;
                    Names = names;
                }
            }

            [LuaGlobal]
            public void DefineClass(LuaTable table)
            {
                DataSetClass def = new DataSetClass((string) table["Name"], _sourceInfo);
                if (_dataSet.Classes.ContainsKey(def.Name))
                {
                    switch (_dataSetLoader._strictness)
                    {
                        case DataSetStrictness.Strict:
                            throw new ArgumentException(
                                $"Ability with DefineClass with Name '{def.Name}' is called more than once"
                            );
                        case DataSetStrictness.Lax:
                            _dataSet.Classes.Remove(def.Name);
                            break;
                    }
                }

                foreach (KeyValuePair<object, object> pair in table)
                {
                    string key = (string) pair.Key;
                    switch (key)
                    {
                        case "Fact":
                        {
                            ImmutableDictionary<string, string>.Builder b = def.Facts.ToBuilder();
                            foreach (KeyValuePair<object, object> factPair in (LuaTable) pair.Value)
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
                                obj =>
                                {
                                    LuaTable t = (LuaTable) obj;
                                    return new DataSetVariableDefinition(
                                        (string) t["Name"],
                                        ParseFormula(t["InitialValue"])
                                    );
                                }
                            );
                            break;
                        case "Bonuses":
                            def.Bonuses = MergeList(
                                def.Bonuses,
                                (LuaTable) pair.Value,
                                obj =>
                                {
                                    LuaTable t = (LuaTable) obj;
                                    return new DataSetBonus(
                                        (string) t["Category"],
                                        ParseList((LuaTable) t["Variables"], o => (string) o),
                                        (string) t["Formula"],
                                        ParseSingleCondition<CharacterInterface>(t["Conditions"])
                                    );
                                }
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
                            def.Levels = MergeList<DataSetClassLevel>(
                                def.Levels,
                                (LuaTable) pair.Value,
                                obj =>
                                {
                                    LuaTable t = (LuaTable) obj;
                                    var addedCasterLevel = (LuaTable) t["AddedSpellCasterLevels"];
                                    var abilities = (LuaTable) t["Abilities"];
                                    ImmutableList<IntermediateGrandAbility> addedAbilities = abilities == null
                                        ? ImmutableList<IntermediateGrandAbility>.Empty
                                        : ParseList(
                                            abilities,
                                            a =>
                                            {
                                                LuaTable at = (LuaTable) a;
                                                return new IntermediateGrandAbility(
                                                    (string) at["Category"],
                                                    (string) at["Nature"],
                                                    ParseList((LuaTable) at["Names"], o => (string) o)
                                                );
                                            }
                                        );

                                    return new DataSetClassLevel(
                                        int.Parse((string) t["Level"]),
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
                                        addedAbilities.SelectMany(
                                                ab => ab.Names.Select(
                                                    n => new DataSetAddAbility(
                                                        ab.Category,
                                                        ab.Nature,
                                                        n
                                                    )
                                                )
                                            )
                                            .ToImmutableList()
                                    );
                                }
                            );
                            break;
                        default:
                            if (_dataSetLoader._strictness == DataSetStrictness.Strict)
                            {
                                throw new ArgumentException($"Unknown class key '{key}'");
                            }

                            break;

                    }
                }

                if (table["Facts"] is LuaTable facts)
                {
                    ImmutableDictionary<string, string>.Builder b = def.Facts.ToBuilder();
                    foreach (KeyValuePair<object,object> pair in facts)
                    {
                        b.Add((string) pair.Key, (string) pair.Value);
                    }

                    def.Facts = b.ToImmutable();
                }

                def.SourcePage = table["SourcePage"] as string ?? def.SourcePage;
            }

            private static DataSetFormula ParseFormula(object obj)
            {
                if (obj is long l)
                {
                    return new DataSetFormula((int) l);
                }

                if (obj is string s)
                {
                    if (int.TryParse(s, out var i))
                    {
                        return new DataSetFormula(i);
                    }

                    return new DataSetFormula(-666);
                }

                throw new ArgumentException("Unparsable formula value");
            }

            #region Abilities

            [LuaGlobal]
            public void DefineAbility(LuaTable table)
            {
                string? abilityKey = (string) table["Key"];
                string abilityName = (string) table["Name"];
                if (abilityKey != null && _dataSet.GetAbility(abilityKey) != null || _dataSet.GetAbility(abilityName) != null)
                {
                    switch (_dataSetLoader._strictness)
                    {
                        case DataSetStrictness.Strict:
                            throw new ArgumentException(
                                $"Ability with DefineAbility with key '{abilityKey}' is called more than once"
                            );
                        case DataSetStrictness.Lax:
                            _dataSet.ClearAbility(abilityName, abilityKey);
                            break;
                    }
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
                        switch (_dataSetLoader._strictness)
                        {
                            case DataSetStrictness.Strict:
                                throw new ArgumentException($"No ability with Key '{key}' found");
                            case DataSetStrictness.Lax:
                                DefineAbility(table);
                                return;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    ability = foundAbility;
                }
                else
                {
                    DataSetAbility? foundAbility = _dataSet.GetAbility((string) table["Name"]);
                    if (foundAbility == null)
                    {
                        switch (_dataSetLoader._strictness)
                        {
                            case DataSetStrictness.Strict:
                                throw new ArgumentException($"No ability with Name '{table["Name"]}' found");
                            case DataSetStrictness.Lax:
                                DefineAbility(table);
                                return;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    ability = foundAbility;
                }

                UpdateAbility(ability, table);
            }

            private void UpdateAbility(DataSetAbility ability, LuaTable table)
            {
                foreach (KeyValuePair<object, object> pair in table)
                {
                    string key = (string) pair.Key;
                    switch (key)
                    {
                        case "Name":
                        case "Key":
                            break;
                        default:
                            if (_dataSetLoader._strictness == DataSetStrictness.Strict)
                            {
                                throw new ArgumentException($"Unknown ability key '{key}'");
                            }

                            break;
                    }
                }
            }

            #endregion

            [LuaGlobal]
            public Chooser<SkillInterface> ChooseSkill(object funcOrListOfFunc)
            {
                return ParseChooser<SkillInterface>(funcOrListOfFunc);
            }

            [LuaGlobal(Name = "ChooseAbilityselection")]
            public object ChooseAbilitySelection(object funcOrListOfFunc)
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

            private static Predicate<T>? ParseSingleCondition<T>(object choose, [CallerMemberName] string name = null)
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
                    return  c => table.Values.Cast<LuaFunction>().All(f => SingleCall<bool>(f, c));
                }

                throw new ArgumentException($"{name} is incorrectly defined");
            }

            private static Chooser<T> ParseChooser<T>(object choose, [CallerMemberName] string name = null)
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

            private static ImmutableList<T> ParseList<T>(LuaTable table, Func<object, T> selector)
            {
                int limit = table.Values.Count;
                var builder = ImmutableList.CreateBuilder<T>();
                for (int i = 1; i <= limit; i++)
                {
                    builder.Add(selector(table[i]));
                }

                return builder.ToImmutable();
            }

            private static ImmutableList<T> MergeList<T>(ImmutableList<T> initial, LuaTable table, Func<object, T> selector)
            {
                ImmutableList<T>.Builder b = initial.ToBuilder();
                b.AddRange(ParseList(table, selector));
                return b.ToImmutable();
            }

            private static T SingleCall<T>(LuaFunction func, params  object[] args)
            {
                var result = func.Call(args);
                if (result.Length != 1)
                    throw new ArgumentException("Lua function returned multiple (or no) arguments");

                return (T) result[0];
            }
        }
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
        public string Category { get; }
        public string Nature { get; }
        public string Name { get; }

        public DataSetAddAbility(string category, string nature, string name)
        {
            Category = category;
            Nature = nature;
            Name = name;
        }
    }

    public class DataSetAddedCasterLevel
    {
        public DataSetAddedCasterLevel(string typeRestriction)
        {
            TypeRestriction = typeRestriction;
        }

        public string? TypeRestriction { get; }
    }

    public class DataSetClassLevel
    {
        public int Level { get; }
        public ImmutableList<DataSetAddedCasterLevel> AddedCasterLevels { get; }
        public ImmutableList<DataSetAddAbility> GrantedAbilities { get; }

        public DataSetClassLevel(int level, ImmutableList<DataSetAddedCasterLevel> addedCasterLevels, ImmutableList<DataSetAddAbility> grantedAbilities)
        {
            Level = level;
            AddedCasterLevels = addedCasterLevels;
            GrantedAbilities = grantedAbilities;
        }
    }

    public class DataSetBonus
    {
        public string Category { get; }
        public ImmutableList<string> Variables { get; }
        public string Formula { get; }
        public Predicate<CharacterInterface> Condition { get; }

        public DataSetBonus(string category, ImmutableList<string> variables, string formula, Predicate<CharacterInterface> condition)
        {
            Category = category;
            Variables = variables;
            Formula = formula;
            Condition = condition;
        }
    }

    public class DataSetVariableDefinition
    {
        public string Name { get; }
        public DataSetFormula InitialValue { get; }

        public DataSetVariableDefinition(string name, DataSetFormula initialValue)
        {
            Name = name;
            InitialValue = initialValue;
        }
    }

    public class StringChooser
    {
        public ImmutableList<string> Options { get; }

        public StringChooser(ImmutableList<string> options)
        {
            Options = options;
        }
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
        public int Count { get; }
        public string Prompt { get; }

        public UserInputChooser(int count, string prompt)
        {
            Count = count;
            Prompt = prompt;
        }
    }

    public class SkillInterface
    {
    }

    public class CharacterInterface
    {
    }

    public class DataSet
    {
        public DataSetInformation? DataSetInformation { get; set; }

        public Dictionary<string, DataSetClass> Classes { get; }  = new Dictionary<string, DataSetClass>();

        private readonly Dictionary<string, DataSetAbility> _keyedAbilities  = new Dictionary<string, DataSetAbility>();
        private readonly Dictionary<string, DataSetAbility> _namedAbilities  = new Dictionary<string, DataSetAbility>();

        public DataSetAbility? GetAbility(string nameOrKey) => _keyedAbilities.GetValueOrDefault(nameOrKey) ??
            _namedAbilities.GetValueOrDefault(nameOrKey);

        public void AddAbility(DataSetAbility ability, string name, string? key = null)
        {
            _namedAbilities.Add(name, ability);
            if (key != null)
                _keyedAbilities.Add(key, ability);
        }

        public void ClearAbility(string name, string? key)
        {
            _namedAbilities.Remove(name);
            if (key != null)
                _keyedAbilities.Remove(key);
        }
    }

    public class DataSetClass
    {
        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableDictionary<string,string> Facts { get; set; } = ImmutableDictionary<string, string>.Empty;
        public string SourcePage { get; set; }
        public Predicate<CharacterInterface> Condition { get; set; }

        public ImmutableList<DataSetVariableDefinition> Definitions { get; set; } =
            ImmutableList<DataSetVariableDefinition>.Empty;

        public ImmutableList<DataSetBonus> Bonuses { get; set; } = ImmutableList<DataSetBonus>.Empty;
        public ImmutableList<string> Types { get; set; } = ImmutableList<string>.Empty;
        public ImmutableList<string> Roles { get; set; } = ImmutableList<string>.Empty;
        public int HitDie { get; set; }
        public int MaxLevel { get; set; }
        public string ExClass { get; set; }
        public ImmutableList<DataSetClassLevel> Levels { get; set; } = ImmutableList<DataSetClassLevel>.Empty;

        public DataSetClass(string name, DataSourceInformation? sourceInfo)
        {
            Name = name;
            SourceInfo = sourceInfo;
        }
    }

    public class DataSetAbility
    {
        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }

        public DataSetAbility(string name, DataSourceInformation? sourceInfo)
        {
            Name = name;
            SourceInfo = sourceInfo;
        }
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