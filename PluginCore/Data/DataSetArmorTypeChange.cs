namespace Primordially.PluginCore.Data
{
    public readonly struct DataSetArmorTypeChange
    {
        public DataSetArmorTypeChange(string @from, string to)
        {
            From = @from;
            To = to;
        }

        public string From { get; }
        public string To { get; }
    }
}