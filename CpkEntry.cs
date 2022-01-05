// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2021-2022, Jiaqi Liu. All rights reserved.
//  Licensed under the MIT License. See LICENSE.txt in the project root for license information.
// ---------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Cpk.Net
{
    /// <summary>
    /// CpkEntry model
    /// </summary>
    public class CpkEntry
    {
        /// <summary>
        /// Virtualized file system path within CPK file archive
        /// Example: music\pi10a.mp3
        /// </summary>
        public string VirtualPath { get; }

        /// <summary>
        /// True if current entry is a directory
        /// False if current entry is a file
        /// </summary>
        public bool IsDirectory { get; }

        /// <summary>
        /// Non-empty child nodes if current CpkEntry is a directory
        /// </summary>
        public IList<CpkEntry> Children { get; }

        public CpkEntry(string virtualPath, bool isDirectory)
        {
            VirtualPath = virtualPath;
            IsDirectory = isDirectory;
            Children = new List<CpkEntry>();
        }

        public CpkEntry(string virtualPath, bool isDirectory, IList<CpkEntry> children)
        {
            VirtualPath = virtualPath;
            IsDirectory = isDirectory;
            Children = children;
        }
    }
}