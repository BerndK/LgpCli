using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LgpCore.Infrastructure
{
  public static class Escape
  {
    public static string EscapeString(string s)
    {
      return Regex.Escape(s);
    }

    public static string EscapeStringForCommandline(string s)
    {
      s = s.Replace("\\", "\\~");
      s = Regex.Escape(s);
      s = s.Replace("\"", "\\\"");
      return s;
    }

    public static string UnescapeStringFromCommandline(string s)
    {
      s = UnescapeString(s);
      s = s.Replace("\\~", "\\");
      return s;
    }
    
    public static string UnescapeString(string s)
    {
      return Regex.Unescape(s);
    }
  }
}
