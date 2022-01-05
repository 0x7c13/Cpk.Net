// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2021-2022, Jiaqi Liu. All rights reserved.
//  Licensed under the MIT License. See LICENSE.txt in the project root for license information.
// ---------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;

namespace Cpk.Net
{
    internal class CrcHash
    {
        private const uint CrcTableMax = 256;
        private const uint Polynomial = 0x04C11DB7; // CRC seed
        private const int GbkCodePage = 936; // GBK Encoding's code page

        private static readonly uint[] CrcTable = new uint[CrcTableMax];

        private static bool _initialized;

        // generate the table of CRC remainders for all possible bytes
        public void Init()
        {
            for (uint i = 0; i < CrcTableMax; i++)
            {
                var crcAccum = i << 24;

                for (var j = 0; j < 8; j++)
                {
                    if ((crcAccum & 0x80000000L) != 0)
                        crcAccum = (crcAccum << 1) ^ Polynomial;
                    else
                        crcAccum = (crcAccum << 1);
                }
                CrcTable[i] = crcAccum;
            }

            _initialized = true;
        }

        public uint ToCrc32Hash(string str)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return ToCrc32Hash(Encoding.GetEncoding(GbkCodePage).GetBytes(str));
        }

        public uint ToCrc32Hash(byte[] strBytes)
        {
            if (strBytes.Length == 0 || strBytes[0] == 0) return 0;

            var data = strBytes.ToList();

            // Append 0 to the end of the byte list if not
            if (data[^1] != 0) data.Add(0);

            if (!_initialized)
            {
                throw new Exception($"{nameof(CrcHash)} not initialized.");
            }

            var index = 0;
            uint result  = (uint)(data[index++] << 24);
            if (data[index] != 0)
            {
                result |= (uint)(data[index++] << 16);

                if(data[index] != 0)
                {
                    result |= (uint)(data[index++] << 8);
                    if (data[index] != 0)
                    {
                        result |= data[index++];
                    }
                }
            }
            result = ~result;

            while (data[index] != 0)
            {
                result = (result << 8 | data[index]) ^ CrcTable[result >> 24];
                index++;
            }

            return ~result;
        }
    }
}