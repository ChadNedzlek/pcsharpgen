namespace Primordially.PluginCore.Data
{
    public class DataSetPublisherInformation
    {
        public DataSetPublisherInformation(string shortName, string longName, string url)
        {
            ShortName = shortName;
            LongName = longName;
            Url = url;
        }

        public string ShortName { get; }
        public string LongName { get; }
        public string Url { get; }
    }
}