using System.Collections.ObjectModel;

namespace Infrastructure;

public class KeyedCollectionDelegated<TKey, TItem> : KeyedCollection<TKey, TItem> where TKey : notnull
{
  private readonly Func<TItem, TKey> _buildKeyFunc;

  public KeyedCollectionDelegated(Func<TItem, TKey> buildKeyFunc)
  {
    _buildKeyFunc = buildKeyFunc;
  }

  public KeyedCollectionDelegated(Func<TItem, TKey> buildKeyFunc, IEqualityComparer<TKey>? comparer) : base(comparer)
  {
    _buildKeyFunc = buildKeyFunc;
  }

  public KeyedCollectionDelegated(Func<TItem, TKey> buildKeyFunc, IEqualityComparer<TKey>? comparer, int dictionaryCreationThreshold) : base(comparer, dictionaryCreationThreshold)
  {
    _buildKeyFunc = buildKeyFunc;
  }

  protected override TKey GetKeyForItem(TItem item) => _buildKeyFunc(item);
}