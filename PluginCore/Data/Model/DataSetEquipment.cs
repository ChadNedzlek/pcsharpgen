using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetEquipmentBase
    {
        private protected DataSetEquipmentBase(
            string name,
            int cost,
            int? baseQuantity,
            int? effectiveDamageResistance,
            DataSetEquipmentContainer? contains,
            bool canHaveMods,
            bool modsRequired,
            int? spellBookPageCount,
            DataSetFormula? pagesPerSpell,
            string? size,
            int? usedSlots,
            double? weight,
            int? armorCheckPenalty,
            DataSetAttackDefinition? secondAttack,
            DataSetAttackDefinition? attack,
            string? fumbleRange,
            int? maxDex,
            string? proficiency,
            int? range,
            int? reach,
            int? reachMultiplier,
            int? arcaneSpellFailureChance,
            string? wieldCategory,
            bool? visible,
            ImmutableDictionary<string, string> qualities,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<string> types,
            ImmutableList<DataSetFormattable> specialProperties,
            DataSetFormattable? description)
        {
            Name = name;
            Cost = cost;
            BaseQuantity = baseQuantity;
            EffectiveDamageResistance = effectiveDamageResistance;
            Contains = contains;
            CanHaveMods = canHaveMods;
            ModsRequired = modsRequired;
            SpellBookPageCount = spellBookPageCount;
            PagesPerSpell = pagesPerSpell;
            Size = size;
            UsedSlots = usedSlots;
            Weight = weight;
            ArmorCheckPenalty = armorCheckPenalty;
            SecondAttack = secondAttack;
            Attack = attack;
            FumbleRange = fumbleRange;
            MaxDex = maxDex;
            Proficiency = proficiency;
            Range = range;
            Reach = reach;
            ReachMultiplier = reachMultiplier;
            ArcaneSpellFailureChance = arcaneSpellFailureChance;
            WieldCategory = wieldCategory;
            Visible = visible;
            Qualities = qualities;
            Bonuses = bonuses;
            Types = types;
            SpecialProperties = specialProperties;
            Description = description;
        }

        public string Name { get; }
        public int Cost { get; }
        public int? BaseQuantity { get; }
        public int? EffectiveDamageResistance { get; }
        public DataSetEquipmentContainer? Contains { get; }
        public bool CanHaveMods { get; }
        public bool ModsRequired { get; }
        public int? SpellBookPageCount { get; }
        public DataSetFormula? PagesPerSpell { get; }
        public string? Size { get; }
        public int? UsedSlots { get; }
        public double? Weight { get; }
        public int? ArmorCheckPenalty { get; }
        public DataSetAttackDefinition? SecondAttack { get; }
        public DataSetAttackDefinition? Attack { get; }
        public string? FumbleRange { get; }
        public int? MaxDex { get; }
        public string? Proficiency { get; }
        public int? Range { get; }
        public int? Reach { get; }
        public int? ReachMultiplier { get; }
        public int? ArcaneSpellFailureChance { get; }
        public string? WieldCategory { get; }
        public bool? Visible { get; }
        public ImmutableDictionary<string, string> Qualities { get; }
        public ImmutableList<DataSetBonus> Bonuses { get; }
        public ImmutableList<string> Types { get; }
        public ImmutableList<DataSetFormattable> SpecialProperties { get; }
        public DataSetFormattable? Description { get; }
    }

    public class DataSetEquipment : DataSetEquipmentBase
    {
        public DataSetEquipment? BaseItem { get; }
        public ImmutableList<DataSetEquipmentAddModifier> EquipmentModifiers { get; }

        public DataSetEquipment(
            string name,
            int cost,
            int? baseQuantity,
            int? effectiveDamageResistance,
            DataSetEquipmentContainer? contains,
            bool canHaveMods,
            bool modsRequired,
            int? spellBookPageCount,
            DataSetFormula? pagesPerSpell,
            string? size,
            int? usedSlots,
            double? weight,
            int? armorCheckPenalty,
            DataSetAttackDefinition? secondAttack,
            DataSetAttackDefinition? attack,
            string? fumbleRange,
            int? maxDex,
            string? proficiency,
            int? range,
            int? reach,
            int? reachMultiplier,
            int? arcaneSpellFailureChance,
            string? wieldCategory,
            bool? visible,
            ImmutableDictionary<string, string> qualities,
            ImmutableList<DataSetBonus> bonuses,
            ImmutableList<string> types,
            ImmutableList<DataSetFormattable> specialProperties,
            DataSetFormattable? description,
            ImmutableList<DataSetEquipmentAddModifier> equipmentModifiers,
            DataSetEquipment? baseItem)
            : base(
                name,
                cost,
                baseQuantity,
                effectiveDamageResistance,
                contains,
                canHaveMods,
                modsRequired,
                spellBookPageCount,
                pagesPerSpell,
                size,
                usedSlots,
                weight,
                armorCheckPenalty,
                secondAttack,
                attack,
                fumbleRange,
                maxDex,
                proficiency,
                range,
                reach,
                reachMultiplier,
                arcaneSpellFailureChance,
                wieldCategory,
                visible,
                qualities,
                bonuses,
                types,
                specialProperties,
                description
            )
        {
            EquipmentModifiers = equipmentModifiers;
            BaseItem = baseItem;
        }
    }
}