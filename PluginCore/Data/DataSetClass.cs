using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Primordially.PluginCore.Data
{
    internal class RepeatingDataSetClassLevel
    {
        public RepeatingDataSetClassLevel(int start, int repeat, DataSetClassLevel info)
        {
            Start = start;
            Repeat = repeat;
            Info = info;
        }

        public int Start { get; }

        public int Repeat { get; }
        public DataSetClassLevel Info { get; }

        public bool IsValidAtLevel(int level)
        {
            if (level == Start)
                return true;
            if (Repeat == 0)
                return false;
            return ((level - Start) % Repeat) == 0;
        }
    }

    public class DataSetClass
    {
        internal DataSetClass(
            string name,
            DataSourceInformation? sourceInfo,
            ImmutableDictionary<string, string> facts,
            string? sourcePage,
            Func<CharacterInterface, bool>? condition,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<string> types,
            ImmutableList<string> roles,
            int? hitDie,
            int? maxLevel,
            string? exClass,
            ImmutableList<RepeatingDataSetClassLevel> levels)
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
            ExClass = exClass;
            _levels = levels;
        }

        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableDictionary<string, string> Facts { get; }
        public string? SourcePage { get; }
        public Func<CharacterInterface, bool>? Condition { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }

        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<string> Types { get; }
        public ImmutableList<string> Roles { get; }
        public int? HitDie { get; }
        public int? MaxLevel { get; }
        public string? ExClass { get; }
        private readonly ImmutableList<RepeatingDataSetClassLevel> _levels;

        public DataSetClass MergedWith(DataSetClass other)
        {
            Func<CharacterInterface, bool>? condition;
            if (other.Condition != null && Condition != null)
            {
                condition = ci => other.Condition(ci) && Condition(ci);
            }
            else
            {
                condition = other.Condition ?? Condition;
            }

            return new DataSetClass(
                other.Name ?? Name,
                other.SourceInfo ?? SourceInfo,
                Facts.AddRange(other.Facts),
                other.SourcePage ?? SourcePage,
                condition,
                Definitions.AddRange(other.Definitions),
                Bonuses.AddRange(other.Bonuses),
                Types.AddRange(other.Types),
                Roles.AddRange(other.Roles),
                other.HitDie ?? HitDie,
                other.MaxLevel ?? MaxLevel,
                other.ExClass ?? ExClass,
                _levels.AddRange(other._levels)
            );
        }
        
        private Lazy<Dictionary<int, DataSetClassLevel>> _levelCache;
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