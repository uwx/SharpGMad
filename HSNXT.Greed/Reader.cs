using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace HSNXT.Greed
{
    /// <summary>
    /// Represents an error regarding reading addon files.
    /// </summary>
    [Serializable]
    internal class ReaderException : Exception
    {
        public ReaderException() { }
        public ReaderException(string message) : base(message) { }
        public ReaderException(string message, Exception inner) : base(message, inner) { }
        protected ReaderException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Provides methods for reading a compiled GMA file.
    /// </summary>
    public class Reader : IDisposable
    {
        /// <summary>
        /// Represents a file's entry in the GMA index.
        /// </summary>
        public struct IndexEntry
        {
            /// <summary>
            /// The path of the file.
            /// </summary>
            public string Path;
            /// <summary>
            /// The size (in bytes) of the file.
            /// </summary>
            public long Size;
            /// <summary>
            /// The CRC checksum of file contents.
            /// </summary>
            public uint Crc;
            /// <summary>
            /// The index of the file.
            /// </summary>
            public uint FileNumber;
            /// <summary>
            /// The offset (in bytes) where the file content is stored in the GMA.
            /// </summary>
            public long Offset;
        }

        /// <summary>
        /// The internal buffer where the addon is loaded.
        /// </summary>
        private readonly Stream _buffer;
        /// <summary>
        /// The byte representing the version character.
        /// </summary>
        public char FormatVersion { get; private set; }
        /// <summary>
        /// Gets the name of the addon.
        /// </summary>
        public string Name { get; private set; }
        /*/// <summary>
        /// Gets the author of the addon. (Currently unused, will always return "Author Name.")
        /// </summary>
        public string Author { get; private set; }*/
        /// <summary>
        /// Gets the description of the addon.
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// Gets the type of the addon.
        /// </summary>
        public string Type { get; private set; }
        /* Not used.
        /// <summary>
        /// Gets the SteamID of the creator.
        /// </summary>
        public ulong SteamID { get; private set; }*/
        /// <summary>
        /// Gets the creation date and time of the addon
        /// </summary>
        public DateTime Timestamp { get; private set; }
        /* Not used.
        /// <summary>
        /// Gets the version of the addon.
        /// </summary>
        public int Version { get; private set; }*/
        /// <summary>
        /// Represents the index area of the addon.
        /// </summary>
        public readonly List<IndexEntry> Index;
        /// <summary>
        /// Represents the offset where the file content storage begins.
        /// </summary>
        private ulong _fileblock;
        /// <summary>
        /// Contains a list of strings, the tags of the read addon.
        /// </summary>
        public List<string> Tags;
        
        /// <summary>
        /// Private constructor to set up object references.
        /// </summary>
        private Reader()
        {
            Index = new List<IndexEntry>();
            Tags = new List<string>();
        }

        /// <summary>
        /// Reads and parses the specified addon file.
        /// </summary>
        /// <param name="stream">The file stream representing the addon file.</param>
        /// <exception cref="System.IO.IOException">Any sort of error regarding reading from the provided stream.</exception>
        /// <exception cref="ReaderException">Errors parsing the file</exception>
        public Reader(Stream stream)
            : this()
        {
            // Seek and read a byte to test access to the stream.
            stream.Seek(0, SeekOrigin.Begin);
            stream.ReadByte();
            stream.Seek(0, SeekOrigin.Begin);

            _buffer = stream;

            Parse();
        }

        /// <summary>
        /// Parses the read addon stream into the instance properties.
        /// </summary>
        /// <exception cref="ReaderException">Parsing errors.</exception>
        private void Parse()
        {
            if (_buffer.Length == 0)
            {
                throw new ReaderException("Attempted to read from empty buffer.");
            }

            _buffer.Seek(0, SeekOrigin.Begin);
            var reader = new BinaryReader(_buffer);

            // Ident
            if (string.Join(string.Empty, reader.ReadChars(Addon.Magic.Length)) != Addon.Magic)
            {
                throw new ReaderException("Header mismatch.");
            }

            FormatVersion = reader.ReadChar();
            if (FormatVersion > Addon.Version)
            {
                throw new ReaderException("Can't parse version " + Convert.ToString(FormatVersion) + " addons.");
            }

            /*SteamID = */reader.ReadUInt64(); // SteamID (long)
            Timestamp = new DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime().
                AddSeconds(reader.ReadInt64()); // Timestamp (long)

            // Required content (not used at the moment, just read out)
            if (FormatVersion > 1)
            {
                var content = reader.ReadNullTerminatedString();

                while (content != string.Empty)
                    content = reader.ReadNullTerminatedString();
            }

            Name = reader.ReadNullTerminatedString();
            Description = reader.ReadNullTerminatedString();
            reader.ReadNullTerminatedString(); // This would be the author... currently not implemented
            /*Version = */reader.ReadInt32(); // Addon version (unused)

            // File index
            var fileNumber = 1;
            var offset = 0;

            while (reader.ReadInt32() != 0)
            {
                var entry = new IndexEntry
                {
                    Path = reader.ReadNullTerminatedString(),
                    Size = reader.ReadInt64(), // long long
                    Crc = reader.ReadUInt32(), // unsigned long
                    Offset = offset,
                    FileNumber = (uint) fileNumber
                };

                Index.Add(entry);

                offset += (int)entry.Size;
                fileNumber++;
            }

            _fileblock = (ulong)reader.BaseStream.Position;

            // Try to parse the description
            var type = string.Empty;
            Description = Json.ParseDescription(Description, ref type, ref Tags);
            Type = type; // Circumvent "A property, indexer or dynamic member access may not be passed as an out or ref parameter"
        }

        /// <summary>
        /// Rereads and parses the addon data from the specified file once more.
        /// </summary>
        /// <exception cref="ReaderException">Parsing errors.</exception>
        public void Reparse()
        {
            Index.Clear();

            Parse();
        }

        /// <summary>
        /// Gets the index entry for the specified file.
        /// </summary>
        /// <param name="fileId">The index of the file.</param>
        /// <param name="entry">The IndexEntry object to be filled with data.</param>
        /// <returns>True if the entry was successfully found, false otherwise.</returns>
        public bool GetEntry(uint fileId, out IndexEntry entry)
        {
            if (Index.Count(file => file.FileNumber == fileId) == 0)
            {
                entry = new IndexEntry();
                return false;
            }

            entry = Index.First(file => file.FileNumber == fileId);
            return true;
        }

        /// <summary>
        /// Gets the specified file contents from the addon and write them into a stream.
        /// </summary>
        /// <param name="fileId">The index of the file.</param>
        /// <param name="buffer">The stream the contents should be written to.</param>
        /// <returns>True if the file was successfully read, false otherwise.</returns>
        public bool GetFile(uint fileId, MemoryStream buffer)
        {
            if (!GetEntry(fileId, out var entry)) return false;

            var readBuffer = new byte[entry.Size];
            _buffer.Seek((long)_fileblock + entry.Offset, SeekOrigin.Begin);
            _buffer.Read(readBuffer, 0, (int)entry.Size);

            buffer.Write(readBuffer, 0, readBuffer.Length);
            return true;
        }

        /// <summary>
        /// Gets the specified file contents from the addon.
        /// </summary>
        /// <param name="fileId">The index of the file.</param>
        /// <param name="buffer">The variable where the all file bytes should be put.</param>
        /// <returns>True if the file was successfully read, false otherwise.</returns>
        public bool GetFile(uint fileId, ref byte[] buffer)
        {
            if (!GetEntry(fileId, out var entry)) return false;

            buffer = new byte[entry.Size];
            _buffer.Seek((long)_fileblock + entry.Offset, SeekOrigin.Begin);
            _buffer.Read(buffer, 0, (int)entry.Size);

            return true;
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}