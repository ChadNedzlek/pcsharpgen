using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Primordially.PluginCore.Data.Model;

namespace Primordially.PluginCore.Data
{
    public interface IDataContainer : IDisposable
    {
        IReadOnlyDictionary<string, DataSetAbility> Abilities { get; }
        IReadOnlyDictionary<string, DataSetClass> Classes { get; }
        IReadOnlyDictionary<string, DataSetAlignment> Alignments { get; }
        IReadOnlyDictionary<string, DataSetFact> Facts { get; }
        IReadOnlyDictionary<string, DataSetSave> Saves { get; }
        IReadOnlyDictionary<string, DataSetAbilityScore> AbilityScores { get; }
        IReadOnlyDictionary<string, DataSetVariable> Variables { get; }
        IReadOnlyDictionary<string, DataSetAbilityCategory> AbilityCategories { get; }
        IReadOnlyDictionary<string, DataSetDomain> Domains { get; }
        IReadOnlyDictionary<string, DataSetEquipmentModifier> EquipmentModifiers { get; }
        IReadOnlyDictionary<string, DataSetEquipment> Equipment { get; }
    }

    public interface IDataSet : IDataContainer
    {
        DataSetInformation? DataSetInformation { get; }
    }
}