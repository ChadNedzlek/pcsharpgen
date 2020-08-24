namespace Primordially.PluginCore.Data.Model
{
    public readonly struct DataSetChargeRange
    {
        public DataSetChargeRange(int? min, int? max)
        {
            Min = min;
            Max = max;
        }

        public int? Min { get; }
        public int? Max { get; }
    }
}