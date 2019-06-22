﻿namespace System.Cryptography
{
    /// <summary>
    /// Provides methods to compute CRC32 checksums.
    /// </summary>
    internal static class CRC32
    {
        /// <summary>
        /// The table containing calculation polynomials.
        /// </summary>
        private static uint[] table;

        /// <summary>
        /// Calculates the CRC32 checksum for the provided byte array.
        /// </summary>
        /// <param name="bytes">The bytes to calculate the checksum for.</param>
        /// <returns>The checksum as an unsigned integer.</returns>
        public static uint ComputeChecksum(byte[] bytes)
        {
            var crc = 0xffffffff;
            for (var i = 0; i < bytes.Length; ++i)
            {
                var index = (byte)((crc & 0xff) ^ bytes[i]);
                crc = (crc >> 8) ^ table[index];
            }
            return ~crc;
        }

        /// <summary>
        /// Calculates the CRC32 checksum for the provided byte array.
        /// </summary>
        /// <param name="bytes">The bytes to calculate the checksum for.</param>
        /// <returns>The checksum as an array of bytes.</returns>
        public static byte[] ComputeChecksumBytes(byte[] bytes)
        {
            return BitConverter.GetBytes(ComputeChecksum(bytes));
        }

        /// <summary>
        /// Sets up the CRC32 generator by calculating the polynomial values.
        /// </summary>
        static CRC32()
        {
            var poly = 0xedb88320;
            table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i < table.Length; ++i)
            {
                temp = i;
                for (var j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (temp >> 1) ^ poly;
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }
                table[i] = temp;
            }
        }
    }
}