using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HSNXT.Greed.ClientSideLuaSeeker
{
    internal static class Program
    {
        // forgot how i got these styled dumps :P
        private static readonly Regex Pattern1 = new Regex(
            @"^(?<path>.+) \([0-9]+(\.[0-9]+)? K?B\)\r?$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant
        );
        
        private static readonly Regex ConsoleLogPattern = new Regex(
            @"^Couldn't add network string \[(?<path>.+)\] - overflow\?\r?$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant
        );
        
        private static int Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            if (args.Length == 0 || args[0] == "--help" || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine("Finds the GMA files associated to Lua files in a specifically-formatted, line-separated list.");
                Console.WriteLine("Files are expected to be UTF-8, no BOM, as GMod's console log appears to use it.");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("    dotnet ClientSideLuaSeeker.dll <addons folder path> [...paths to text files]");
                return 1;
            }

            if (args.Length == 1)
            {
                Console.WriteLine("Please provide at least one text file to crawl through, or pass --help for help.");
                return 2;
            }

            var addonsFolder = args[0];
            var dumpPaths = args.Skip(1);

            var paths = new HashSet<string>();
            var duplicates = 0;
            var i = 1;

            foreach (var path in dumpPaths)
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                foreach (var match in Pattern1.Matches(text).Concat(ConsoleLogPattern.Matches(text)))
                {
                    if (!paths.Add(match.Groups["path"].Value))
                    {
                        duplicates++;
                    }

                    RewriteLine($"Match {i++} of {args.Length - 1}, total {paths.Count} uniq, {duplicates} dup");
                }
            }
            
            RewriteLine("");
            
            Console.WriteLine($"In {args.Length - 1} text files");
            Console.WriteLine($"{paths.Count + duplicates} total, {paths.Count} uniq, {duplicates} dup");
            Console.WriteLine("Beginning dig.");
            Console.WriteLine();
            
            var countsPerAddon = new Dictionary<string, ulong>();

            foreach (var file in Directory.EnumerateFiles(addonsFolder).Where(file => file.EndsWith(".gma")))
            {
                using (var addon = RealtimeAddon.Load(file, true, true))
                {
                    var fileName = Path.GetFileName(file);
                    var count = 0UL;
                    foreach (var addonFile in addon.OpenAddon.Files)
                    {
                        if (!paths.Contains(addonFile.Path)) continue;

                        Console.WriteLine($"{fileName}:{addonFile.Path}");
                        count++;
                    }
                    
                    var key = $"{addon.OpenAddon.Title} ({fileName})";
                    countsPerAddon[key] = count;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished dig.");
            var counts = countsPerAddon.Select(e => (double) e.Value).ToArray();
            Console.WriteLine($"Mean amount of lua files/addon: {MathHelpers.Mean(counts):0.##}");
            Console.WriteLine($"StdDev: {MathHelpers.StdDev(counts):0.##}");
            Console.WriteLine($"Median: {MathHelpers.Median(counts)}");
            Console.WriteLine("Individual counts of lua files (most to least):");
            
            const bool printEmpties = false;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable HeuristicUnreachableCode RedundantIfElseBlock
#pragma warning disable 162
            if (printEmpties)
            {
                foreach (var (key, value) in countsPerAddon.OrderByDescending(e => e.Value))
                {
                    Console.WriteLine(key + ": " + value);
                }
            }
            else
            {
                foreach (var (key, value) in countsPerAddon.OrderByDescending(e => e.Value))
                {
                    if (value > 0) Console.WriteLine(key + ": " + value);
                }
            }
#pragma warning restore 162
            // ReSharper restore HeuristicUnreachableCode RedundantIfElseBlock

            return 0;
        }
        
        // https://stackoverflow.com/a/8946847 (adapted)
        public static void RewriteLine(string str)
        {
            Console.CursorLeft = 0;
            Console.Write(str);
            if (Console.WindowWidth - str.Length > 1)
                Console.Write(new string(' ', Console.WindowWidth - str.Length - 1));
        }
    }
}