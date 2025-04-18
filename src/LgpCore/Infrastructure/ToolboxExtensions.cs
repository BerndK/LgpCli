using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Infrastructure
{
  public static class ToolboxExtensions
  {
    public static object? GetValueSafeTyped(this RegistryKey registryKey, string? name, object? defaultValue = null, RegistryValueOptions options = RegistryValueOptions.None)
    {
      //Registry does not return uint but int, and also not ulong but long
      var value = registryKey.GetValue(name, defaultValue, options);
      switch (value)
      {
        case int intValue:
          return (uint) intValue;
        case long longValue:
          return (ulong) longValue;
      }

      return value;
    }

    public static void SetValueSafeTyped(this RegistryKey registryKey, string name, object value, RegistryValueKind valueKind)
    {
      switch (value)
      {
        case uint uintValue:
          registryKey.SetValue(name, (int)uintValue, valueKind);
          break;
        case ulong ulongValue:
          registryKey.SetValue(name, (long)ulongValue, valueKind);
          break;
        default:
          registryKey.SetValue(name, value, valueKind);
          break;
      }
    }

    public static IEnumerable<TSource> WhereNotDefault<TSource>(this IEnumerable<TSource?> source) where TSource : class
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      return source.Where(value => !EqualityComparer<TSource?>.Default.Equals(value, default)).Select(e => e!);
    }

    public static IEnumerable<TSource> WhereNotDefault<TSource>(this IEnumerable<Nullable<TSource>> source) where TSource : struct
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      return source
        .Where(value => !EqualityComparer<TSource?>.Default.Equals(value, default))
        .Select(e => e!.Value);
    }


  }
}