namespace Primordially.PluginCore.Data.Model
{
    public class DataSetSave
    {
        public string Name { get; }
        public string SortKey { get; }
        public DataSetBonus Bonus { get; }

        public DataSetSave(string name, string sortKey, DataSetBonus bonus)
        {
            Name = name;
            SortKey = sortKey;
            Bonus = bonus;
        }
    }
}