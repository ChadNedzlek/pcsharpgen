using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAbilityBase
    {
        public DataSetAbilityBase(
            string name,
            string? key,
            string? displayName,
            string? category,
            DataSetFormattable? description,
            DataSourceInformation? sourceInfo,
            bool? stackable,
            bool? allowMultiple,
            bool? visible,
            int? cost,
            string? sourcePage,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            Choice? choice,
            DataSetCondition<CharacterInterface> condition)
        {
            Name = name;
            Key = key;
            SourceInfo = sourceInfo;
            Bonuses = bonuses;
            StackableSet = stackable;
            Category = category;
            AllowMultipleSet = allowMultiple;
            VisibleSet = visible;
            Definitions = definitions;
            Aspects = aspects;
            Types = types;
            CostSet = cost;
            Description = description;
            SourcePage = sourcePage;
            Choice = choice;
            Condition = condition;
            DisplayName = displayName;
        }


        protected readonly bool? StackableSet;
        protected readonly bool? AllowMultipleSet;
        protected readonly bool? VisibleSet;
        protected readonly int? CostSet;
        public string Name { get; }
        public string? Key { get; }
        public string? DisplayName { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public string? Category { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetAspect> Aspects { get; }
        public ImmutableList<string> Types { get; }
        public DataSetFormattable? Description { get; }
        public string? SourcePage { get; }
        public Choice? Choice { get;}
        public DataSetCondition<CharacterInterface> Condition { get; }

        public bool Stackable => StackableSet ?? false;
        public bool AllowMultiple => AllowMultipleSet ?? false;
        public int Cost => CostSet ?? 0;
        public bool Visible => VisibleSet ?? true;
    }

    public class DataSetAbility : DataSetAbilityBase
    {
        public ImmutableList<DataSetAddAbility> Abilities { get; }

        public DataSetAbility(
            string name,
            string? key,
            string? displayName,
            string? category,
            DataSetFormattable? description,
            DataSourceInformation? sourceInfo,
            bool? stackable,
            bool? allowMultiple,
            bool? visible,
            int? cost,
            string? sourcePage,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            Choice? choice,
            ImmutableList<DataSetAddAbility> abilities,
            DataSetCondition<CharacterInterface> condition)
            : base(
                name,
                key,
                displayName,
                category,
                description,
                sourceInfo,
                stackable,
                allowMultiple,
                visible,
                cost,
                sourcePage,
                bonuses,
                definitions,
                aspects,
                types,
                choice,
                condition
            )
        {
            Abilities = abilities;
        }
    }
}