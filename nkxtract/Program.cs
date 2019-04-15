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
