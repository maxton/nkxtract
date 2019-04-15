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
  class Program
  {
    static void Main(string[] args)
    {
      if(args.Length != 2)
      {
        Console.WriteLine("Usage: nkxtract.exe path/to/file.nkx path/to/output/dir");
        return;
      }
      string inputFile = args[0];
      string outputDir = args[1];

      var key = KeyLoader.LoadKey("Release");
      if (key == null)
      {
        Console.WriteLine("Couldn't load decryption key: No key found in registry");
        return;
      }
      
      try
      {
        using (var s = File.OpenRead(inputFile))
        {
          var nks = new Nks(s, key);
          nks.Extract(outputDir);
        }
      }
      catch (InvalidDataException e)
      {
        Console.WriteLine("Could not extract files: " + e.Message);
      }
    }
  }
}
