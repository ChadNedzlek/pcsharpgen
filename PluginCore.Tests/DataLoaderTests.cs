using NUnit.Framework;
using Primordially.PluginCore.Data;

namespace Primordially.PluginCore.Tests
{
    public class DataLoaderTests
    {
        [Test]
        public void Test()
        {
            DataSetLoader loader = new DataSetLoader(@"C:\Users\Chad Nedzlek\source\repos\primordially\Data", DataSetStrictness.Lax);
            loader.LoadData(@"@/core_rulebook/index.lua");
        }
    }
}