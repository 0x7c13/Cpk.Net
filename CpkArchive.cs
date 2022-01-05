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
using System.Threading.Tasks;
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
        private const uint CpkLabel = 0x_1A_54_53_52;  // CPK header label
        private const uint CpkDefaultMaxNumOfFile = 32768;	// Max number of files per archive
        private const int GbkCodePage = 936; // GBK Encoding's code page
        private const int RootCrc = 0;

        private readonly string _filePath;
        private readonly CpkTable[] _tables = new CpkTable[CpkDefaultMaxNumOfFile];

        private readonly Dictionary<uint, byte[]> _fileNameMap = new Dictionary<uint, byte[]>();
        private readonly Dictionary<uint, uint> _crcToTableIndexMap = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, HashSet<uint>> _fatherCrcToChildCrcTableIndexMap = new Dictionary<uint, HashSet<uint>>();

        private readonly CrcHash _crcHash = new CrcHash();

        private bool _loaded;

        public CpkArchive(string cpkFilePath)
        {
            _filePath = cpkFilePath;
            _crcHash.Init();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Load the CPK file from file system.
        /// This method should be called before using any other file
        /// operations.
        /// </summary>
        /// <returns>Root level CpkEntry nodes</returns>
        /// <exception cref="InvalidDataException">Throw if file is not valid CPK archive</exception>
        public async Task LoadAsync()
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);

            var header = await Utility.ReadStruct<CpkHeader>(stream);

            if (!IsValidCpkHeader(header))
            {
                throw new InvalidDataException($"File: {_filePath} is not a valid CPK file.");
            }

            for (var i = 0; i < header.MaxFileNum; i++)
            {
                _tables[i] = await Utility.ReadStruct<CpkTable>(stream);
            }

            await Task.Run(BuildCrcIndexMap);
            await BuildFileNameMap();

            _loaded = true;
        }

        /// <summary>
        /// Build a tree structure map of the internal files
        /// and return the root nodes in CpkEntry format
        /// </summary>
        /// <returns>Root level CpkEntry nodes</returns>
        public async Task<IList<CpkEntry>> GetRootEntries()
        {
            CheckIfArchiveLoaded();
            return await Task.FromResult(GetChildren(RootCrc).ToList());
        }

        /// <summary>
        /// Check if file exists inside the archive using the
        /// virtual path.
        /// </summary>
        /// <param name="fileVirtualPath"></param>
        /// <returns></returns>
        public bool FileExists(string fileVirtualPath)
        {
            CheckIfArchiveLoaded();
            var crc = _crcHash.ToCrc32Hash(fileVirtualPath.ToLower());
            return _crcToTableIndexMap.ContainsKey(crc);
        }

        /// <summary>
        /// Open and create a Stream pointing to the CPK's internal file location
        /// of the given virtual file path and populate the size of the file.
        /// </summary>
        /// <param name="fileVirtualPath">Virtualized file path inside CPK archive</param>
        /// <param name="size">Size of the file</param>
        /// <param name="isCompressed">True if file is compressed</param>
        /// <returns>A Stream used to read the content</returns>
        /// <exception cref="ArgumentException">Throw if file does not exists</exception>
        /// <exception cref="InvalidOperationException">Throw if given file path is a directory</exception>
        public Stream Open(string fileVirtualPath, out uint size, out bool isCompressed)
        {
            CheckIfArchiveLoaded();

            if (!FileExists(fileVirtualPath))
            {
                throw new ArgumentException($"<{fileVirtualPath}> does not exists in the archive.");
            }

            var crc = _crcHash.ToCrc32Hash(fileVirtualPath.ToLower());
            var table = _tables[_crcToTableIndexMap[crc]];

            if (table.IsDirectory())
            {
                throw new InvalidOperationException($"Cannot open <{fileVirtualPath}> since it is a directory.");
            }

            return OpenInternal(table, out size, out isCompressed);
        }

        private Stream OpenInternal(CpkTable table, out uint size, out bool isCompressed)
        {
            FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            stream.Seek(table.StartPos, SeekOrigin.Begin);
            size = table.PackedSize;

            if (table.IsCompressed())
            {
                isCompressed = true;
                return new LzoStream(stream, CompressionMode.Decompress);
            }
            else
            {
                isCompressed = false;
                return stream;
            }
        }

        private void CheckIfArchiveLoaded()
        {
            if (!_loaded)
            {
                throw new Exception($"Cpk file not loaded yet. Please call {nameof(LoadAsync)} method before using.");
            }
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

        private async Task BuildFileNameMap()
        {
            await using FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);

            foreach (var table in _tables)
            {
                if (table.IsEmpty() || !table.IsValid() || table.IsDeleted()) continue;

                long extraInfoOffset = table.StartPos + table.PackedSize;
                var extraInfo = new byte[table.ExtraInfoSize];
                stream.Seek(extraInfoOffset, SeekOrigin.Begin);
                await stream.ReadAsync(extraInfo);

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

                var virtualPath = rootPath + fileName;

                if (child.IsDirectory())
                {
                    yield return new CpkEntry(virtualPath, child.IsDirectory(), GetChildren(child.CRC, virtualPath).ToList());
                }
                else
                {
                    yield return new CpkEntry(virtualPath, child.IsDirectory());
                }
            }
        }
    }
}