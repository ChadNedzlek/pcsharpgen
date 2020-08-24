using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetAbilityCategory
    {
        public DataSetAbilityCategory(
            string name,
            string category,
            string plural,
            string? displayLocation,
            bool visible,
            bool editable,
            bool editPool,
            bool fractionalPool,
            string? pool,
            string? abilityList,
            ImmutableList<string> types)
        {
            Name = name;
            Category = category;
            Plural = plural;
            DisplayLocation = displayLocation;
            Visible = visible;
            Editable = editable;
            EditPool = editPool;
            FractionalPool = fractionalPool;
            Pool = pool;
            AbilityList = abilityList;
            Types = types;
        }

        public string Name { get; }
        public string Category { get; }
        public string Plural { get; }
        public string? DisplayLocation { get; }
        public bool Visible { get; }
        public bool Editable { get; }
        public bool EditPool { get; }
        public bool FractionalPool { get; }
        public string? Pool { get; }
        public string? AbilityList { get; }
        public ImmutableList<string> Types { get; }
    }
}