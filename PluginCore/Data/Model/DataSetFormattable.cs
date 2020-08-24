using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetFormattable
    {
        public DataSetFormattable(string formatString, ImmutableList<DataSetFormula> arguments)
        {
            FormatString = formatString;
            Arguments = arguments;
        }

        public DataSetFormattable(string formatString) : this(formatString, ImmutableList<DataSetFormula>.Empty)
        {
        }

        public string FormatString { get; }
        public ImmutableList<DataSetFormula> Arguments { get; }
    }
}