// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2021-2022, Jiaqi Liu. All rights reserved.
//  Licensed under the MIT License. See LICENSE.txt in the project root for license information.
// ---------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using lzo.net;

namespace Cpk.Net
{
    /// <summary>
    /// CpkArchive
    /// Load and create a tree structure mapping of the internal
    /// file system model within the CPK archive.
    /// </summary>
    public class CpkArchive
    {
        private const uint SupportedCpkVersion = 1;
        private const uint CpkLabel = 0x_1A_54_53_52;
        private const uint CpkDefaultMaxNumOfFile = 32768;	// 每包最多文件个数
        private const int GbkCodePage = 936; // GBK Encoding's code page
        private const int RootCrc = 0;

        private readonly string _filePath;

        private CpkHeader _header;
        private readonly CpkTable[] _tables = new CpkTable[CpkDefaultMaxNumOfFile];

        private readonly Dictionary<uint, byte[]> _fileNameMap = new Dictionary<uint, byte[]>();
        private readonly Dictionary<uint, uint> _crcToTableIndexMap = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, HashSet<uint>> _fatherCrcToChildCrcTableIndexMap = new Dictionary<uint, HashSet<uint>>();

        private readonly CrcHash _crcHash = new CrcHash();

        public CpkArchive(string cpkFilePath)
        {
            _filePath = cpkFilePath;
            _crcHash.Init();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Load Cpk file from filesystem and store a copy of the
        /// header and index table.
        /// </summary>
        /// <returns>Root level CpkEntry nodes</returns>
        /// <exception cref="InvalidDataException"></exception>
        public IList<CpkEntry> Load()
        {
            using FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);

            _header = Utility.ReadStruct<CpkHeader>(stream);

            if (!IsValidCpkHeader(_header))
            {
                throw new InvalidDataException($"File: {_filePath} is not a valid CPK file.");
            }

            Console.WriteLine($"CPK Info: Number of entries: {_header.FileNum}");

            for (var i = 0; i < _header.MaxFileNum; i++)
            {
                _tables[i] = Utility.ReadStruct<CpkTable>(stream);
            }

            BuildCrcIndexMap();
            BuildFileNameMap();

            return GetChildren(RootCrc).ToList();
        }

        /// <summary>
        /// Open and create a Stream pointing to the CPK's internal file location
        /// of the given virtual file path and populate the size of the file.
        /// </summary>
        /// <param name="fileVirtualPath">Virtualized file path inside CPK archive</param>
        /// <param name="size">Size of the file</param>
        /// <returns>A Stream used to read the content</returns>
        /// <exception cref="ArgumentException">Throw if file does not exists</exception>
        /// <exception cref="InvalidOperationException">Throw if given file path is a directory</exception>
        public Stream Open(string fileVirtualPath, out uint size)
        {
            var crc = _crcHash.ToCrc32Hash(fileVirtualPath);
            try
            {
                return Open(crc, out size);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"File: {fileVirtualPath} not found.");
            }
        }

        /// <summary>
        /// Open and create a Stream pointing to the CPK's internal file location
        /// of the given CRC hash and populate the size of the file.
        /// </summary>
        /// <param name="crc">File's CRC hash value</param>
        /// <param name="size">Size of the file</param>
        /// <returns>A Stream used to read the content</returns>
        /// <exception cref="ArgumentException">Throw if CRC does not exists</exception>
        /// <exception cref="InvalidOperationException">Throw if given CRC is a directory</exception>
        public Stream Open(uint crc, out uint size)
        {
            if (!_crcToTableIndexMap.ContainsKey(crc))
            {
                throw new ArgumentException("CRC {crc} not found.");
            }

            var fileName = Encoding.GetEncoding(GbkCodePage).GetString(_fileNameMap[crc]);
            var table = _tables[_crcToTableIndexMap[crc]];

            if (table.IsDirectory())
            {
                throw new InvalidOperationException($"Failed to open {fileName} since it is a directory.");
            }

            FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            stream.Seek(table.StartPos, SeekOrigin.Begin);
            size = table.PackedSize;

            if (table.IsCompressed()) return new LzoStream(stream, CompressionMode.Decompress);
            else return stream;
        }

        private static bool IsValidCpkHeader(CpkHeader header)
        {
            if (header.Lable != CpkLabel) return false;
            if (header.Version != SupportedCpkVersion) return false;
            if (header.TableStart == 0) return false;
            if (header.FileNum > header.MaxFileNum) return false;
            if (header.ValidTableNum > header.MaxTableNum) return false;
            if (header.FileNum > header.ValidTableNum) return false;

            return true;
        }

        private void BuildCrcIndexMap()
        {
            for (uint i = 0; i < _tables.Length; i++)
            {
                var table = _tables[i];
                if (table.IsEmpty() || !table.IsValid() || table.IsDeleted()) continue;

                _crcToTableIndexMap[table.CRC] = i;

                if (_fatherCrcToChildCrcTableIndexMap.ContainsKey(table.FatherCRC))
                {
                    _fatherCrcToChildCrcTableIndexMap[table.FatherCRC].Add(table.CRC);
                }
                else
                {
                    _fatherCrcToChildCrcTableIndexMap[table.FatherCRC] = new HashSet<uint> {table.CRC};
                }
            }
        }

        private void BuildFileNameMap()
        {
            using FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);

            foreach (var table in _tables)
            {
                if (table.IsEmpty() || !table.IsValid() || table.IsDeleted()) continue;

                long extraInfoOffset = table.StartPos + table.PackedSize;
                var extraInfo = new byte[table.ExtraInfoSize];
                stream.Seek(extraInfoOffset, SeekOrigin.Begin);
                stream.Read(extraInfo);

                var fileName = Utility.TrimEnd(extraInfo, new byte[] { 0x00, 0x00 });
                _fileNameMap[table.CRC] = fileName;
            }
        }

        private IEnumerable<CpkEntry> GetChildren(uint fatherCrc, string rootPath = "")
        {
            if (!_fatherCrcToChildCrcTableIndexMap.ContainsKey(fatherCrc))
            {
                yield break;
            }

            if (rootPath != string.Empty)  rootPath += CpkConstants.CpkVirtualDirectorySeparatorChar;

            foreach (var childCrc in _fatherCrcToChildCrcTableIndexMap[fatherCrc])
            {
                var index = _crcToTableIndexMap[childCrc];
                var child = _tables[index];
                var fileName = Encoding.GetEncoding(GbkCodePage).GetString(_fileNameMap[child.CRC]);

                var virtualPath = rootPath + fileName.ToLower();

                if (child.IsDirectory())
                {
                    yield return new CpkEntry(virtualPath, child, GetChildren(child.CRC, virtualPath).ToList());
                }
                else
                {
                    yield return new CpkEntry(virtualPath, child);
                }
            }
        }
    }
}