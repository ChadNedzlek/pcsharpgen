using System;
using System.Collections.Immutable;
using System.Linq;
using Primordially.PluginCore.Data.LuaInterfaces;

namespace Primordially.PluginCore.Data.Choosers
{
    public delegate bool ChooserFilter<in T>(CharacterInterface character, T arg);

    public interface IChooser
    {
        public string ChooseType { get; }
    }

    public interface IChooser<T> : IChooser
    {
        IImmutableList<T> Filter(CharacterInterface character, IImmutableList<T> options);
    }

    public class Chooser<T> : IChooser<T>
    {
        private readonly ChooserFilter<T> _filter;

        public Chooser(ChooserFilter<T> filter)
        {
            _filter = filter;
        }

        public IImmutableList<T> Filter(
            CharacterInterface character,
            IImmutableList<T> all)
        {
            return all.Where(s => _filter(character, s)).ToImmutableList();
        }

        public string ChooseType { get; } = typeof(T).Name;
    }

    public class StringChooser : IChooser<string>
    {
        private readonly ImmutableList<string> _strings;

        public StringChooser(ImmutableList<string> strings)
        {
            _strings = strings;
        }

        public IImmutableList<string> Filter(CharacterInterface character, IImmutableList<string> options)
        {
            return _strings;
        }
        
        public string ChooseType { get; } = nameof(String);
    }

    public class Choice
    {
        public int? PerChoice { get; }
        public int? MaxTimes { get; }
        public IChooser Chooser { get; }

        public Choice(IChooser chooser, int? perChoice, int? maxTimes)
        {
            PerChoice = perChoice;
            MaxTimes = maxTimes;
            Chooser = chooser;
        }

        public static Choice? Build(IChooser? chooser, int? perChoice, int? maxTimes)
        {
            if (chooser == null)
                return null;

            Type genericChooserType = chooser
                .GetType()
                .FindInterfaces(
                    (t, _) => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IChooser<>),
                    null
                )
                .FirstOrDefault();
            if (genericChooserType == null)
            {
                throw new ArgumentException("Chooser must be a IChooser<T>");
            }

            var choiceType = typeof(Choice<>).MakeGenericType(genericChooserType.GetGenericArguments());
            return (Choice) Activator.CreateInstance(choiceType, chooser, perChoice, maxTimes)!;
        }
    }

    public class Choice<T> : Choice
    {
        public new IChooser<T> Chooser => (IChooser<T>) base.Chooser;

        public Choice(IChooser chooser, int? perChoice, int? maxTimes)
            : base(chooser, perChoice, maxTimes)
        {
        }
    }
}