﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HSNXT.Greed
{
    /// <summary>
    /// Represents an error with files checking against the whitelist.
    /// </summary>
    [Serializable]
    internal class WhitelistException : Exception
    {
        public WhitelistException(string message) : base(message) { }
    }

    /// <summary>
    /// Provides methods to use the internal global whitelist.
    /// </summary>
    internal static class Whitelist
    {
        /// <summary>
        /// Gets or sets whether the whitelist has been overridden to support whitelist non-conforming addons.
        /// </summary>
        public static bool Override = false;

        /// <summary>
        /// A list of string patterns of allowed files.
        /// </summary>
        private static readonly string[] Wildcard = {
			"lua/*.lua",
			"scenes/*.vcd",
			"particles/*.pcf",
			"resource/fonts/*.ttf",
			"scripts/vehicles/*.txt",
			"resource/localization/*/*.properties",
			"maps/*.bsp",
			"maps/*.nav",
			"maps/*.ain",
			"maps/thumb/*.png",
			"sound/*.wav",
			"sound/*.mp3",
			"sound/*.ogg",
			"materials/*.vmt",
			"materials/*.vtf",
			"materials/*.png",
			"materials/*.jpg",
			"materials/*.jpeg",
			"models/*.mdl",
			"models/*.vtx",
			"models/*.phy",
			"models/*.ani",
			"models/*.vvd",
			"gamemodes/*/*.txt",
			"gamemodes/*/*.fgd",
			"gamemodes/*/logo.png",
			"gamemodes/*/icon24.png",
			"gamemodes/*/gamemode/*.lua",
			"gamemodes/*/entities/effects/*.lua",
			"gamemodes/*/entities/weapons/*.lua",
			"gamemodes/*/entities/entities/*.lua",
			"gamemodes/*/backgrounds/*.png",
			"gamemodes/*/backgrounds/*.jpg",
			"gamemodes/*/backgrounds/*.jpeg",
			"gamemodes/*/content/models/*.mdl",
			"gamemodes/*/content/models/*.vtx",
			"gamemodes/*/content/models/*.phy",
			"gamemodes/*/content/models/*.ani",
			"gamemodes/*/content/models/*.vvd",
			"gamemodes/*/content/materials/*.vmt",
			"gamemodes/*/content/materials/*.vtf",
			"gamemodes/*/content/materials/*.png",
			"gamemodes/*/content/materials/*.jpg",
			"gamemodes/*/content/materials/*.jpeg",
			"gamemodes/*/content/scenes/*.vcd",
			"gamemodes/*/content/particles/*.pcf",
			"gamemodes/*/content/resource/fonts/*.ttf",
			"gamemodes/*/content/scripts/vehicles/*.txt",
			"gamemodes/*/content/resource/localization/*/*.properties",
			"gamemodes/*/content/maps/*.bsp",
			"gamemodes/*/content/maps/*.nav",
			"gamemodes/*/content/maps/*.ain",
			"gamemodes/*/content/maps/thumb/*.png",
			"gamemodes/*/content/sound/*.wav",
			"gamemodes/*/content/sound/*.mp3",
			"gamemodes/*/content/sound/*.ogg",
			null
        };

        /// <summary>
        /// Contains a list of whitelist patterns grouped by file type.
        /// </summary>
        private static readonly Dictionary<string, string[]> InternalWildcardFileTypes = new Dictionary<string, string[]>();
        /// <summary>
        /// Get a list of known whitelist patterns grouped by file type.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> WildcardFileTypes => InternalWildcardFileTypes;

        /// <summary>
        /// Get a list of file extension - filetype maps known.
        /// </summary>
        public static Dictionary<string, string> FileTypes = new Dictionary<string, string>();

        /// <summary>
        /// Static constructor
        /// </summary>
        static Whitelist()
        {
            // Initialize the known file types into the internal dictionary.
            InternalWildcardFileTypes.Add("Map files", new[] { "*.bsp", "*.png", "*.nav", "*.ain", "*.fgd" });
            InternalWildcardFileTypes.Add("Lua scripts", new[] { "*.lua" });
            InternalWildcardFileTypes.Add("Materials", new[] { "*.vmt", "*.vtf", "*.png" });
            InternalWildcardFileTypes.Add("Models", new[] { "*.mdl", "*.vtx", "*.phy", "*.ani", "*.vvd" });
            InternalWildcardFileTypes.Add("Text files", new[] { "*.txt" });
            InternalWildcardFileTypes.Add("Fonts", new[] { "*.ttf" });
            InternalWildcardFileTypes.Add("Images", new[] { "*.png", "*.jpg", "*.jpeg" });
            InternalWildcardFileTypes.Add("Scenes", new[] { "*.vcd" });
            InternalWildcardFileTypes.Add("Particle effects", new[] { "*.pcf" });
            InternalWildcardFileTypes.Add("Localization properties", new[] { "*.properties" });
            InternalWildcardFileTypes.Add("Sounds", new[] { "*.wav", "*.mp3", "*.ogg" });

            // Map files
            FileTypes.Add("bsp", "Source Map file");
            FileTypes.Add("nav", "Navigation mesh");
            FileTypes.Add("ain", "AI node-graph");
            FileTypes.Add("fgd", "Hammer game definitions");

            // Lua scripts
            FileTypes.Add("lua", "Lua script");

            // Materials
            FileTypes.Add("vmt", "Material file");
            FileTypes.Add("vtf", "Texture file");

            // Models
            FileTypes.Add("mdl", "Model");
            FileTypes.Add("vtx", "Hardware-specific material compilation");
            FileTypes.Add("phy", "Model physics");
            FileTypes.Add("ani", "Model animations");
            FileTypes.Add("vvd", "Model vertex data");

            // Text files
            FileTypes.Add("txt", "Text document");

            // Fonts
            FileTypes.Add("ttf", "True-Type font");

            // Images
            FileTypes.Add("png", "Portable Network Graphics image");
            FileTypes.Add("jpg", "JPEG image");
            FileTypes.Add("jpeg", "JPEG image");

            // Scenes
            FileTypes.Add("vcd", "Choreography data");

            // Particle effects
            FileTypes.Add("pcf", "Particle effect");

            // Localization properties
            FileTypes.Add("properties", "Localization property");

            // Sounds
            FileTypes.Add("wav", "Waveform sound");
            FileTypes.Add("mp3", "MP3 music");
            FileTypes.Add("ogg", "OGG Vorbis audio");
        }

        /// <summary>
        /// Check a path against the internal whitelist determining whether it's allowed or not.
        /// </summary>
        /// <param name="path">The relative path of the filename to determine.</param>
        /// <param name="honourOverride">Whether the checking call should honour Whitelist.Override.
        /// Defaults to true. If set to false, Check will forcibly check against the whitelist.</param>
        /// <returns>True if the file is allowed, false if not.</returns>
        public static bool Check(string path, bool honourOverride = true)
        {
            return honourOverride && Override || Wildcard.Any(wildcard => Check(wildcard, path));
        }

        /// <summary>
        /// Check a path against the specified wildcard determining whether it's allowed or not.
        /// </summary>
        /// <param name="wildcard">The wildcard to check against. (e.g.: files/*.file)</param>
        /// <param name="input">The path to check.</param>
        /// <returns>True if there is match, false if not.</returns>
        public static bool Check(string wildcard, string input)
        {
            if (wildcard == null)
                return false;

            var pattern = $"^{Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".")}$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return regex.IsMatch(input);
        }

        /// <summary>
        /// Check a path against the internal whitelist and returns the first matching substring.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>The matching substring or String.Empty if there was no match.</returns>
        public static string GetMatchingString(string path)
        {
            var match = string.Empty;

            foreach (var wildcard in Wildcard)
            {
                if (match != string.Empty)
                    break;
                if (wildcard == null || wildcard == string.Empty)
                    break;

                match = GetMatchingString(wildcard, path);
            }

            return match;
        }

        /// <summary>
        /// Check a path against the specified wildcard and returns the matching substring.
        /// </summary>
        /// <param name="wildcard">The wildcard to check against (e.g.: files/*.file)</param>
        /// <param name="path">The path to check.</param>
        /// <returns>The matching substring or String.Empty if there was no match.</returns>
        public static string GetMatchingString(string wildcard, string path)
        {
            var pattern = Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".");
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(path);

            if (match.Success == false)
                return string.Empty;
            return match.Value;
        }
    }
}
