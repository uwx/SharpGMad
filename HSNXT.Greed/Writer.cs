using System;
using System.Cryptography;
using System.IO;

namespace HSNXT.Greed
{
    /// <summary>
    /// Provides ways for compiling addons.
    /// </summary>
    internal static class Writer
    {
        /// <summary>
        /// Compiles the specified addon into the specified stream.
        /// </summary>
        /// <param name="addon">The addon to compile.</param>
        /// <param name="stream">The stream which the result should be written to.</param>
        /// <exception cref="IOExecption">Happens if there is a problem with the specified stream.</exception>
        public static void Create(Addon addon, Stream stream)
        {
            var writer = new BinaryWriter(stream);
            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            writer.BaseStream.SetLength(0);

            // Header (5)
            writer.Write(Addon.Magic.ToCharArray()); // Ident (4)
            writer.Write(Addon.Version); // Version (1)
            // SteamID (8) [unused]
            writer.Write((ulong)0);
            // TimeStamp (8)
            writer.Write((ulong)
                (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime()).TotalSeconds);
            // Required content (a list of strings)
            writer.Write((char)0); // signifies nothing
            // Addon Name (n)
            writer.WriteNullTerminatedString(addon.Title);
            // Addon Description (n)
            writer.WriteNullTerminatedString(addon.DescriptionJson);
            // Addon Author (n) [unused]
            writer.WriteNullTerminatedString("Author Name");
            // Addon version (4) [unused]
            writer.Write(1);

            // File list
            uint fileNum = 0;

            foreach (var f in addon.Files)
            {
                // Remove prefix / from filename
                var file = f.Path.TrimStart('/');

                fileNum++;

                writer.Write(fileNum); // File number (4)
                writer.WriteNullTerminatedString(file.ToLowerInvariant()); // File name (all lower case!) (n)
                writer.Write(f.Size); // File size (8) unsigned long
                writer.Write(f.Crc); // File CRC (4) long long
            }
            writer.Flush();

            // Zero to signify the end of files
            fileNum = 0;
            writer.Write(fileNum);

            // The files
            foreach (var f in addon.Files)
            {
                writer.Write(f.Content);
                writer.Flush();
            }

            // CRC what we've written (to verify that the download isn't shitted) (4)
            writer.Seek(0, SeekOrigin.Begin);
            var buffer_whole = new byte[writer.BaseStream.Length];
            writer.BaseStream.Read(buffer_whole, 0, (int)writer.BaseStream.Length);
            ulong addonCRC = CRC32.ComputeChecksum(buffer_whole);
            writer.Write(addonCRC);
            writer.Flush();

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
        }
    }
}
