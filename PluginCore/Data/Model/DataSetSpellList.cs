using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetSpellList
    {
        public string Kind { get; }
        public string Name { get; }
        public ImmutableList<DataSetSpellListLevel> Levels { get; }

        public DataSetSpellList(string kind, string name, ImmutableList<DataSetSpellListLevel> levels)
        {
            Kind = kind;
            Name = name;
            Levels = levels;
        }
    }
}