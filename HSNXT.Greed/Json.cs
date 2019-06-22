using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HSNXT.Greed
{
    /// <summary>
    /// Represents the GMA's embedded JSON field.
    /// </summary>
    [DataContract]
    internal class DescriptionJson
    {
        /// <summary>
        /// Gets or sets the description of the addon.
        /// </summary>
        [DataMember(Name = "description")]
        public string Description;

        /// <summary>
        /// Gets or sets the type of the addon.
        /// </summary>
        [DataMember(Name = "type")]
        public string Type;

        /// <summary>
        /// Contains a list of strings, the tags of the addon.
        /// </summary>
        [DataMember(Name = "tags")]
        public List<string> Tags;
    }

    /// <summary>
    /// Represents the addon metadata declaring addon.json file.
    /// </summary>
    [DataContract]
    internal class AddonJson : DescriptionJson
    {
        // Description, Type and Tags is inherited.

        /// <summary>
        /// Gets or sets the title of the addon.
        /// </summary>
        [DataMember(Name = "title")]
        public string Title;

        /// <summary>
        /// Contains a list of string, the ignore patterns of files that should not be compiled.
        /// </summary>
        [DataMember(Name = "ignore")]
        public List<string> Ignore = new List<string>();
    }

    /// <summary>
    /// The exception thrown when the JSON file read/write encounters an error.
    /// </summary>
    [Serializable]
    internal class AddonJsonException : Exception
    {
        public AddonJsonException(string message) : base(message) { }
        public AddonJsonException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Provides methods to parse and create addon metadata JSONs.
    /// </summary>
    public class Json
    {
        /// <summary>
        /// Gets the title from the read JSON.
        /// </summary>
        public string Title { get; }
        /// <summary>
        /// Gets the description from the read JSON.
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// Gets the addon type from the read JSON.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// Gets a list of strings, the ignore patterns of files that should not be compiled.
        /// </summary>
        public List<string> Ignores { get; }
        /// <summary>
        /// Gets a list of strings, the tags of the addon.
        /// </summary>
        public List<string> Tags { get; }

        /// <summary>
        /// Initializes a JSON reader instance, reading the specified file.
        /// </summary>
        /// <param name="infoFile">The addon.json file to read.</param>
        /// <exception cref="AddonJsonException">Errors regarding reading/parsing the JSON.</exception>
        public Json(string infoFile)
        {
            Ignores = new List<string>();
            Tags = new List<string>();
            string fileContents;

            // Try to open the file
            try
            {
                fileContents = File.ReadAllText(infoFile);
            }
            catch (IOException ex)
            {
                throw new AddonJsonException("Couldn't find file", ex);
            }

            // Parse the JSON
            AddonJson tree;
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(fileContents)))
            {
                var jsonFormatter = new DataContractJsonSerializer(typeof(AddonJson));

                try
                {
                    tree = (AddonJson)jsonFormatter.ReadObject(stream);
                }
                catch (SerializationException ex)
                {
                    throw new AddonJsonException("Couldn't parse json", ex);
                }
            }

            // Check the title
            if (string.IsNullOrEmpty(tree.Title))
                throw new AddonJsonException("title is empty!");
            Title = tree.Title;

            // Get the description
            Description = tree.Description;

            // Load the addon type
            if (tree.Type.ToLowerInvariant() == string.Empty || tree.Type.ToLowerInvariant() == null)
                throw new AddonJsonException("type is empty!");
            if (!Greed.Tags.TypeExists(tree.Type.ToLowerInvariant()))
                throw new AddonJsonException("type isn't a supported type!");
            Type = tree.Type.ToLowerInvariant();

            // Parse the tags
            if (tree.Tags.Count > 2)
                throw new AddonJsonException("too many tags - specify 2 only!");
            foreach (var tag in tree.Tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;

                if (!Greed.Tags.TagExists(tag.ToLowerInvariant()))
                    throw new AddonJsonException("tag isn't a supported word!");
                Tags.Add(tag.ToLowerInvariant());
            }

            // Parse the ignores
            if (tree.Ignore != null)
                Ignores.AddRange(tree.Ignore);
        }

        /// <summary>
        /// Parses a description of an addon and extracts Type and Tags if it was an appropriate JSON string.
        /// </summary>
        /// <param name="readDescription">The whole description read from the file.</param>
        /// <param name="type">The type of the addon.</param>
        /// <param name="tags">The tag list of the addon.</param>
        /// <returns>The description part of the readDescription input (if it was JSON) or the whole input.</returns>
        public static string ParseDescription(string readDescription, ref string type, ref List<string> tags)
        {
            var description = readDescription; // By default, the description is the whole we read.
            var newline = Environment.NewLine.Replace("\r", "\\u000d").Replace("\n", "\\u000a");
            var descTempReplace = readDescription.Replace("\\n", newline).Replace("\\t", "\\u0009");

            using (var descStream = new MemoryStream(Encoding.ASCII.GetBytes(descTempReplace)))
            {
                var bytes = new byte[(int)descStream.Length];
                descStream.Read(bytes, 0, (int)descStream.Length);
                descStream.Seek(0, SeekOrigin.Begin);

                var jsonSerializer = new DataContractJsonSerializer(typeof(DescriptionJson));
                try
                {
                    var dJson = (DescriptionJson)jsonSerializer.ReadObject(descStream);

                    description = dJson.Description; // If there's a description in the JSON, make it the returned description
                    type = dJson.Type;
                    tags = new List<string>(dJson.Tags);
                }
                catch (SerializationException)
                {
                    // The description is a plaintext in the file.
                    type = string.Empty;
                    tags = new List<string>();
                }
            }

            return description;
        }
        
        /// <summary>
        /// Creates a JSON string using the properties of the provided Addon.
        /// </summary>
        /// <param name="addon">The addon which metadata is to be used.</param>
        /// <returns>The compiled JSON string.</returns>
        /// <exception cref="AddonJsonException">Errors regarding creating the JSON.</exception>
        public static string BuildDescription(Addon addon)
        {
            var tree = new DescriptionJson {Description = addon.Description};

            // Load the addon type
            if (addon.Type.ToLowerInvariant() == string.Empty || addon.Type.ToLowerInvariant() == null)
                throw new AddonJsonException("type is empty!");
            if (!Greed.Tags.TypeExists(addon.Type.ToLowerInvariant()))
                throw new AddonJsonException("type isn't a supported type!");
            tree.Type = addon.Type.ToLowerInvariant();

            // Parse the tags
            tree.Tags = new List<string>();
            if (addon.Tags.Count > 2)
                throw new AddonJsonException("too many tags - specify 2 only!");
            foreach (var tag in addon.Tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;

                if (!Greed.Tags.TagExists(tag.ToLowerInvariant()))
                    throw new AddonJsonException("tag isn't a supported word!");
                tree.Tags.Add(tag.ToLowerInvariant());
            }

            string strOutput;

            using (var stream = new MemoryStream())
            {
                var jsonFormatter = new DataContractJsonSerializer(typeof(DescriptionJson));

                try
                {
                    jsonFormatter.WriteObject(stream, tree);
                }
                catch (SerializationException ex)
                {
                    throw new AddonJsonException("Couldn't create json", ex);
                }

                stream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                strOutput = Encoding.ASCII.GetString(bytes);
                strOutput = strOutput.Replace("\\u000d", "").Replace("\\u0009", "\\t").Replace("\\u000a", "\\n");
            }

            return strOutput;
        }
    }
}