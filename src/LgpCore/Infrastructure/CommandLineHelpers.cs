using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace LgpCore.Infrastructure
{
  public static partial class CommandLineExtensions
  {
    public static T? GetValueForHandlerParameter<T>(this IValueDescriptor<T> symbol, InvocationContext context)
    {
      if (symbol is IValueSource valueSource &&
          valueSource.TryGetValue(symbol, context.BindingContext, out var boundValue) &&
          boundValue is T value)
      {
        return value;
      }
      else
      {
        switch (symbol)
        {
          case Argument<T> argument:
            return context.ParseResult.GetValueForArgument(argument);
          case Option<T> option:
            return context.ParseResult.GetValueForOption(option);
          //case ContextProviderBinder<T> contextProviderBinder:
          //case BinderBase<T> binderBase:
          default:
            throw new ArgumentOutOfRangeException(nameof(symbol));
        }
      }
    }
    public static object? GetValueForHandlerParameter(this IValueDescriptor symbol, InvocationContext context)
    {
      if (symbol is IValueSource valueSource &&
          valueSource.TryGetValue(symbol, context.BindingContext, out var boundValue))
      {
        return boundValue;
      }
      else
      {
        switch (symbol)
        {
          case Argument argument:
            return context.ParseResult.GetValueForArgument(argument);
          case Option option:
            return context.ParseResult.GetValueForOption(option);
          //case ContextProviderBinder<T> contextProviderBinder:
          //case BinderBase<T> binderBase:
          default:
            throw new ArgumentOutOfRangeException(nameof(symbol));
        }
      }
    }

    public static string ShortestAlias(this IdentifierSymbol symbol)
    {
      var shortest = symbol.Name;
      foreach (var alias in symbol.Aliases)
      {
        if (alias.Length < shortest.Length)
          shortest = alias;
      }
      return shortest;
    }

    [LibraryImport("shell32.dll", EntryPoint = "CommandLineToArgvW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CommandLineToArgv(string lpCmdLine, out int pNumArgs);

    //use this to split a commandline string into arguments, it seems to be more reliable than the built-in CommandLine.SplitCommandLine
    public static string[] CommandLineToArgs(string? commandLine)
    {
      if (string.IsNullOrEmpty(commandLine)) { return []; }

      var argsPtr = CommandLineToArgv(commandLine, out var argc);
      if (argsPtr == IntPtr.Zero)
      {
        throw new Win32Exception(Marshal.GetLastWin32Error());
      }
      try
      {
        var result = new string[argc];
        for (var i = 0; i < result.Length; ++i)
        {
          var ptr = Marshal.ReadIntPtr(argsPtr, i * IntPtr.Size);
          if (ptr == IntPtr.Zero)
            throw new InvalidOperationException();
          result[i] = Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        return result;
      }
      finally
      {
        Marshal.FreeHGlobal(argsPtr);
      }
    }
  }

  /// <summary>
  /// Used to bind a service from the dependency injection container to a commandline argument
  /// NOTE: this is a simple implementation and does not support multiple services of the same type because this uses the ServiceProvider built into System.CommandLine
  /// use this to register the wanted service in the BindingContext's (simple fake) ServiceProvider
  ///
  /// <code>
  /// var builder = new CommandLineBuilder(rootCommand)
  ///    .UseDefaults()
  ///    .UseService&lt;IServiceProvider&gt;(() => myInstanceOfRealServiceProvider);
  /// builder.Build().Invoke(args);</code>
  ///
  /// use this to bind the service to the commandline argument:
  /// <code>var serviceProviderBinder = new ContextProviderBinder&lt;IServiceProvider&gt;();</code>
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ContextProviderBinder<T> : BinderBase<T> where T : notnull
  {
    protected override T GetBoundValue(BindingContext bindingContext) => bindingContext.GetRequiredService<T>();
  }

  public interface ISymbolProviderToAddToCommand
  {
    IEnumerable<IValueDescriptor> SymbolsToAdd { get; }
  }

  /// <summary>
  /// Used to build multi-value arguments for the commandline (see command enable).
  /// define two options
  /// might look like this: --key key1 --value value1 --key key2 --value value2 value3
  /// this will produce a list of tuples with the key and a list of values: <code>List&lt;(TKey, List&lt;TValue&gt;)&gt;</code>
  /// </summary>
  public class KeyValuePairsBinder<TKey, TValue> : BinderBase<List<(TKey, List<TValue>)>>, ISymbolProviderToAddToCommand
  {
    public Option<TKey[]> KeyOption { get; }
    public Option<TValue[]> ValueOption { get; }

    public KeyValuePairsBinder(Option<TKey[]> keyOption, Option<TValue[]> valueOption)
    {
      KeyOption = keyOption;
      ValueOption = valueOption;
    }

    protected override List<(TKey, List<TValue>)> GetBoundValue(BindingContext bindingContext)
    {
      var result = new List<(TKey, List<TValue>)>();
      var keyParseResult = bindingContext.ParseResult.FindResultFor(KeyOption);
      var valueParseResult = bindingContext.ParseResult.FindResultFor(ValueOption);
      if (keyParseResult == null ^ valueParseResult == null)
        throw new ArgumentException($"the options '{KeyOption.Name}' ({KeyOption.Aliases.FirstOrDefault()}) and '{ValueOption.Name}' ({ValueOption.Aliases.FirstOrDefault()}) needs to be provided in pairs");

      if (keyParseResult != null && keyParseResult.Tokens.Any())
      {
        var tokenIndexes = bindingContext.ParseResult.Tokens
            .Where(t => t.Type == TokenType.Argument)
            .Index()
            .ToList()
            .ToDictionary(e => e.Item, e => e.Index, GenericReferenceEqualityComparer<Token>.Instance);
        var keyTokens = new Stack<(int index, Token token)>(keyParseResult.Tokens.Index().ToList());
        var keyValues = keyParseResult.GetValueForOption(KeyOption);
        if (keyValues == null || keyValues.Length != keyTokens.Count)
          throw new InvalidOperationException();
        var valueTokens = new Stack<(int index, Token token)>(valueParseResult!.Tokens.Index().ToList());
        var valueValues = valueParseResult.GetValueForOption(ValueOption) ?? throw new InvalidOperationException();
        if (valueValues == null || valueValues.Length != valueTokens.Count)
          throw new InvalidOperationException();
        while (keyTokens.TryPop(out var keyToken))
        {
          var keyTokenIdx = tokenIndexes[keyToken.token];
          var locValues = new List<TValue>();
          while (valueTokens.TryPeek(out var valueToken))
          {
            var valueTokenIdx = tokenIndexes[valueToken.token];
            if (valueTokenIdx < keyTokenIdx)
              break;
            valueTokens.Pop();
            locValues.Add(valueValues[valueToken.index]);
          }
          if (!locValues.Any())
            throw new ArgumentException($"no value provided for key '{keyToken.token.Value}'");
          result.Insert(0, (keyValues[keyToken.index], locValues));
        }
      }

      return result;
    }

    public IEnumerable<IValueDescriptor> SymbolsToAdd
    {
      get
      {
        yield return KeyOption;
        yield return ValueOption;
      }
    }
  }

  public static class DependencyInjectionMiddleware
  {
    //public static CommandLineBuilder UseServiceProvider(this CommandLineBuilder builder, Func<IServiceProvider> serviceProviderFunc)
    //{
    //  return builder.AddMiddleware(async (context, next) =>
    //  {
    //    context.BindingContext.AddService<IServiceProvider>(provider => serviceProviderFunc());
    //    await next(context);
    //  });
    //}

    public static CommandLineBuilder UseService<T>(this CommandLineBuilder builder, Func<T> serviceProviderFunc)
    {
      return builder.AddMiddleware(async (context, next) =>
      {
        context.BindingContext.AddService(provider => serviceProviderFunc());
        await next(context);
      });
    }
  }

  public abstract class TypedCommand : Command
  {
    protected TypedCommand(string name, string? description = null) : base(name, description)
    {
    }

    protected void AddFromConstructor(IValueDescriptor symbol)
    {
      if (symbol is Argument argument && !Arguments.Contains(argument))
        Add(argument);
      if (symbol is Option option && !Options.Contains(option))
        Add(option);
      if (symbol is ISymbolProviderToAddToCommand provider)
        foreach (var s in provider.SymbolsToAdd)
          AddFromConstructor(s);
    }

    public static Command<T1> Create<T1>(string name, string? description, IValueDescriptor<T1> symbol1) =>
      new(name, description, symbol1);
    public static Command<T1, T2> Create<T1, T2>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2) =>
      new(name, description, symbol1, symbol2);
    public static Command<T1, T2, T3> Create<T1, T2, T3>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3) =>
      new(name, description, symbol1, symbol2, symbol3);

    public static Command<T1, T2, T3, T4> Create<T1, T2, T3, T4>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3, IValueDescriptor<T4> symbol4) =>
      new(name, description, symbol1, symbol2, symbol3, symbol4);

    public static Command<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3, IValueDescriptor<T4> symbol4, IValueDescriptor<T5> symbol5) =>
      new(name, description, symbol1, symbol2, symbol3, symbol4, symbol5);

    public static Command<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3, IValueDescriptor<T4> symbol4, IValueDescriptor<T5> symbol5, IValueDescriptor<T6> symbol6) =>
      new(name, description, symbol1, symbol2, symbol3, symbol4, symbol5, symbol6);

    public static Command<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3, IValueDescriptor<T4> symbol4, IValueDescriptor<T5> symbol5, IValueDescriptor<T6> symbol6, IValueDescriptor<T7> symbol7) =>
      new(name, description, symbol1, symbol2, symbol3, symbol4, symbol5, symbol6, symbol7);

    public static Command<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(string name, string? description, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2, IValueDescriptor<T3> symbol3, IValueDescriptor<T4> symbol4, IValueDescriptor<T5> symbol5, IValueDescriptor<T6> symbol6, IValueDescriptor<T7> symbol7, IValueDescriptor<T8> symbol8) =>
      new(name, description, symbol1, symbol2, symbol3, symbol4, symbol5, symbol6, symbol7, symbol8);
  }

  public class Command<T1> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }

    public Command(string name, string? description, IValueDescriptor<T1> symbol1) : base(name, description)
    {
      Symbol1 = symbol1;
      AddFromConstructor(symbol1);
    }

    public void SetHandler(Action<T1> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        handler(value1!);
      });
    }
  }

  public class Command<T1, T2> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }

    public Command(string name, string? description,
      IValueDescriptor<T1> symbol1,
      IValueDescriptor<T2> symbol2) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
    }

    public void SetHandler(Action<T1, T2> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        handler(value1!, value2!);
      });
    }
  }

  public class Command<T1, T2, T3> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
    }

    public void SetHandler(Action<T1, T2, T3> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!);
      });
    }
  }

  public class Command<T1, T2, T3, T4> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }
    private IValueDescriptor<T4> Symbol4 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3,
        IValueDescriptor<T4> symbol4) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      Symbol4 = symbol4;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
      AddFromConstructor(symbol4);
    }

    public void SetHandler(Action<T1, T2, T3, T4> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        var value4 = Symbol4.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!, value4!);
      });
    }
  }

  public class Command<T1, T2, T3, T4, T5> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }
    private IValueDescriptor<T4> Symbol4 { get; }
    private IValueDescriptor<T5> Symbol5 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3,
        IValueDescriptor<T4> symbol4,
        IValueDescriptor<T5> symbol5) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      Symbol4 = symbol4;
      Symbol5 = symbol5;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
      AddFromConstructor(symbol4);
      AddFromConstructor(symbol5);
    }

    public void SetHandler(Action<T1, T2, T3, T4, T5> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        var value4 = Symbol4.GetValueForHandlerParameter(context);
        var value5 = Symbol5.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!, value4!, value5!);
      });
    }
  }

  public class Command<T1, T2, T3, T4, T5, T6> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }
    private IValueDescriptor<T4> Symbol4 { get; }
    private IValueDescriptor<T5> Symbol5 { get; }
    private IValueDescriptor<T6> Symbol6 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3,
        IValueDescriptor<T4> symbol4,
        IValueDescriptor<T5> symbol5,
        IValueDescriptor<T6> symbol6) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      Symbol4 = symbol4;
      Symbol5 = symbol5;
      Symbol6 = symbol6;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
      AddFromConstructor(symbol4);
      AddFromConstructor(symbol5);
      AddFromConstructor(symbol6);
    }

    public void SetHandler(Action<T1, T2, T3, T4, T5, T6> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        var value4 = Symbol4.GetValueForHandlerParameter(context);
        var value5 = Symbol5.GetValueForHandlerParameter(context);
        var value6 = Symbol6.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!, value4!, value5!, value6!);
      });
    }
  }

  public class Command<T1, T2, T3, T4, T5, T6, T7> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }
    private IValueDescriptor<T4> Symbol4 { get; }
    private IValueDescriptor<T5> Symbol5 { get; }
    private IValueDescriptor<T6> Symbol6 { get; }
    private IValueDescriptor<T7> Symbol7 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3,
        IValueDescriptor<T4> symbol4,
        IValueDescriptor<T5> symbol5,
        IValueDescriptor<T6> symbol6,
        IValueDescriptor<T7> symbol7) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      Symbol4 = symbol4;
      Symbol5 = symbol5;
      Symbol6 = symbol6;
      Symbol7 = symbol7;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
      AddFromConstructor(symbol4);
      AddFromConstructor(symbol5);
      AddFromConstructor(symbol6);
      AddFromConstructor(symbol7);
    }

    public void SetHandler(Action<T1, T2, T3, T4, T5, T6, T7> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        var value4 = Symbol4.GetValueForHandlerParameter(context);
        var value5 = Symbol5.GetValueForHandlerParameter(context);
        var value6 = Symbol6.GetValueForHandlerParameter(context);
        var value7 = Symbol7.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!, value4!, value5!, value6!, value7!);
      });
    }
  }

  public class Command<T1, T2, T3, T4, T5, T6, T7, T8> : TypedCommand
  {
    private IValueDescriptor<T1> Symbol1 { get; }
    private IValueDescriptor<T2> Symbol2 { get; }
    private IValueDescriptor<T3> Symbol3 { get; }
    private IValueDescriptor<T4> Symbol4 { get; }
    private IValueDescriptor<T5> Symbol5 { get; }
    private IValueDescriptor<T6> Symbol6 { get; }
    private IValueDescriptor<T7> Symbol7 { get; }
    private IValueDescriptor<T8> Symbol8 { get; }

    public Command(string name, string? description,
        IValueDescriptor<T1> symbol1,
        IValueDescriptor<T2> symbol2,
        IValueDescriptor<T3> symbol3,
        IValueDescriptor<T4> symbol4,
        IValueDescriptor<T5> symbol5,
        IValueDescriptor<T6> symbol6,
        IValueDescriptor<T7> symbol7,
        IValueDescriptor<T8> symbol8) : base(name, description)
    {
      Symbol1 = symbol1;
      Symbol2 = symbol2;
      Symbol3 = symbol3;
      Symbol4 = symbol4;
      Symbol5 = symbol5;
      Symbol6 = symbol6;
      Symbol7 = symbol7;
      Symbol8 = symbol8;
      AddFromConstructor(symbol1);
      AddFromConstructor(symbol2);
      AddFromConstructor(symbol3);
      AddFromConstructor(symbol4);
      AddFromConstructor(symbol5);
      AddFromConstructor(symbol6);
      AddFromConstructor(symbol7);
      AddFromConstructor(symbol8);
    }

    public void SetHandler(Action<T1, T2, T3, T4, T5, T6, T7, T8> handler)
    {
      Handler = new MyAnonymousCommandHandler(context =>
      {
        var value1 = Symbol1.GetValueForHandlerParameter(context);
        var value2 = Symbol2.GetValueForHandlerParameter(context);
        var value3 = Symbol3.GetValueForHandlerParameter(context);
        var value4 = Symbol4.GetValueForHandlerParameter(context);
        var value5 = Symbol5.GetValueForHandlerParameter(context);
        var value6 = Symbol6.GetValueForHandlerParameter(context);
        var value7 = Symbol7.GetValueForHandlerParameter(context);
        var value8 = Symbol8.GetValueForHandlerParameter(context);
        handler(value1!, value2!, value3!, value4!, value5!, value6!, value7!, value8!);
      });
    }
  }
  internal class MyAnonymousCommandHandler : ICommandHandler
  {
    private readonly Func<InvocationContext, Task>? _asyncHandle;
    private readonly Action<InvocationContext>? _syncHandle;

    public MyAnonymousCommandHandler(Func<InvocationContext, Task> handle)
      => _asyncHandle = handle ?? throw new ArgumentNullException(nameof(handle));

    public MyAnonymousCommandHandler(Action<InvocationContext> handle)
      => _syncHandle = handle ?? throw new ArgumentNullException(nameof(handle));

    public int Invoke(InvocationContext context)
    {
      if (_syncHandle is not null)
      {
        _syncHandle(context);
        return context.ExitCode;
      }

      return SyncUsingAsync(context); // kept in a separate method to avoid JITting
    }

    private int SyncUsingAsync(InvocationContext context) => InvokeAsync(context).GetAwaiter().GetResult();

    public async Task<int> InvokeAsync(InvocationContext context)
    {
      if (_syncHandle is not null)
      {
        return Invoke(context);
      }

      object returnValue = _asyncHandle!(context);

      int ret;

      switch (returnValue)
      {
        case Task<int> exitCodeTask:
          ret = await exitCodeTask;
          break;
        case Task task:
          await task;
          ret = context.ExitCode;
          break;
        case int exitCode:
          ret = exitCode;
          break;
        default:
          ret = context.ExitCode;
          break;
      }

      return ret;
    }

  }
}
