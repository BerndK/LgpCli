using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
  public static class TreeVisualizer
  {
    public static string Visualize<T>(T? root, Func<T, IList<T>?> getChildsFunc, Func<T, string?> nameFunc) =>
      Visualize(root, getChildsFunc, nameFunc, out _);

    public static string Visualize<T>(T? root, Func<T, IList<T>?> getChildsFunc, Func<T, string?> nameFunc, out List<T> leafs)
    {
      var sb = new StringBuilder();
      var handled = new HashSet<T>();
      var locLeafs = new HashSet<T>();

      void Print(T? tree, bool isLast, string prefix)
      {
        if (tree == null)
          return;
        (string current, string next) = isLast
          ? (prefix + "└─" + nameFunc(tree), prefix + "  ")
          : (prefix + "├─" + nameFunc(tree), prefix + "│ ");
        
        sb.Append(current[2..]);
        var children = getChildsFunc(tree);
        if (handled.Contains(tree) && (children?.Count ?? 0) > 0)
        {
          sb.AppendLine(" ... (cyclic)");
        }
        else
        {
          handled.Add(tree);
          sb.AppendLine();
          if (children != null && children.Any())
          {
            T lastChild = children.Last()!;
            foreach (var child in children)
            {
              Print(child, object.ReferenceEquals(child, lastChild), next);
            }
          }
          else
            locLeafs.Add(tree);
        }
      }

      Print(root, true, "");
      leafs = locLeafs.ToList();
      return sb.ToString();
    }
  }
}
