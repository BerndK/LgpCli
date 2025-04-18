using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Infrastructure
{
  public delegate void ScopeChangedDelegate(object? state, bool pushed);

  public interface ICustomExternalScopeProvider: IExternalScopeProvider
  {
    int Level { get; }
    event ScopeChangedDelegate? ScopeChanged;
  }

  public class CustomExternalScopeProvider : ICustomExternalScopeProvider
  {
    private readonly LoggerExternalScopeProvider scopeProvider;

    public CustomExternalScopeProvider()
    {
      scopeProvider = new LoggerExternalScopeProvider();
    }

    public int Level { get; private set; }

    public event ScopeChangedDelegate? ScopeChanged;

    public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
    {
      scopeProvider.ForEachScope(callback, state);
    }

    public IDisposable Push(object? state)
    {
      var scopeDisp = scopeProvider.Push(state);
      OnScopeChanged(state, true);
      Level++;
      return Disposable.Create(() =>
      {
        Level--;
        OnScopeChanged(state, false);
        scopeDisp.Dispose();
      });
    }

    protected virtual void OnScopeChanged(object? state, bool pushed)
    {
      ScopeChanged?.Invoke(state, pushed);
    }
  }
}
