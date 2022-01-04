// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2021-2022, Jiaqi Liu. All rights reserved.
//  Licensed under the MIT License. See LICENSE.txt in the project root for license information.
// ---------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Cpk.Net
{
    internal static class Utility
    {
        internal static T ReadStruct<T>(FileStream stream) where T : struct
        {
            var structSize = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[structSize];
            stream.Read(buffer);
            return ByteArrayToStruct<T>(buffer);
        }

        internal static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        private static unsafe T ByteArrayToStruct<T>(byte[] bytes) where T : struct
        {
            fixed (byte* ptr = &bytes[0])
            {
                return (T) (Marshal.PtrToStructure((IntPtr) ptr, typeof(T)) ?? default(T));
            }
        }

        private static int GetPatternIndex(ReadOnlySpan<byte> src, ReadOnlySpan<byte> pattern, int startIndex = 0)
        {
            var maxFirstCharSlot = src.Length - pattern.Length + 1;
            for (var i = startIndex; i < maxFirstCharSlot; i++)
            {
                if (src[i] != pattern[0])
                    continue;

                for (var j = pattern.Length - 1; j >= 1; j--)
                {
                    if (src[i + j] != pattern[j]) break;
                    if (j == 1) return i;
                }
            }

            return -1;
        }

        public static byte[] TrimEnd(byte[] buffer, ReadOnlySpan<byte> pattern)
        {
            var length = GetPatternIndex(buffer, pattern);
            return length == -1 ? buffer : buffer[0..length];
        }
    }
}