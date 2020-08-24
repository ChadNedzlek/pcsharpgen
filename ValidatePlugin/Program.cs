using System;
using System.Diagnostics;
using System.IO;
using Mono.Options;
using Primordially.PluginCore.Data;
using Splat;

namespace Primordially.ValidatePlugin
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            string? rootPath = null;
            OptionSet options = new OptionSet
            {
                {"root-path|root|r=", "Root path of character builder", v => rootPath = v},
            };

            var remaining = options.Parse(args);
            if (rootPath == null)
            {
                Console.Error.WriteLine($"Usage: {Environment.GetCommandLineArgs()[0]} --root-path <root-path> <index.lua>");
                return 1;
            }

            if (remaining.Count != 1)
            {
                Console.Error.WriteLine($"Usage: {Environment.GetCommandLineArgs()[0]} --root-path <root-path> <index.lua>");
                return 1;
            }

            string path = Path.GetFullPath(Path.Combine(rootPath, remaining[0]));
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"'{path}' does not exist");
                return 2;
            }

            Locator.CurrentMutable.RegisterConstant((ILogger) new ConsoleLogger());

            Stopwatch watch = Stopwatch.StartNew();
            DataModuleLoader loader = new DataModuleLoader(rootPath, DataSetStrictness.Lax);
            var dataSet = loader.LoadData(path);
            watch.Stop();

            Console.WriteLine($"Load time : {watch.Elapsed}");

            Console.WriteLine("Data Set Summary");
            Console.WriteLine("Classes : " + dataSet.Classes.Count);
            Console.WriteLine("Abilities : " + dataSet.Abilities.Count);
            return 0;
        }
    }
}