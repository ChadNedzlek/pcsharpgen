namespace Primordially.PluginCore.Data.Model
{
    public class DataSetModDefinition
    {
        public DataSetModDefinition(string target, string action, DataSetFormula value)
        {
            Target = target;
            Action = action;
            Value = value;
        }

        public string Target { get; }
        public string Action { get; }
        public DataSetFormula Value { get; }
    }
}