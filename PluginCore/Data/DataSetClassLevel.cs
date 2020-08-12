using System;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetClassLevel
    {
        public static DataSetClassLevel Empty { get; } = new DataSetClassLevel(ImmutableList<DataSetAddedCasterLevel>.Empty, ImmutableList<DataSetAddAbility>.Empty);


        public DataSetClassLevel(
            ImmutableList<DataSetAddedCasterLevel> addedCasterLevels,
            ImmutableList<DataSetAddAbility> grantedAbilities)
        {
            AddedCasterLevels = addedCasterLevels;
            GrantedAbilities = grantedAbilities;
        }

        public ImmutableList<DataSetAddedCasterLevel> AddedCasterLevels { get; }
        public ImmutableList<DataSetAddAbility> GrantedAbilities { get; }

        public DataSetClassLevel MergedWith(DataSetClassLevel other)
        {
            return new DataSetClassLevel(
                AddedCasterLevels.AddRange(other.AddedCasterLevels),
                GrantedAbilities.AddRange(other.GrantedAbilities)
            );
        }
    }
}