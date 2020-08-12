using System;
using System.Collections.Immutable;
using System.Linq;

namespace Primordially.PluginCore.Data
{
    public class Chooser<T>
    {
        private readonly Func<CharacterInterface, T, bool> _filter;

        public Chooser(Func<CharacterInterface, T, bool> filter)
        {
            _filter = filter;
        }

        public ImmutableList<T> Filter(
            CharacterInterface character,
            ImmutableList<T> all)
        {
            return all.Where(s => _filter(character, s)).ToImmutableList();
        }
    }
}