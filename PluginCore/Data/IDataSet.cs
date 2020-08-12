using System;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public interface IDataSet : IDisposable
    {
        DataSetInformation? DataSetInformation { get; }
        IImmutableDictionary<string, DataSetAbility> Abilities { get; }
        ImmutableDictionary<string, DataSetClass> Classes { get; }
        ImmutableDictionary<string, DataSetAlignment> Alignments { get; }
        ImmutableDictionary<string, DataSetFact> Facts { get; }
        ImmutableDictionary<string, DataSetSave> Saves { get; }
        ImmutableDictionary<string, DataSetAbilityScore> AbilityScores { get; }
        ImmutableDictionary<string, DataSetVariable> Variables { get; }
        ImmutableDictionary<string, DataSetAbilityCategory> AbilityCategories { get; }
    }
}