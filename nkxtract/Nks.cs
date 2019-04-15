/*
    Copyright 2019 Maxton

    This file is part of nkxtract.

    nkxtract is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with nkxtract.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace nkxtract
{
  class Key
  {
    public byte[] erk;
    public byte[] riv;
  }

  class KeyLoader
  {
    const string prefix = "HKEY_LOCAL_MACHINE\\Software\\Native Instruments\\";
    public static Key LoadKey(string name)
    {
      // Super secret obfuscation:
      // JDX = KEY rot1
      var key = Microsoft.Win32.Registry.GetValue(prefix + name, "JDX", null) as string;
      // HU = IV rot1
      var iv = Microsoft.Win32.Registry.GetValue(prefix + name, "HU", null) as string;
      if (key == null || iv == null)
      {
        return null;
      }
      return new Key
      {
        erk = key.FromHexCompact(),
        riv = iv.FromHexCompact()
      };
    }
  }

  class FileDecryptStream : Stream
  {
    Stream s;
    uint offset, len, end;
    byte[] xor;
    const int XorLength = 0x10000;
    public FileDecryptStream(Stream s, uint offset, uint len, Key k)
    {
      this.s = s;
      this.offset = offset;
      this.len = len;
      this.end = offset + len;
      this.xor = new byte[XorLength];
      var lcg_seed = 0x608da0a2;
      for (int i = 0; i < XorLength; i++)
      {
        lcg_seed = lcg_seed * 0x343fd + 0x269ec3;
        xor[i] = (byte)(lcg_seed >> 16);
      }

      byte[] cryptedCounter = new byte[16];
      byte[] counter = k.riv.ToArray();
      int counterLoc = 0;
      using (AesManaged aesAlg = new AesManaged())
      {
        aesAlg.Mode = CipherMode.ECB;
        aesAlg.BlockSize = 128;
        aesAlg.KeySize = 128;
        aesAlg.Padding = PaddingMode.None;
        aesAlg.Key = k.erk;
        ICryptoTransform encryptor = aesAlg.CreateEncryptor();
        encryptor.TransformBlock(counter, 0, counter.Length, cryptedCounter, 0);
        for (int i = 0; i < XorLength; i++)
        {
          if (i != 0 && i % 16 == 0)
          {
            for (int j = 15; j >= 0; j--)
            {
              if (++counter[j] != 0)
                break;
            }
            counterLoc = 0;
            encryptor.TransformBlock(counter, 0, counter.Length, cryptedCounter, 0);
          }
          xor[i] = (byte)(xor[i] ^ cryptedCounter[counterLoc]); //decrypt one byte
          counterLoc++;
        }
      }
      Position = 0;
    }

    public override bool CanRead => s.Position >= offset && s.Position < offset + len;

    public override bool CanSeek => s.CanSeek;

    public override bool CanWrite => s.CanWrite;

    public override long Length => len;

    public override long Position { get => s.Position - offset; set => s.Position = value + offset; }

    public override void Flush() => s.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
      var start = Position;
      count = Math.Max(0, Math.Min(count, (int)(len - Position)));
      int r = s.Read(buffer, offset, count);
      for(int i = 0; i < r; i++)
      {
        buffer[offset + i] ^= xor[(start + i) % XorLength];
      }
      return r;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      switch (origin)
      {
        case SeekOrigin.Begin:
          s.Position = offset + this.offset;
          break;
        case SeekOrigin.Current:
          return s.Seek(offset, SeekOrigin.Current);
        case SeekOrigin.End:
          s.Position = offset + this.offset + this.len;
          break;
        default:
          break;
      }
      return s.Position;
    }

    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotImplementedException();
    }
  }

  class Nks
  {
    const uint IdDir = 0x5e70ac54;
    const uint IdEncFile = 0x16ccf80a;
    const uint IdFile = 0x4916e63c;

    const uint FileOffsetXor = 0x1f4e0c8d;

    Stream s;
    Key k;
    Dir root;
    public Nks(Stream s, Key k)
    {
      this.s = s;
      this.k = k;
      root = ReadDirectory(0, "");
    }

    public void Extract(string output)
    {
      if (!Directory.Exists(output))
        throw new ArgumentException("Invalid output directory");
      ExtractHelper(output, root);
    }

    private void ExtractHelper(string output, Dir d)
    {
      Directory.CreateDirectory(output);
      foreach(var it in d.children)
      {
        if(it is Dir d2)
        {
          ExtractHelper(Path.Combine(output, d2.name), d2);
        }
        else if(it is FileNode f)
        {
          using (var of = File.Open(Path.Combine(output, f.name), FileMode.Create))
          using (var fi = new FileDecryptStream(s, f.offset, f.size, k))
          {
            Console.WriteLine(of.Name);
            fi.CopyTo(of);
          }
        }
      }
    }

    public void PrintFileListing()
    {
      PrintDir(root, "");
    }

    private void PrintDir(Dir d, string pfx)
    {
      foreach (var it in d.children)
      {
        Console.WriteLine($"{pfx}{it.name}");
        if(it is Dir)
        {
          PrintDir(it as Dir, pfx + "|-- ");
        }
      }
    }

    Dir ReadDirectory(uint offset, string name)
    {
      s.Position = offset;
      uint Magic = s.ReadUInt32LE();
      if (Magic != IdDir)
      {
        throw new InvalidDataException($"Directory {name} had an invalid magic: {Magic:X2}");
      }

      ushort Version = s.ReadUInt16LE();
      if (Version != 0x111)
      {
        throw new InvalidDataException($"Directory {name} had unknown version: {Version:X3}");
      }
      uint Id = s.ReadUInt32LE();
      uint unk_0 = s.ReadUInt32LE();
      uint Entries = s.ReadUInt32LE();
      uint unk_1 = s.ReadUInt32LE();

      var ret = new Dir
      {
        magic = Magic,
        offset = offset,
        name = name,
        children = new Item[Entries]
      };

      for (uint i = 0; i < Entries; i++)
      {
        ret.children[i] = ReadDirent();
      }
      return ret;
    }

    FileNode ReadFile(uint offset, string name)
    {
      s.Position = offset;
      var f = new FileNode
      {
        offset = offset + 0x1f,
        name = name,
      };
      f.magic = s.ReadUInt32LE();

      switch (f.magic)
      {
        case IdEncFile:
          f.encrypted = true;
          break;
        case IdFile:
          f.encrypted = false;
          break;
        default:
          throw new InvalidDataException($"File {name} had an invalid magic: {f.magic:X2}");
      }

      ushort Version = s.ReadUInt16LE();
      if (Version != 0x111)
      {
        throw new InvalidDataException($"File {name} had unknown version: {Version:X3}");
      }

      var Id = s.ReadUInt32LE();
      f.KeyIndex = s.ReadUInt32LE();
      s.Position += 5; // Unknown stuff
      f.size = s.ReadUInt32LE();
      var unk2 = s.ReadUInt32LE();
      var unk3 = s.ReadUInt32LE();

      return f;
    }

    // Reads a directory entry, and also goes ahead and reads the file or directory that it references
    Item ReadDirent()
    {
      var pos = s.Position;
      var ent_size = s.ReadUInt16LE();
      var file_offset = s.ReadUInt32LE();
      var entry_type = s.ReadUInt16LE();
      if (entry_type < 0 || entry_type > 2)
      {
        throw new InvalidDataException($"Unknown entry type: {entry_type}");
      }
      var name = s.ReadBytes((int)(ent_size - (s.Position - pos)));
      var name_str = Encoding.Unicode.GetString(name).TrimEnd('\0');

      if (entry_type == 1)
      {
        var dir = ReadDirectory(file_offset, name_str);
        s.Position = pos + ent_size;
        return dir;
      }
      else
      {
        var file = ReadFile(file_offset ^ FileOffsetXor, name_str);
        s.Position = pos + ent_size;
        return file;
      }
    }

    class Item
    {
      public uint magic;
      public uint offset;
      public string name;
    }
    class FileNode : Item
    {
      public uint size;
      public uint KeyIndex;
      public bool encrypted;
    }
    class Dir : Item
    {
      public Item[] children;
    }
  }
}
