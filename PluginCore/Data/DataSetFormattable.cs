using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetFormattable
    {
        public DataSetFormattable(string formatString, ImmutableList<string> arguments)
        {
            FormatString = formatString;
            Arguments = arguments;
        }

        public string FormatString { get; }
        public ImmutableList<string> Arguments { get; }
    }
}