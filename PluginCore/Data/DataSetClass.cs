using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ReactiveUI;

namespace Primordially.PluginCore.Data
{
    public class DataSetClassBase
    {
        internal DataSetClassBase(
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
            int? maxLevel)
        {
            Name = name;
            SourceInfo = sourceInfo;
            Facts = facts;
            SourcePage = sourcePage;
            Condition = condition;
            Definitions = definitions;
            Bonuses = bonuses;
            Types = types;
            Roles = roles;
            HitDie = hitDie;
            MaxLevel = maxLevel;
        }

        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableDictionary<string, string> Facts { get; }
        public string? SourcePage { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }

        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<string> Types { get; }
        public ImmutableList<string> Roles { get; }
        public int? HitDie { get; }
        public int? MaxLevel { get; }
        
    }

    public class DataSetClass : DataSetClassBase
    {
        private readonly ImmutableList<RepeatingDataSetClassLevel> _levels;
        public DataSetClass? ExClass { get; }

        internal DataSetClass(
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
            ImmutableList<RepeatingDataSetClassLevel> levels,
            DataSetClass? exClass)
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
            _levels = levels;
            ExClass = exClass;
        }

        
        private readonly Lazy<Dictionary<int, DataSetClassLevel>> _levelCache = new Lazy<Dictionary<int, DataSetClassLevel>>();
        public DataSetClassLevel GetLevel(int level)
        {
            if (_levelCache.Value.TryGetValue(level, out var cached))
            {
                return cached;
            }

            lock (_levelCache)
            {
                if (_levelCache.Value.TryGetValue(level, out cached))
                    return cached;

                var list = _levels.Where(l => l.IsValidAtLevel(level)).ToList();
                DataSetClassLevel built;
                switch (list.Count)
                {
                    case 0:
                        built = DataSetClassLevel.Empty;
                        break;
                    case 1:
                        built = list[1].Info;
                        break;
                    default:
                    {
                        var casterLevels = list[0].Info.AddedCasterLevels.ToBuilder();
                        var abilities = list[0].Info.GrantedAbilities.ToBuilder();
                        foreach (RepeatingDataSetClassLevel otherLevel in list.Skip(1))
                        {
                            casterLevels.AddRange(otherLevel.Info.AddedCasterLevels);
                            abilities.AddRange(otherLevel.Info.GrantedAbilities);
                        }

                        built = new DataSetClassLevel(casterLevels.ToImmutable(), abilities.ToImmutable());
                    }
                        break;
                }

                _levelCache.Value.Add(level, built);
                return built;
            }
        }
    }
}