using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
  public class GenericReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class//, IEqualityComparer
  {
    private GenericReferenceEqualityComparer() { }

    public static GenericReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj)
    {
      // Depending on target framework, RuntimeHelpers.GetHashCode might not be annotated
      // with the proper nullability attribute. We'll suppress any warning that might
      // result.
      return RuntimeHelpers.GetHashCode(obj!);
    }
  }
}
