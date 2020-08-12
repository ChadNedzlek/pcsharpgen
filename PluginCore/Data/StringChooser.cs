using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class StringChooser
    {
        public StringChooser(ImmutableList<string> options)
        {
            Options = options;
        }

        public ImmutableList<string> Options { get; }
    }
}