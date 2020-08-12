namespace Primordially.PluginCore.Data
{
    public class DataSetAddAbility
    {
        public DataSetAddAbility(string category, string nature, string name)
        {
            Category = category;
            Nature = nature;
            Name = name;
        }

        public string Category { get; }
        public string Nature { get; }
        public string Name { get; }
    }
}