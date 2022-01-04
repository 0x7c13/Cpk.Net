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
        /// CpkTable struct
        /// </summary>
        public CpkTable Table { get; }

        /// <summary>
        /// Non-empty child nodes if current CpkEntry is a directory
        /// </summary>
        public IList<CpkEntry> Children { get; }

        public CpkEntry(string virtualPath, CpkTable table)
        {
            VirtualPath = virtualPath;
            Table = table;
            Children = new List<CpkEntry>();
        }

        public CpkEntry(string virtualPath, CpkTable table, IList<CpkEntry> children)
        {
            VirtualPath = virtualPath;
            Table = table;
            Children = children;
        }
    }
}