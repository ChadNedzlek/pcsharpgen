using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using NLua;

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
            private DataSourceInformation? _sourceInfo = null;

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

            #region Abilities

            [LuaGlobal]
            public void DefineAbility(LuaTable table)
            {
                string abilityKey = GetAbilityKey(table);
                if (_dataSet.Abilities.ContainsKey(abilityKey))
                {
                    switch (_dataSetLoader._strictness)
                    {
                        case DataSetStrictness.Strict:
                            throw new ArgumentException(
                                $"Ability with DefineAbility with key '{abilityKey}' is called more than once"
                            );
                        case DataSetStrictness.Lax:
                            _dataSet.Abilities.Remove(abilityKey);
                            break;
                    }
                }

                var ability = new DataSetAbility((string) table["Name"]);
                foreach (KeyValuePair<object, object> pair in table)
                {
                    string key = (string) pair.Key;
                    switch (key)
                    {
                        case "Name":
                        case "Key":
                            // only used for keying
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

            private string GetAbilityKey(LuaTable table)
            {
                return (string) (table["Key"] ?? table["Name"]);
            }

            [LuaGlobal]
            public void ModifyAbility(LuaTable table)
            {
            }

            #endregion

            [LuaGlobal]
            public Func<CharacterInterface, SkillInterface, bool> ChooseSkill(object choose)
            {
                if (choose is LuaFunction func)
                {
                    return (c, s) => SingleCall<bool>(func, c, s);
                }

                if (choose is LuaTable table)
                {
                    return (c, s) => table.Values.Cast<LuaFunction>().All(f => SingleCall<bool>(f, c, s));
                }

                throw new ArgumentException("ChooseSkill is incorrectly defined");
            }

            public class SkillInterface
            {
            }

            public class CharacterInterface
            {
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

            private static T SingleCall<T>(LuaFunction func, params  object[] args)
            {
                var result = func.Call(args);
                if (result.Length != 1)
                    throw new ArgumentException("Lua function returned multiple (or no) arguments");

                return (T) result[0];
            }
        }
    }

    public class DataSet
    {
        public DataSetInformation? DataSetInformation { get; set; }
        public Dictionary<string, DataSetAbility> Abilities { get; } = new Dictionary<string, DataSetAbility>();
    }

    public class DataSetAbility
    {
        public string Name { get; }

        public DataSetAbility(string name)
        {
            Name = name;
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