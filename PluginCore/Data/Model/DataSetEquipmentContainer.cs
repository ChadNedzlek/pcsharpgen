using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetEquipmentContainer
    {
        public DataSetEquipmentContainer(
            double capacity,
            bool containedItemWeightDoesNotCount,
            double? containedItemWeightModifier,
            ImmutableDictionary<string, int?> itemLimits)
        {
            Capacity = capacity;
            ContainedItemWeightDoesNotCount = containedItemWeightDoesNotCount;
            ContainedItemWeightModifier = containedItemWeightModifier;
            ItemLimits = itemLimits;
        }

        public double Capacity { get; }
        public bool ContainedItemWeightDoesNotCount { get; }
        public double? ContainedItemWeightModifier { get; }
        public ImmutableDictionary<string, int?> ItemLimits { get; }
    }
}