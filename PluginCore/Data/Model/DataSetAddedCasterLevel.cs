namespace Primordially.PluginCore.Data.Model
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