namespace Primordially.PluginCore.Data
{
    internal class RepeatingDataSetClassLevelBase
    {
        private protected RepeatingDataSetClassLevelBase(int start, int repeat)
        {
            Start = start;
            Repeat = repeat;
        }

        public int Start { get; }
        public int Repeat { get; }
    }

    internal class RepeatingDataSetClassLevel : RepeatingDataSetClassLevelBase
    {
        public DataSetClassLevel Info { get; }

        internal RepeatingDataSetClassLevel(int start, int repeat, DataSetClassLevel info)
            : base(start, repeat)
        {
            Info = info;
        }

        public bool IsValidAtLevel(int level)
        {
            if (level == Start)
                return true;
            if (Repeat == 0)
                return false;
            return ((level - Start) % Repeat) == 0;
        }
    }
}