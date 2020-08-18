using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAbility
    {
        public string Name { get; }
        public string Key { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string? SourcePage { get; }

        public bool Stackable { get; }
        public bool AllowMultiple { get; }
        public int Cost { get; }
        public bool Visible { get; }
        public string SortKey { get; }

        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetAspect> Aspects { get; }
        public ImmutableList<string> Types { get; }
        public DataSetFormattable Description { get; }
        public DataSetCondition<CharacterInterface> Condition { get; }

        public DataSourceInformation? SourceInfo { get; }
        public Choice? Choice { get;}
        // TemporaryBonuses
        // DamageReduction
        // Movement
        // AutomaticLanguages
        public ImmutableList<DataSetAddAbility> Abilities { get; }

        public DataSetAbility(
            string name,
            string key,
            string displayName,
            string category,
            string sortKey,
            DataSetFormattable description,
            DataSourceInformation? sourceInfo,
            bool stackable,
            bool allowMultiple,
            bool visible,
            int cost,
            string? sourcePage,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            Choice? choice,
            ImmutableList<DataSetAddAbility> abilities,
            DataSetCondition<CharacterInterface> condition)
        {
            Name = name;
            Key = key;
            DisplayName = displayName;
            Category = category;
            SortKey = sortKey;
            Description = description;
            SourceInfo = sourceInfo;
            Stackable = stackable;
            AllowMultiple = allowMultiple;
            Visible = visible;
            Cost = cost;
            SourcePage = sourcePage;
            Bonuses = bonuses;
            Definitions = definitions;
            Aspects = aspects;
            Types = types;
            Choice = choice;
            Abilities = abilities;
            Condition = condition;
        }
    }
}