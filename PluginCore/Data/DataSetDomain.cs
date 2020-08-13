using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetDomainBase
    {
        public string Name { get; }
        public string Description { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetSpellList?> SpellLists { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }
        public ImmutableList<string> ClassSkills { get; }
        public string SourcePage { get; }

        public DataSetDomainBase(
            string name,
            string description,
            ImmutableList<DataSetVariableDefinition> definitions, ImmutableList<DataSetSpellList?> spellLists,
            DataSetCondition<CharacterInterface> condition,
            ImmutableList<string> classSkills,
            string sourcePage)
        {
            Name = name;
            Description = description;
            Definitions = definitions;
            SpellLists = spellLists;
            Condition = condition;
            ClassSkills = classSkills;
            SourcePage = sourcePage;
        }
    }

    public class DataSetDomain : DataSetDomainBase
    {
        public ImmutableList<DataSetAddAbility> GrantAbilities { get; }

        public DataSetDomain(
            string name,
            string description,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetSpellList?> spellLists,
            DataSetCondition<CharacterInterface> condition,
            ImmutableList<string> classSkills,
            string sourcePage,
            ImmutableList<DataSetAddAbility> grantAbilities)
            : base(name, description, definitions, spellLists, condition, classSkills, sourcePage)
        {
            GrantAbilities = grantAbilities;
        }
    }
}
