﻿using System;
using System.Collections.Generic;
using System.Cryptography;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using SysPath = System.IO.Path;

namespace HSNXT.Greed
{
    /// <summary>
    /// Represents an error with files checking against ignore list.
    /// </summary>
    [Serializable]
    internal class IgnoredException : Exception
    {
        public IgnoredException() { }
        public IgnoredException(string message) : base(message) { }
        public IgnoredException(string message, Exception inner) : base(message, inner) { }
        protected IgnoredException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Represents an addon file in the program's memory and provides methods to manipulate an addon.
    /// </summary>
    public class Addon
    {
        /// <summary>
        /// The identation (first few characters) of GMA files.
        /// </summary>
        public const string Magic = "GMAD";
        /// <summary>
        /// The version byte for GMA files.
        /// </summary>
        public const char Version = (char)3;
        /* (Currently not used)
        /// <summary>
        /// The AppID of Garry's Mod on Steam.
        /// </summary>
        public const uint AppID = 4000;*/
        /* (Currently not used.)
        /// <summary>
        /// Some sort of checksum/signature entry.
        /// </summary>
        public const uint CompressionSignature = 0xBEEFCACE;*/
        
        /* (Currently not used.)
        /// <summary>
        /// Represents the header of a GMA file.
        /// </summary>
        private struct Header
        {
            /// <summary>
            /// The identation (first few characters).
            /// </summary>
            public string Ident;
            /// <summary>
            /// The version byte.
            /// </summary>
            public char Version;

            /// <summary>
            /// Creates a new header using the identation and version specified.
            /// </summary>
            /// <param name="ident">The identation (first few characters)</param>
            /// <param name="version">The version byte.</param>
            public Header(string ident, char version)
            {
                this.Ident = ident;
                this.Version = version;
            }
        }*/

        /* (Current not used.)
        /// <summary>
        /// This is the position in the file containing a 64 bit unsigned int that represents the file's age
        /// It's basically the time it was uploaded to Steam - and is set on download/extraction from steam.
        /// </summary>
        public static uint TimestampOffset = (uint)System.Runtime.InteropServices.Marshal.SizeOf(new Header(Ident, Version))
            + (uint)sizeof(ulong);*/
        
        /// <summary>
        /// Gets or sets the title of the addon.
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Gets the format version byte of the addon (from header).
        /// </summary>
        public char FormatVersion { get; }
        /*/// <summary>
        /// Gets or sets the author of the addon.
        /// Currently it has no use, as the author is always written as "Author Name".
        /// </summary>
        public string Author; // Not used. Current writer only writes "Author Name"*/
        /// <summary>
        /// Gets or sets the description of the addon.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Gets a compiled JSON string of the addon's metadata.
        /// </summary>
        public string DescriptionJson => Json.BuildDescription(this);

        /// <summary>
        /// Gets or sets the type of the addon.
        /// </summary>
        public string Type { get; set; }
        /* Not used.
        /// <summary>
        /// Gets or sets the Steam ID of the creator.
        /// </summary>
        public ulong SteamID;*/
        /// <summary>
        /// Gets the creation time of the addon.
        /// </summary>
        public DateTime Timestamp { get; }
        /* Not used.
        /// <summary>
        /// Gets or sets the version of the addon.
        /// </summary>
        public int AddonVersion;*/
        /// <summary>
        /// Contains a list of file contents of the addon.
        /// </summary>
        public List<ContentFile> Files { get; }
        /// <summary>
        /// Gets or sets a list of addon tags.
        /// </summary>
        public List<string> Tags { get; set; }
        /// <summary>
        /// Gets or sets a list of addon ignore patterns.
        /// </summary>
        public List<string> Ignores { get; }

        /// <summary>
        /// Initializes an new, empty Addon instance.
        /// </summary>
        public Addon()
        {
            //Author = "Author Name";
            Title = string.Empty;
            Type = string.Empty;
            Description = string.Empty;
            Files = new List<ContentFile>();
            Tags = new List<string>();
            Ignores = new List<string>();
            /*SteamID = 0;*/
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Sets up a new instance of Addon using the data provided by the specified addon reader.
        /// </summary>
        /// <param name="reader">The addon reader which handled reading the addon file.</param>
        /// <param name="lenientReading">Whether or not to ignore invalid file names when reading.</param>
        /// <exception cref="ArgumentException">Happens if a file with the same path is already added.</exception>
        /// <exception cref="WhitelistException">The file is prohibited from storing by the global whitelist.</exception>
        /// <exception cref="IgnoredException">The file is prohibited from storing by the addon's ignore list.</exception>
        public Addon(Reader reader, bool lenientReading = false)
            : this()
        {
            //Author = reader.Author;
            Title = reader.Name;
            Description = reader.Description;
            Type = reader.Type;
            Tags = reader.Tags;
            /*AddonVersion = reader.Version;*/
            FormatVersion = reader.FormatVersion;
            /*SteamID = reader.SteamID;*/
            Timestamp = reader.Timestamp;

            foreach (var file in reader.Index)
            {
                if (!lenientReading) CheckRestrictions(file.Path);

                var contentFile = new ContentFile(reader, file);
                Files.Add(contentFile);
            }
        }

        /// <summary>
        /// Sets up a new instance of Addon using the metadata provided from the specified JSON.
        /// </summary>
        /// <param name="addonJson">The JSON instance containing the metadata to use.</param>
        public Addon(Json addonJson)
            : this()
        {
            Title = addonJson.Title;
            Description = addonJson.Description;
            Type = addonJson.Type;
            Tags = new List<string>(addonJson.Tags);
            Ignores = new List<string>(addonJson.Ignores);
            FormatVersion = Version;
            /*SteamID = (ulong)0;*/
            /*AddonVersion = (int)1;*/
            Timestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Gets whether the specified file is ignored by the addon's ignore list.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>True if the addon is ignored, false if not.</returns>
        private bool IsIgnored(string path)
        {
            foreach (var pattern in Ignores)
                if (Whitelist.Check(pattern, path)) return true;

            return false;
        }

        /// <summary>
        /// Gets whether the specified file is allowed to be in GMAs by the global whitelist.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>True if the addon is allowed, false if not.</returns>
        private static bool IsWhitelisted(string path)
        {
            return Whitelist.Check(path.ToLowerInvariant());
        }

        /// <summary>
        /// Check against whitelist patterns to ensure that the file is allowed in the addon.
        /// </summary>
        /// <param name="path">The path of the file to check</param>
        /// <returns>True if the file matched whitelist. False in extreme cases.
        /// Rely on handling the thrown exceptions to sort out prohibited files.</returns>
        /// <exception cref="ArgumentException">Happens if a file with the same path is already added.</exception>
        /// <exception cref="WhitelistException">The file is prohibited from storing by the global whitelist.</exception>
        /// <exception cref="IgnoredException">The file is prohibited from storing by the addon's ignore list.</exception>
        public void CheckRestrictions(string path)
        {
            if (Files.Any(entry => entry.Path == path))
                throw new ArgumentException("A file with the same path is already added.");

            // Check if file is allowed to be added
            if (string.IsNullOrEmpty(path) || path == string.Empty)
                // When adding from realtime, path can become "" if it does not match against the whitelist 
                throw new WhitelistException("Path was empty.");
            if (path.Contains(".."))
                // Realtime shell can have users who will try to traverse folders
                throw new WhitelistException(path + ": contains upwards traversion");
            if (path == "addon.json")
                // Never allow addon.json to be added
                throw new WhitelistException(path + ": is addon.json");
            if (IsIgnored(path))
                throw new IgnoredException(path + ": ignored");
            if (!IsWhitelisted(path))
                throw new WhitelistException(path + ": not allowed by whitelist.");
        }

        /// <summary>
        /// Adds the specified file into the Addon's internal container.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="content">Array of bytes containing the file content.</param>
        /// <exception cref="ArgumentException">Happens if a file with the same path is already added.</exception>
        /// <exception cref="WhitelistException">The file is prohibited from storing by the global whitelist.</exception>
        /// <exception cref="IgnoredException">The file is prohibited from storing by the addon's ignore list.</exception>
        public void AddFile(string path, byte[] content)
        {
            if (path.ToLowerInvariant() != path)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\t\t[Filename contains capital letters]");
                Console.ResetColor();
                path = path.ToLowerInvariant();
            }

            // This should throw the exception if error happens.
            CheckRestrictions(path);

            var file = new ContentFile(path, content);
            Files.Add(file);
        }

        /// <summary>
        /// Sorts the internal file list by filename.
        /// </summary>
        public void Sort()
        {
            Files.Sort((x, y) => string.CompareOrdinal(x.Path, y.Path));
        }

        /// <summary>
        /// Gets the ContentFile entry for the specified path.
        /// </summary>
        /// <param name="path">The path of the file WITHIN the addon.</param>
        /// <returns>The ContentFile instance.</returns>
        /// <exception cref="FileNotFoundException">The specified file is not in the collection.</exception>
        public ContentFile GetFile(string path)
        {
            ContentFile file;

            try
            {
                file = Files.First(e => e.Path == path);
            }
            catch (InvalidOperationException)
            {
                throw new FileNotFoundException("The file is not in the archive.");
            }

            return file;
        }

        /// <summary>
        /// Removes the specified file and its contents from the internal storage.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <exception cref="FileNotFoundException">The specified file is not in the collection.</exception>
        public void RemoveFile(string path)
        {
            var toRemove = Files.Where(e => e.Path == path).ToArray();

            switch (toRemove.Length)
            {
                case 0:
                    throw new FileNotFoundException("The file is not in the archive.");
                case 1:
                    Files.Remove(toRemove.First());
                    break;
            }
        }
    }

    /// <summary>
    /// Represents a file entry to an Addon instance.
    /// </summary>
    public class ContentFile
    {
        /// <summary>
        /// Indicates the types how a file can be stored.
        /// </summary>
        private enum ContentStorageType
        {
            /// <summary>
            /// Represents a storage in the addon itself. The contents will be read from the addon file.
            /// ContentFile.AssociatedReader and ContentFile.ReaderFileEntry must be set.
            /// </summary>
            AddonInstance,
            /// <summary>
            /// Represents a storage on the filesystem. The contents will be read from the disk.
            /// ContentFile.ExternalPath must be set.
            /// </summary>
            Filesystem
        }

        /// <summary>
        /// Gets or sets the path of the file.
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// Gets or sets an array of bytes containg the file content.
        /// </summary>
        public byte[] Content
        {
            get
            {
                byte[] returnContent = null;

                switch (_storage)
                {
                    case ContentStorageType.AddonInstance:
                        if (_associatedReader == null || _readerFileEntry == 0)
                            throw new ArgumentException("Invalid setup in reader information");

                        _associatedReader.GetFile(_readerFileEntry, ref returnContent);

                        break;
                    case ContentStorageType.Filesystem:
                        returnContent = new byte[_externalFile.Length];
                        _externalFile.Seek(0, SeekOrigin.Begin);
                        _externalFile.Read(returnContent, 0, (int)_externalFile.Length);

                        break;
                }

                return returnContent;
            }
            set
            {
                switch (_storage)
                {
                    case ContentStorageType.AddonInstance:
                        // Convert current content file to a filesystem-backed instance.
                        _storage = ContentStorageType.Filesystem;
                        _associatedReader = null;
                        _readerFileEntry = 0;
                        _externalFile = new FileStream(GenerateExternalPath(Path), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

                        // Fallthrough to the next statement so we write the contents.
                        goto case ContentStorageType.Filesystem;
                    case ContentStorageType.Filesystem:
                        _externalFile.Seek(0, SeekOrigin.Begin);
                        _externalFile.Write(value, 0, value.Length);
                        _externalFile.Flush();
                        _externalFile.SetLength(_externalFile.Position);

                        break;
                }
            }
        }

        /// <summary>
        /// The storage of the addon.
        /// </summary>
        private ContentStorageType _storage;

        /// <summary>
        /// The reader where the contents can be read if Storage is AddonInstance.
        /// </summary>
        private Reader _associatedReader;
        /// <summary>
        /// The index FileNumber in the AssociatedReader if Storage is AddonInstance.
        /// </summary>
        private uint _readerFileEntry;

        /// <summary>
        /// The file on the disk if Storage is Filesystem.
        /// </summary>
        private FileStream _externalFile;

        /// <summary>
        /// Gets the size of the content.
        /// </summary>
        public long Size
        {
            get
            {
                if (_storage != ContentStorageType.AddonInstance) return Content.LongLength;
                
                _associatedReader.GetEntry(_readerFileEntry, out var entry);
                return entry.Size;

            }
        }

        /// <summary>
        /// Gets the CRC32 checksum of the content.
        /// </summary>
        public uint Crc
        {
            get
            {
                if (_storage != ContentStorageType.AddonInstance) return CRC32.ComputeChecksum(Content);
                
                _associatedReader.GetEntry(_readerFileEntry, out var entry);
                return entry.Crc;
            }
        }

        /// <summary>
        /// Initializes a new content file using an already existing file from an addon as storage.
        /// </summary>
        /// <param name="reader">The reader of the addon.</param>
        /// <param name="index">The index of the file to use.</param>
        public ContentFile(Reader reader, Reader.IndexEntry index)
        {
            Path = index.Path;

            _storage = ContentStorageType.AddonInstance;
            _associatedReader = reader;
            _readerFileEntry = index.FileNumber;
        }

        /// <summary>
        /// Initializes a new content file using pure content as storage.
        /// </summary>
        /// <param name="path">The path of the file WITHIN the addon.</param>
        /// <param name="content">The array of bytes containing the already set content.</param>
        public ContentFile(string path, byte[] content)
        {
            _storage = ContentStorageType.Filesystem;
            Path = path;

            _externalFile = new FileStream(GenerateExternalPath(path), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

            Content = content;
        }

        /// <summary>
        /// Generates a temporary path for the specified file.
        /// </summary>
        /// <param name="path">The path of the file WITHIN the addon.</param>
        /// <returns>The generated temporary path.</returns>
        public static string GenerateExternalPath(string path)
        {
            var tempfile = SysPath.GetTempFileName();
            File.Delete(tempfile);

            var tempname = SysPath.GetFileNameWithoutExtension(tempfile);

            return $"{SysPath.GetTempPath()}{tempname}_sharpgmad_{SysPath.GetFileName(path)}.tmp";
        }

        /// <summary>
        /// Switches the current ContentFile to represent a file saved into an addon.
        /// Used after saving addons so that previous externally-saved entries are dropped in time.
        /// </summary>
        /// <param name="reader">The reader of the addon.</param>
        /// <param name="index">The index of the file to use.</param>
        public void SwitchToAddonInstance(Reader reader, Reader.IndexEntry index)
        {
            switch (_storage)
            {
                case ContentStorageType.Filesystem:
                    _associatedReader = reader;
                    _readerFileEntry = index.FileNumber;
                    DisposeExternal();

                    _storage = ContentStorageType.AddonInstance;
                    break;
                case ContentStorageType.AddonInstance:
                    // Update the entry itself. There is no need to touch files on disk.
                    _associatedReader = reader;
                    _readerFileEntry = index.FileNumber;
                    break;
            }
        }

        /// <summary>
        /// Disposes the externally saved content backer for the current file.
        /// </summary>
        public void DisposeExternal()
        {
            if (_storage != ContentStorageType.Filesystem) return;

            try
            {
                var path = _externalFile.Name;
                _externalFile.Dispose();
                File.Delete(path);
            }
            catch (Exception)
            {
                // We couldn't delete the file. Noop, we just leave it there.
            }
        }

        /// <summary>
        /// Cleans up the temporary folder from possible stale externally saved content files.
        /// </summary>
        public static void DisposeExternals()
        {
            foreach (var file in Directory.GetFiles(SysPath.GetTempPath(),
                "tmp*_sharpgmad_*.tmp*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // Noop. It is only a temporary file, it shouldn't be that bad if we don't clean it up.
                }
            }
        }

        ~ContentFile()
        {
            DisposeExternal();
        }
    }
}
