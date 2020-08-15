using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public delegate string ModifyName(string original, string? choiceText);

    public class DataSetEquipmentModifierBase
    {
        protected readonly bool? VisibleSet;
        protected readonly bool? AffectsBothHeadsSet;

        private protected DataSetEquipmentModifierBase(
            string? name,
            string key,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<string> types,
            ImmutableList<DataSetFormattable> specialProperties,
            DataSetFormattable? description,
            DataSetFormula? cost,
            ImmutableList<string> grantedItemTypes,
            bool? visible,
            bool? affectsBothHeads,
            Choice? choice,
            DataSetArmorTypeChange? armorTypeChange,
            DataSetChargeRange? charges,
            int? equivalentEnhancementBonus)
        {
            Bonuses = bonuses;
            Key = key;
            Types = types;
            SpecialProperties = specialProperties;
            Description = description;
            NameSet = name;
            Cost = cost;
            GrantedItemTypes = grantedItemTypes;
            VisibleSet = visible;
            AffectsBothHeadsSet = affectsBothHeads;
            Choice = choice;
            ArmorTypeChange = armorTypeChange;
            Charges = charges;
            EquivalentEnhancementBonus = equivalentEnhancementBonus;
        }
        
        protected string? NameSet { get; }
        public string Key { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<string> Types { get; }
        public ImmutableList<DataSetFormattable> SpecialProperties { get; }
        public DataSetFormattable? Description { get; }
        public DataSetFormula? Cost { get; }
        public ImmutableList<string> GrantedItemTypes { get; }
        public bool Visible => VisibleSet ?? true;
        public bool AffectsBothHeads => AffectsBothHeadsSet ?? false;
        public Choice? Choice { get; }
        public DataSetArmorTypeChange? ArmorTypeChange { get; }
        public DataSetChargeRange? Charges { get; }
        public int? EquivalentEnhancementBonus { get;  }
    }

    public class DataSetEquipmentModifier : DataSetEquipmentModifierBase
    {
        public string Name => NameSet!;
        public ModifyName NameModifier { get; }
        public ImmutableList<DataSetEquipment> Replaces { get; }
        public ImmutableList<DataSetEquipment> AutomaticEquipment { get; }

        public DataSetEquipmentModifier(
            string name,
            string key,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<string> types,
            ImmutableList<DataSetFormattable> specialProperties,
            DataSetFormattable? description,
            DataSetFormula? cost,
            ImmutableList<string> grantedItemTypes,
            bool? visible,
            bool? affectsBothHeads,
            ModifyName nameModifier,
            Choice? choice,
            DataSetArmorTypeChange? armorTypeChange,
            DataSetChargeRange? charges,
            int? equivalentEnhancementBonus,
            ImmutableList<DataSetEquipment> replaces,
            ImmutableList<DataSetEquipment> automaticEquipment)
            : base(
                name,
                key,
                bonuses,
                types,
                specialProperties,
                description,
                cost,
                grantedItemTypes,
                visible,
                affectsBothHeads,
                choice,
                armorTypeChange,
                charges,
                equivalentEnhancementBonus
            )
        {
            NameModifier = nameModifier;
            Replaces = replaces;
            AutomaticEquipment = automaticEquipment;
        }
    }
}