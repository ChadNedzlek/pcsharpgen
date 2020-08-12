namespace Primordially.PluginCore.Data
{
    public class DataSetAlignment
    {
        public DataSetAlignment(string name, string abbreviation, string sortKey)
        {
            Name = name;
            Abbreviation = abbreviation;
            SortKey = sortKey;
        }

        public string Name { get; }
        public string Abbreviation { get; }
        public string SortKey { get; }
    }
}