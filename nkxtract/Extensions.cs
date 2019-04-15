using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nkxtract
{
  static class Extensions
  {
    public static string ToHexCompact(this byte[] k)
    {
      StringBuilder sb = new StringBuilder(k.Length * 2);
      foreach (var b in k)
      {
        sb.AppendFormat("{0:X2}", b);
      }
      return sb.ToString();
    }
    public static byte[] FromHexCompact(this string k)
    {
      var b = new List<byte>();
      var key = k.Replace(" ", "");
      for (var x = 0; x < key.Length - 1;)
      {
        byte result = 0;
        int sub;
        for (var i = 0; i < 2; i++, x++)
        {
          result <<= 4;
          if (key[x] >= '0' && key[x] <= '9')
            sub = '0';
          else if (key[x] >= 'a' && key[x] <= 'f')
            sub = 'a' - 10;
          else if (key[x] >= 'A' && key[x] <= 'F')
            sub = 'A' - 10;
          else
            continue;
          result |= (byte)(key[x] - sub);
        }
        b.Add(result);
      }
      return b.ToArray();
    }
  }
}
