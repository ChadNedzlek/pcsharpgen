namespace Primordially.PluginCore.Data
{
    public class DataSetLink
    {
        public DataSetLink(string name, string url, string text)
        {
            Name = name;
            Url = url;
            Text = text;
        }

        public string Name { get; }
        public string Url { get; }
        public string Text { get; }
    }
}