using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
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
}