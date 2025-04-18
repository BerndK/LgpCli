using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
  public class Tree<T>
  {
    private Tree<T>? parent;
    private List<Tree<T>> children = new List<Tree<T>>();
    public Tree(Tree<T>? parent, T value)
    {
      Value = value;
      this.parent = parent;
    }

    public T Value { get; set; }
    public Tree<T>? Parent
    {
      get => parent;
      set
      {
        parent?.children.Remove(this);
        this.parent = value;
        this.parent?.children.Add(this);
      }
    }

    public IReadOnlyList<Tree<T>> Children => children;

    public Tree<T> Add(T value)
    {
      var tree = new Tree<T>(this, value);
      children.Add(tree);
      return tree;
    }
  }
}
