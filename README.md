# About Cpk.Net

This is an implementation of the CPack file reader in C# using `.NET Standard 2.1`.
CPack(.cpk) is a file format used to store game assets in the game Pal3(仙剑奇侠传三) and Pal3a(仙剑奇侠传3外传-问情篇) created by Softstar Technology (Shanghai) Co., Ltd.

## How to build Cpk.Net?
```console
dotnet build
```

## Code Example

```C#
// Example to load and open one file stored inside a CPK archive using Cpk.Net

using Cpk.Net;

const string CpkPath = "...";
const string VirtualFilePath = "..."; // Virtualized/relative file path within CPK archive

var cpk = new CpkArchive(CpkPath);
await cpk.Load();
using var stream = cpk.Open(VirtualFilePath, out uint size);
...
```

```C#
// Example to load and upack all files stored inside a CPK archive using Cpk.Net

using Cpk.Net;

const string CpkPath = "...";
const string OutputFolderPath = "...";

var cpk = new CpkArchive(CpkPath);
var rootNodes = await cpk.Load();
Unpack(rootNodes, OutputFolderPath);

void Unpack(IList<CpkEntry> nodes, string rootPath)
{
    foreach (var node in nodes)
    {
        // You need to replace CPK directory separator char with 
        // your current system directory separator char.
        // So this code can run anywhere.
        var relativePath = node.VirtualPath.Replace(
            CpkConstants.CpkVirtualDirectorySeparatorChar, Path.DirectorySeparatorChar);
        
        if (node.Table.IsDirectory())
        {
            new DirectoryInfo(rootPath + relativePath).Create();
            Unpack(node.Children, rootPath);
        }
        else
        {
            using var readStream = cpk.Open(node.Table.CRC, out var size);
            using var writeStream = new FileStream(rootPath + relativePath, FileMode.Create, FileAccess.Write);
            CopyStream(readStream, writeStream, (int)size);
            Console.WriteLine($"{node.VirtualPath} unpacked.");
        }
    }
}

static void CopyStream(Stream input, Stream output, int length, int bufferSize = 32768)
{
    byte[] buffer = new byte[bufferSize];
    int read;
    while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, length))) > 0)
    {
        output.Write(buffer, 0, read);
        length -= read;
    }
}

```
# License

MIT License

Copyright (c) 2022 Jiaqi Liu

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

