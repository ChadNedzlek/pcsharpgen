using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAbility
    {
        public static DataSetAbility? Empty { get; } = new DataSetAbility(
            "",
            null,
            ImmutableList<DataSetBonus>.Empty,
            ImmutableList<DataSetAddAbility>.Empty,
            null,
            null,
            null,
            null,
            ImmutableList<DataSetVariableDefinition>.Empty,
            ImmutableList<DataSetAspect>.Empty,
            ImmutableList<string>.Empty,
            null,
            null,
            null
        );

        public DataSetAbility(
            string name,
            DataSourceInformation? sourceInfo,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<DataSetAddAbility> abilities,
            bool? stackable,
            string? category,
            bool? allowMultiple,
            bool? visible,
            ImmutableList<DataSetVariableDefinition> definitions,
            ImmutableList<DataSetAspect> aspects,
            ImmutableList<string> types,
            int? cost,
            DataSetFormattable? description,
            string? sourcePage)
        {
            Name = name;
            SourceInfo = sourceInfo;
            Bonuses = bonuses;
            Abilities = abilities;
            _stackable = stackable;
            Category = category;
            _allowMultiple = allowMultiple;
            _visible = visible;
            Definitions = definitions;
            Aspects = aspects;
            Types = types;
            _cost = cost;
            Description = description;
            SourcePage = sourcePage;
        }

        public string Name { get; }
        public DataSourceInformation? SourceInfo { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; } = ImmutableList<DataSetBonus>.Empty;
        public ImmutableList<DataSetAddAbility> Abilities { get; } = ImmutableList<DataSetAddAbility>.Empty;

        private readonly bool? _stackable;
        public bool Stackable => _stackable ?? false;

        public string? Category { get; }

        private readonly bool? _allowMultiple;
        public bool AllowMultiple => _allowMultiple ?? false;

        public ImmutableList<DataSetVariableDefinition> Definitions { get; } = ImmutableList<DataSetVariableDefinition>.Empty;
        public ImmutableList<DataSetAspect> Aspects { get; } = ImmutableList<DataSetAspect>.Empty;
        public ImmutableList<string> Types { get; } = ImmutableList<string>.Empty;

        private readonly int? _cost;
        public int Cost => _cost ?? 0;
        public DataSetFormattable? Description { get; }
        public string? SourcePage { get; }

        private bool? _visible;
        public bool Visible => _visible ?? true;

        public DataSetAbility MergedWith(DataSetAbility other)
        {
            return new DataSetAbility(
                other.Name ?? Name,
                SourceInfo ?? other.SourceInfo,
                Bonuses.AddRange(other.Bonuses),
                Abilities.AddRange(other.Abilities),
                other._stackable ?? _stackable,
                other.Category ?? Category,
                other._allowMultiple ?? _allowMultiple,
                other._visible ?? _visible,
                Definitions.AddRange(other.Definitions),
                Aspects.AddRange(other.Aspects),
                Types.AddRange(other.Types),
                other._cost ?? _cost,
                other.Description ?? Description,
                other.SourcePage ?? SourcePage
            );
        }
    }
}