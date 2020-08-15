using System;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    internal class NumberChooser : IChooser<int>
    {
        public int? Min { get; }
        public int? Max { get; }
        public int? Increment { get; }
        public string? Title { get; }

        public NumberChooser(int? min, int? max, int? increment, string? title)
        {
            Min = min;
            Max = max;
            Increment = increment;
            Title = title;
        }

        public IImmutableList<int> Filter(CharacterInterface character, IImmutableList<int> options)
        {
            return options;
        }

        public virtual string ChooseType => nameof(Int32);
    }
}