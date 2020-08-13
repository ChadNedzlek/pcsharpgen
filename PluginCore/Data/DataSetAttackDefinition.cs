using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAttackDefinitionBase
    {
        private protected DataSetAttackDefinitionBase(string criticalDamageMultiplier, string criticalThreatRange, DataSetDiceFormula? damage, ImmutableList<string> types)
        {
            CriticalDamageMultiplier = criticalDamageMultiplier;
            CriticalThreatRange = criticalThreatRange;
            Damage = damage;
            Types = types;
        }

        public string CriticalDamageMultiplier { get; set; }
        public string CriticalThreatRange { get; set; }
        public DataSetDiceFormula? Damage { get; set; }
        public ImmutableList<string> Types { get; }
    }

    public class DataSetAttackDefinition : DataSetAttackDefinitionBase
    {
        public ImmutableList<DataSetEquipmentAddModifier> EquipmentModifiers { get; }

        public DataSetAttackDefinition(
            string criticalDamageMultiplier,
            string criticalThreatRange,
            DataSetDiceFormula? damage,
            ImmutableList<string> types,
            ImmutableList<DataSetEquipmentAddModifier> equipmentModifiers) : base(
            criticalDamageMultiplier,
            criticalThreatRange,
            damage,
            types
        )
        {
            EquipmentModifiers = equipmentModifiers;
        }
    }
}