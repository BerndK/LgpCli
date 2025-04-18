using System;
using System.Collections;
using System.Reflection;

namespace Infrastructure
{
  /// <summary>
  /// Provides a set of static methods for creating Disposables.
  /// </summary>
  public static class Disposable
  {
    /// <summary>Gets the disposable that does nothing when disposed.</summary>
    public static IDisposable Empty => DefaultDisposable.Instance;

    /// <summary>
    /// Creates a disposable object that invokes the specified action when disposed.
    /// </summary>
    /// <param name="dispose">Action to run during the first call to <see cref="M:System.IDisposable.Dispose" />. The action is guaranteed to be run at most once.</param>
    /// <returns>The disposable object that runs the given action upon disposal.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="dispose" /> is null.</exception>
    public static IDisposable Create([Obfuscation(Exclude = false, Feature = "-rename")] Action dispose)
    {
      if (dispose == null)
        throw new ArgumentNullException(nameof(dispose));
      return new AnonymousDisposable(dispose);
    }
    public static DisposableValue<T> Create<T>([Obfuscation(Exclude = false, Feature = "-rename")] Action dispose,
      T value)
    {
      if (dispose == null)
        throw new ArgumentNullException(nameof(dispose));
      return new DisposableValue<T>(value, dispose);
    }

    public static DisposableValue<TValue, TContext> Create<TValue, TContext>(
      [Obfuscation(Exclude = false, Feature = "-rename")] Action<TContext> dispose, TValue value,
      TContext initialContextValue)
    {
      if (dispose == null)
        throw new ArgumentNullException(nameof(dispose));
      return new DisposableValue<TValue, TContext>(value, dispose, initialContextValue);
    }
  }

  /// <summary>
  /// Represents a disposable that does nothing on disposal.
  /// </summary>
  internal sealed class DefaultDisposable : IDisposable
  {
    /// <summary>Singleton default disposable.</summary>
    public static readonly DefaultDisposable Instance = new DefaultDisposable();

    private DefaultDisposable()
    {
    }

    /// <summary>Does nothing.</summary>
    public void Dispose()
    {
    }
  }
  /// <summary>Represents an Action-based disposable.</summary>
  internal sealed class AnonymousDisposable : IDisposable
  {
    private volatile Action? _dispose;

    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _dispose == null;

    /// <summary>
    /// Constructs a new disposable with the given action used for disposal.
    /// </summary>
    /// <param name="dispose">Disposal action which will be run upon calling Dispose.</param>
    public AnonymousDisposable(Action dispose)
    {
      _dispose = dispose;
    }

    /// <summary>
    /// Calls the disposal action if and only if the current instance hasn't been disposed yet.
    /// </summary>
    public void Dispose()
    {
      Action? action = Interlocked.Exchange(ref _dispose, null);
      action?.Invoke();
    }
  }

  /// <summary>
  /// A disposable that can serve a value
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class DisposableValue<T> : IDisposable
  {
    private T value;
    private volatile Action? _dispose;

    /// <summary>
    /// Constructs a new disposable with the given action used for disposal.
    /// </summary>
    /// <param name="dispose">Disposal action which will be run upon calling Dispose.</param>
    public DisposableValue(T value, Action dispose)
    {
      this.value = value;
      _dispose = dispose;
    }

    //public static DisposableValue<T> Empty => new DisposableValue<T>(default, () => { });

    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _dispose == null;

    [Obfuscation(Exclude = false, Feature = "-rename")]
    public T Value
    {
      get
      {
        if (IsDisposed)
          throw new ObjectDisposedException(nameof(this.Value));
        return this.value;
      }
    }

    /// <summary>
    /// Calls the disposal action if and only if the current instance hasn't been disposed yet.
    /// </summary>
    public void Dispose()
    {
      Action? action = Interlocked.Exchange(ref _dispose, null);
      action?.Invoke();
    }
  }

  /// <summary>
  /// a Disposable that can serve a value.
  /// The consumer has also a context value that can be changed and is finally passed to the dispose action
  /// e.g this is useful, if the consumer wants to decide (during the run) to store or discard changes (when hitting the dispose code)
  /// </summary>
  /// <typeparam name="TValue"></typeparam>
  /// <typeparam name="TContext"></typeparam>
  public class DisposableValue<TValue, TContext> : IDisposable
  {
    private TValue value;
    private TContext contextValue;
    private volatile Action<TContext>? _dispose;


    /// <summary>
    /// Constructs a new disposable with the given action used for disposal.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="dispose">Disposal action which will be run upon calling Dispose.</param>
    /// <param name="initialContextValue">is a value that can be set during lifetime and is passed to dispose</param>
    public DisposableValue(TValue value, Action<TContext> dispose, TContext initialContextValue)
    {
      this.value = value;
      _dispose = dispose;
      this.contextValue = initialContextValue;
    }

    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _dispose == null;

    [Obfuscation(Exclude = false, Feature = "-rename")]
    public TValue Value
    {
      get
      {
        if (IsDisposed)
          throw new ObjectDisposedException(nameof(this.Value));
        return this.value;
      }
    }

    [Obfuscation(Exclude = false, Feature = "-rename")]
    public TContext ContextValue
    {
      get => this.contextValue;
      set
      {
        if (IsDisposed)
          throw new ObjectDisposedException(nameof(this.ContextValue));
        this.contextValue = value;
      }
    }

    /// <summary>
    /// Calls the disposal action if and only if the current instance hasn't been disposed yet.
    /// </summary>
    public void Dispose()
    {
      var action = Interlocked.Exchange(ref _dispose, null);
      action?.Invoke(this.contextValue);
    }
  }

  public class Disposables : IDisposable
  {
    private bool isDisposed;
    private List<IDisposable> disposables = new List<IDisposable>();

    public Disposables()
    {
    }

    public Disposables(IEnumerable<IDisposable> disps)
    {
      lock (this)
      {
        disposables.AddRange(disps);
      }
    }

    public void Add(IDisposable disposable)
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        disposables.Add(disposable);
      }
    }

    public void Dispose()
    {
      lock (this)
      {
        if (isDisposed)
          return;

        while (disposables.Any())
        {
          var disposable = disposables.Last();
          disposable.Dispose();
          disposables.Remove(disposable);
        }

        isDisposed = true;
      }
    }
  }

  public class DisposableCollection<T> : IDisposable, ICollection<T> where T : IDisposable
  {
    private bool isDisposed;
    private List<T> disposables = new List<T>();

    public DisposableCollection()
    {
    }

    public DisposableCollection(IEnumerable<T> disps)
    {
      lock (this)
      {
        disposables.AddRange(disps);
      }
    }

    public void Add(T disposable)
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        disposables.Add(disposable);
      }
    }

    public void Clear()
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        disposables.Clear();
      }
    }

    public bool Contains(T item)
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        return disposables.Contains(item);
      }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        disposables.CopyTo(array, arrayIndex);
      }
    }

    public bool Remove(T item)
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        return disposables.Remove(item);
      }
    }

    public int Count
    {
      get
      {
        lock (this)
        {
          if (isDisposed)
            throw new InvalidOperationException("Disposables is already disposed");
          return disposables.Count;
        }
      }
    }

    public bool IsReadOnly
    {
      get
      {
        lock (this)
        {
          if (isDisposed)
            throw new InvalidOperationException("Disposables is already disposed");
          return ((ICollection<T>)disposables).IsReadOnly;
        }
      }
    }

    public void Dispose()
    {
      lock (this)
      {
        if (isDisposed)
          return;

        while (disposables.Any())
        {
          var disposable = disposables.Last();
          disposable.Dispose();
          disposables.Remove(disposable);
        }

        isDisposed = true;
      }
    }

    public IEnumerator<T> GetEnumerator()
    {
      lock (this)
      {
        if (isDisposed)
          throw new InvalidOperationException("Disposables is already disposed");
        return disposables.GetEnumerator();
      }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}

