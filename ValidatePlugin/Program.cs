using System;
using System.IO;
using NLua;
using Primordially.PluginCore.Data;
using Splat;

namespace Primordially.ValidatePlugin
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine($"Usage: {Environment.GetCommandLineArgs()[0]} <index.lua>");
                return 1;
            }

            string path = Path.GetFullPath(args[0]);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"'{path}' does not exist");
                return 2;
            }

            Locator.CurrentMutable.RegisterConstant((ILogger) new ConsoleLogger());

            DataSetLoader loader = new DataSetLoader(Path.GetDirectoryName(path)!, DataSetStrictness.Lax);
            var dataSet = loader.LoadData(path);

            Console.WriteLine("Data Set Summary");
            Console.WriteLine("Classes : " + dataSet.Classes.Count);
            Console.WriteLine("Abilities : " + dataSet.Abilities.Count);
            return 0;
        }
    }
}