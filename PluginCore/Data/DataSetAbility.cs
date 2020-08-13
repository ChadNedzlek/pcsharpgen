using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAbilityBase
    {
        public DataSetAbilityBase(
            string name,
            string? key,
            DataSourceInformation? sourceInfo,
            ImmutableList<DataSetBonus> bonuses,
            bool? stackable,
            string? category,
            bool? allowMultiple,
            bool? visible,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            int? cost,
            DataSetFormattable? description,
            string? sourcePage,
            Choice? choice)
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
        }


        protected readonly bool? StackableSet;
        protected readonly bool? AllowMultipleSet;
        protected readonly bool? VisibleSet;
        protected readonly int? CostSet;
        public string Name { get; }
        public string? Key { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public string? Category { get; }
        public ImmutableList<DataSetVariableDefinition> Definitions { get; }
        public ImmutableList<DataSetAspect> Aspects { get; }
        public ImmutableList<string> Types { get; }
        public DataSetFormattable? Description { get; }
        public string? SourcePage { get; }
        public Choice? Choice { get;}
        
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
            DataSourceInformation? sourceInfo,
            ImmutableList<DataSetBonus> bonuses,
            bool? stackable,
            string? category,
            bool? allowMultiple,
            bool? visible,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            int? cost,
            DataSetFormattable? description,
            string? sourcePage,
            Choice? choice,
            ImmutableList<DataSetAddAbility> abilities)
            : base(
                name,
                key,
                sourceInfo,
                bonuses,
                stackable,
                category,
                allowMultiple,
                visible,
                definitions,
                aspects,
                types,
                cost,
                description,
                sourcePage,
                choice
            )
        {
            Abilities = abilities;
        }
    }
}