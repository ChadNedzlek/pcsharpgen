namespace Primordially.PluginCore.Data
{
    public class DataSetVariable
    {
        public string Name { get; }
        public string Type { get; }
        public string? Channel { get; }
        public string? Scope { get; }

        public DataSetVariable(string name, string type, string? channel, string? scope)
        {
            Name = name;
            Type = type;
            Channel = channel;
            Scope = scope;
        }
    }
}