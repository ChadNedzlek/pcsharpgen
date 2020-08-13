namespace Primordially.PluginCore.Data
{
    public delegate bool DataSetCondition<in T>(T arg);

    public static class DataSetConditions<T>
    {
        public static DataSetCondition<T> Empty { get; } = _ => true;
    }

    public static class DataSetConditions
    {
        public static DataSetCondition<T> CombineWith<T>(this DataSetCondition<T> left, DataSetCondition<T> right)
        {
            if (left == DataSetConditions<T>.Empty)
            {
                return right;
            }

            if (right == DataSetConditions<T>.Empty)
            {
                return left;
            }

            return a => left(a) && right(a);
        }
    }
}