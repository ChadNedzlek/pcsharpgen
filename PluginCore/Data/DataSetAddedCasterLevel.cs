namespace Primordially.PluginCore.Data
{
    public class DataSetAddedCasterLevel
    {
        public DataSetAddedCasterLevel(string? typeRestriction)
        {
            TypeRestriction = typeRestriction;
        }

        public string? TypeRestriction { get; }
    }
}