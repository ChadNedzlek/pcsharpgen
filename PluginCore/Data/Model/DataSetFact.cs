namespace Primordially.PluginCore.Data.Model
{
    public class DataSetFact
    {
        public DataSetFact(
            string category,
            bool selectable,
            bool visible,
            bool required,
            string dataFormat,
            string? displayName,
            string? explanation)
        {
            Category = category;
            Selectable = selectable;
            Visible = visible;
            Required = required;
            DataFormat = dataFormat;
            DisplayName = displayName;
            Explanation = explanation;
        }

        public string Category { get; }
        public bool Selectable { get; }
        public bool Visible { get; }
        public bool Required { get; }
        public string DataFormat { get; }
        public string? DisplayName { get; }
        public string? Explanation { get; }
    }
}