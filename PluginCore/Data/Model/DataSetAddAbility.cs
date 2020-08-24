namespace Primordially.PluginCore.Data.Model
{
    public class DataSetAddAbilityBase
    {
        public DataSetAddAbilityBase(string category, string nature)
        {
            Category = category;
            Nature = nature;
        }

        public string Category { get; }
        public string Nature { get; }
    }

    public class DataSetAddAbility : DataSetAddAbilityBase
    {
        public DataSetAddAbility(string category, string nature, DataSetAbility ability) : base(category, nature)
        {
            Ability = ability;
        }

        public DataSetAbility Ability { get; }
    }
}