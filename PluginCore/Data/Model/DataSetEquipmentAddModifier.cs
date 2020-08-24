using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetEquipmentAddModifierBase
    {
        private protected DataSetEquipmentAddModifierBase(ImmutableList<DataSetFormula> parameters)
        {
            Parameters = parameters;
        }

        public ImmutableList<DataSetFormula> Parameters { get; }
    }

    public class DataSetEquipmentAddModifier : DataSetEquipmentAddModifierBase
    {
        public DataSetEquipmentModifier Modifier { get; }

        public DataSetEquipmentAddModifier(ImmutableList<DataSetFormula> parameters, DataSetEquipmentModifier modifier)
            : base(parameters)
        {
            Modifier = modifier;
        }
    }
}
