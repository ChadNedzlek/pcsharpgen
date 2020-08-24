namespace Primordially.PluginCore.Data.Model
{
    public class DataSetVariableDefinition
    {
        public DataSetVariableDefinition(string name, DataSetFormula initialValue)
        {
            Name = name;
            InitialValue = initialValue;
        }

        public string Name { get; }
        public DataSetFormula InitialValue { get; }
    }
}