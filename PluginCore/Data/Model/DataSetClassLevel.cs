using System.Collections.Immutable;

namespace Primordially.PluginCore.Data.Model
{
    public class DataSetClassLevelBase
    {
        private protected DataSetClassLevelBase(ImmutableList<DataSetAddedCasterLevel> addedCasterLevels)
        {
            AddedCasterLevels = addedCasterLevels;
        }
        
        public ImmutableList<DataSetAddedCasterLevel> AddedCasterLevels { get; }
    }

    public class DataSetClassLevel : DataSetClassLevelBase
    {
        public static DataSetClassLevel Empty { get; } = new DataSetClassLevel(ImmutableList<DataSetAddedCasterLevel>.Empty, ImmutableList<DataSetAddAbility>.Empty);

        public ImmutableList<DataSetAddAbility> GrantedAbilities { get; }

        public DataSetClassLevel(ImmutableList<DataSetAddedCasterLevel> addedCasterLevels, ImmutableList<DataSetAddAbility> grantedAbilities)
            : base(addedCasterLevels)
        {
            GrantedAbilities = grantedAbilities;
        }
    }
}