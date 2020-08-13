using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetAspect
        : DataSetFormattable
    {
        public DataSetAspect(string name, string formatString, ImmutableList<DataSetFormula> arguments) : base(formatString, arguments)
        {
            Name = name;
        }

        public string Name { get; }
    }
}