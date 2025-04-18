using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Infrastructure
{
  public static class TypeNameHelper
  {
    private static readonly Dictionary<Type, string> _typeToFriendlyName = new Dictionary<Type, string>
    {
        { typeof(string), "string" },
        { typeof(object), "object" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(char), "char" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(short), "short" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(sbyte), "sbyte" },
        { typeof(float), "float" },
        { typeof(ushort), "ushort" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        { typeof(void), "void" }
    };

    public static string FriendlyName(this Type type, string nestedSeparator = ".")
    {
      if (type.FullName == null)
        return string.Empty;

      if (type.IsArray)
      {
        return type.GetElementType()!.FriendlyName(nestedSeparator) + "[]";
      }

      string? friendlyName;
      if (_typeToFriendlyName.TryGetValue(type, out friendlyName))
      {
        return friendlyName;
      }

      friendlyName = type.Name;
      if (type.IsGenericType)
      {
        int backtick = friendlyName.IndexOf('`');
        if (backtick > 0)
        {
          friendlyName = friendlyName.Remove(backtick);
        }
        friendlyName += $"<{string.Join(", ", type.GetGenericArguments().Select(e => e.FriendlyName(nestedSeparator)))}>";
      }

      if (type.IsNested)
      {
        friendlyName = $"{type.DeclaringType!.FriendlyName(nestedSeparator)}{nestedSeparator}{friendlyName}";
      }
      return friendlyName;
    }

    public static string FriendlyName(object? o, string nestedSeparator = ".") => o?.GetType().FriendlyName(nestedSeparator) ?? "null";
  }
}
