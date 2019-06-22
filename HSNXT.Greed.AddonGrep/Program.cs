using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HSNXT.Greed.AddonGrep
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine("===");
            Console.WriteLine("AddonGrep");
            Console.WriteLine("===");
            Console.WriteLine();

            if (args.Length == 0 || args[0] == "--help" || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine("Finds files inside GMA files within a Garry's Mod addons folder.");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("    dotnet AddonGrep.dll <addons folder path> [--print-files]");
                Console.WriteLine("Along with any amount of the following:");
                Console.WriteLine("    -F | --match-path <glob>");
                Console.WriteLine("    -R | --regex-path <regex>");
                Console.WriteLine("    -C | --match-content <glob>");
                Console.WriteLine("    -T | --regex-content <regex>");
                Console.WriteLine("If there is not at least one pattern, every file will be printed.");
                Console.WriteLine("Every pattern must match for a file to be printed.");
                Console.WriteLine("");
                Console.WriteLine("Searches are case-insensitive regardless of filesystem, and slashes are normalized.");
                return 2;
            }

            string sourcePath = null;
            var shouldPrintFiles = false;
            
            var pathMatchers = new List<Regex>();
            var contentMatchers = new List<Regex>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Length == 0) continue;

                bool NotPair(out int exitCode)
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine($"All arguments found after the input directory must be in pairs. Last argument: {args[i]}");
                        {
                            exitCode = 3;
                            return true;
                        }
                    }

                    exitCode = 0;
                    return false;
                }

                switch (args[i])
                {
                    case "-P":
                    case "--match-path":
                        if (NotPair(out var exitCode1)) return exitCode1;
                        pathMatchers.Add(Glob(args[++i]));
                        break;
                    case "-R":
                    case "--regex-path":
                        if (NotPair(out var exitCode2)) return exitCode2;
                        pathMatchers.Add(new Regex(args[++i], RegexOptions.Compiled));
                        break;
                    case "-C":
                    case "--match-content":
                        if (NotPair(out var exitCode3)) return exitCode3;
                        contentMatchers.Add(Glob(args[++i]));
                        break;
                    case "-D":
                    case "--regex-content":
                        if (NotPair(out var exitCode4)) return exitCode4;
                        contentMatchers.Add(new Regex(args[++i], RegexOptions.Compiled));
                        break;
                    case "--print-files":
                        shouldPrintFiles = true;
                        break;
                    default:
                        sourcePath = args[i];
                        if (!Directory.Exists(sourcePath))
                        {
                            Console.WriteLine($"No directory exists at path: {sourcePath}");
                            return 4;
                        }
                        break;
                }
            }

            if (sourcePath == null)
            {
                Console.WriteLine("No addons directory path provided");
                return 5;
            }
            
            foreach (var file in Directory.EnumerateFiles(sourcePath).Where(file => file.EndsWith(".gma")))
            {
                if (shouldPrintFiles)
                {
                    Console.WriteLine(file);
                }

                using (var addon = RealtimeAddon.Load(file, true, true))
                {
                    foreach (var addonFile in addon.OpenAddon.Files)
                    {
                        if (CheckMatch(addonFile, pathMatchers, contentMatchers))
                        {
                            Console.WriteLine($"{Path.GetFileName(file)}:{addonFile.Path}");
                        }
                    }
                }
            }

            return 0;
        }

        private static bool CheckMatch(
            ContentFile addonFile, IReadOnlyCollection<Regex> pathMatchers, IReadOnlyCollection<Regex> contentMatchers)
        {
            if (pathMatchers.Count == 0 && contentMatchers.Count == 0)
            {
                return true;
            }
            
            if (pathMatchers.Count > 0)
            {
                var path = addonFile.Path;
                if (pathMatchers.Any(matcher => !matcher.IsMatch(path)))
                {
                    return false;
                }
            }

            if (contentMatchers.Count > 0)
            {
                var content = Encoding.UTF8.GetString(addonFile.Content);
                if (contentMatchers.Any(matcher => !matcher.IsMatch(content)))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly Regex NormalizeDirectorySeparators = new Regex(
            @"[\\/]+",
            RegexOptions.Compiled
        );
        private static Regex Glob(string pattern)
        {
            pattern = NormalizeDirectorySeparators.Replace(pattern, "/");

            pattern = Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".");

            return new Regex(
                $"^{pattern}$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
            );
        }
    }
}