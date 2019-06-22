using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HSNXT.Greed.Unswepper
{
    public static class UnswepperProgram
    {
        private static readonly Regex IncludePruneRegex = new Regex(
            @"(AddCSLuaFile|include)\s*\(\s*(([""'])(shared|cl_init)\.lua\3)?\s*\)", RegexOptions.Compiled
        );

        private static readonly ISet<string> FileWhitelist = new HashSet<string>(new[] {
            "init.lua", "shared.lua", "cl_init.lua"
        });

        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-help" || args[0] == "-h")
            {
                Console.WriteLine("Converts multi-file SWEPs/SENTs into single-file, condensing the number of lua " +
                                  "files by a factor of up to 3 in worst-case scenarios.");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("    dotnet Unswepper.dll <addon root path>");
                return 1;
            }

            var iterationCount = 0;
            var items = Directory.EnumerateDirectories($"{args[0]}/lua_pre/weapons").Select(e => ("weapons", e))
                .Concat(Directory.EnumerateDirectories($"{args[0]}/lua_pre/entities").Select(e => ("entities", e)));

            foreach (var (folder, dir) in items)
            {
                var output = GetMergedSwepFileContents(dir);

                var className = Path.GetFileName(dir);
                File.WriteAllText($"{args[0]}/lua/{folder}/{className}.lua", output, Encoding.UTF8);
                iterationCount++;
            }
            
            Console.WriteLine($"Hooray! Wrote {iterationCount} files.");

            return 0;
        }

        public static string GetMergedSwepFileContents(string dir)
        {
            var init = $"{dir}/init.lua";
            var shared = $"{dir}/shared.lua";
            var clInit = $"{dir}/cl_init.lua";
            
            foreach (var entry in Directory.GetFileSystemEntries(dir))
            {
                if (FileWhitelist.Contains(Path.GetFileName(entry))) continue;

                Console.WriteLine($"Sparse file: {entry}");
                Console.WriteLine($"Files must be one of ({string.Join(", ", FileWhitelist)}).");
                Environment.Exit(2);
            }

            var output = new StringBuilder(
                $"-- This file was automatically generated at {DateTimeOffset.UtcNow}.\n" +
                "-- Changes to it might be erased upon new generation.\n\n"
            );
            var addCsLuaFile = false;

            string initText = null;
            string sharedText = null;
            string clInitText = null;
            // TODO: sh_sounds.lua, sh_soundscript.lua

            if (File.Exists(init))
            {
                initText = File.ReadAllText(init, Encoding.UTF8);
                initText = initText.Replace("\r\n", "\n");

                // god forbid someone do this, but it's here anyway
                if (IncludePruneRegex.IsMatch(initText))
                {
                    addCsLuaFile = true;
                    initText = IncludePruneRegex.Replace(initText, "--[[$&]]");
                }
            }

            if (File.Exists(shared))
            {
                sharedText = File.ReadAllText(shared, Encoding.UTF8);
                sharedText = sharedText.Replace("\r\n", "\n");

                if (IncludePruneRegex.IsMatch(sharedText))
                {
                    addCsLuaFile = true;
                    sharedText = IncludePruneRegex.Replace(sharedText, "--[[$&]]");
                }
            }

            if (File.Exists(clInit))
            {
                clInitText = File.ReadAllText(clInit, Encoding.UTF8);
                clInitText = clInitText.Replace("\r\n", "\n");

                addCsLuaFile = true;
                if (IncludePruneRegex.IsMatch(clInitText))
                {
                    clInitText = IncludePruneRegex.Replace(clInitText, "--[[$&]]");
                }
            }

            if (addCsLuaFile) output.Append("AddCSLuaFile()\n\n");

            if (initText != null)
            {
                output.Append("if SERVER then -- ").Append(init).Append('\n');
                output.AppendFormat("print \"[Unswepper] ServerInit: {0}\"\n", GetParentFilename(init));
                output.Append(initText).Append('\n');
                output.Append("end\n\n");
            }

            if (sharedText != null)
            {
                output.Append("do -- ").Append(shared).Append('\n');
                output.AppendFormat("print \"[Unswepper] Shared: {0}\"\n", GetParentFilename(shared));
                output.Append(sharedText).Append('\n');
                output.Append("end\n\n");
            }

            if (clInitText != null)
            {
                output.Append("if CLIENT then -- ").Append(clInit).Append('\n');
                output.AppendFormat("print \"[Unswepper] ClientInit: {0}\"\n", GetParentFilename(clInit));
                output.Append(clInitText).Append('\n');
                output.Append("end\n\n");
            }

            output.Append("-- End of SWEP");
            return output.ToString();
        }

        private static string GetParentFilename(string file)
        {
            return Path.GetFileName(Path.GetDirectoryName(file));
        }
    }
}