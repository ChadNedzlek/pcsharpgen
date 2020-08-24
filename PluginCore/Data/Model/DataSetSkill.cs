using System.Collections.Immutable;
using Primordially.PluginCore.Data.LuaInterfaces;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetSkillBase
    {
        public DataSetSkillBase(string name, ImmutableList<string> types, ImmutableList<DataSetBonus> bonuses, string sourcePage, DataSetCondition<CharacterInterface> condition)
        {
            Name = name;
            Types = types;
            Bonuses = bonuses;
            SourcePage = sourcePage;
            Condition = condition;
        }

        public string Name { get; }
        public ImmutableList<string> Types { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public string SourcePage { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }
    }

    public class DataSetSkill : DataSetSkillBase
    {
        public DataSetSkill(
            string name,
            ImmutableList<string> types,
            DataSetAbilityScore keyStat,
            bool useUntrained,
            ImmutableList<DataSetBonus> bonuses,
            string sourcePage,
            DataSetCondition<CharacterInterface> condition)
            : base(name, types, bonuses, sourcePage, condition)
        {
            KeyStat = keyStat;
            UseUntrained = useUntrained;
        }

        public DataSetAbilityScore KeyStat { get; }
        public bool UseUntrained { get; }
    }
}