using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetSpellListLevel
    {
        public int Level { get; }
        public ImmutableList<string> Spells { get; }

        public DataSetSpellListLevel(int level, ImmutableList<string> spells)
        {
            Level = level;
            Spells = spells;
        }
    }
}