using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetDomain
    {
        public string Name { get; }
        public DataSetFormattable Description { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetSpellList?> SpellLists { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }
        public string SourcePage { get; }
        public ImmutableList<DataSetSkill> ClassSkills { get; }
        public ImmutableList<DataSetAddAbility> GrantAbilities { get; }

        public DataSetDomain(
            string name,
            DataSetFormattable description,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetSpellList?> spellLists,
            DataSetCondition<CharacterInterface> condition,
            string sourcePage,
            ImmutableList<DataSetSkill> classSkills,
            ImmutableList<DataSetAddAbility> grantAbilities)
        {
            Name = name;
            Description = description;
            Definitions = definitions;
            SpellLists = spellLists;
            Condition = condition;
            SourcePage = sourcePage;
            GrantAbilities = grantAbilities;
            ClassSkills = classSkills;
        }
    }
}
