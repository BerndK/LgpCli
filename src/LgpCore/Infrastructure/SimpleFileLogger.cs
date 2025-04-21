using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Logging.SimpleFile
{
  //ToDo: 1BEKA move this to a package

  public static class SimpleFileLoggerFactoryExtensions
  {
    /// <summary>
    /// Adds a debug logger named 'Debug' to the factory.
    /// </summary>
    /// <param name="builder">The extension method argument.</param>
    //?[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SimpleFileFormatterOptions))]
    public static ILoggingBuilder AddSimpleFile(this ILoggingBuilder builder)
    {
      builder.Services.TryAddSingleton<SimpleFileLoggerFormatterBase, SimpleFileLoggerFormatter>();
      builder.Services.TryAddSingleton<SimpleFileRollingOptions>();
      builder.Services.TryAddSingleton<IAssemblyInfoProvider, EntryAssemblyInfoProvider>();
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SimpleFileLoggerProvider>());

      return builder;
    }

    //?[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SimpleFileFormatterOptions))]
    public static ILoggingBuilder AddSimpleFile(this ILoggingBuilder builder, SimpleFileRollingOptions options, IAssemblyInfoProvider? assemblyInfoProvider, SimpleFileLoggerFormatterBase? formatter)
    {
      if (builder == null) throw new ArgumentNullException(nameof(builder));
      if (options == null) throw new ArgumentNullException(nameof(options));

      if (formatter != null)
        builder.Services.TryAddSingleton<SimpleFileLoggerFormatterBase>(formatter);
      builder.Services.TryAddSingleton<SimpleFileRollingOptions>(options);
      if (assemblyInfoProvider != null)
        builder.Services.TryAddSingleton<IAssemblyInfoProvider>(assemblyInfoProvider);
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SimpleFileLoggerProvider>());

      return builder;
    }

    /// <summary>
    /// Adds an event logger. Use <paramref name="configure"/> to enable logging for specific <see cref="LogLevel"/>s.
    /// </summary>
    /// <param name="builder">The extension method argument.</param>
    /// <param name="configure">A delegate to configure the <see cref="SimpleFileRollingOptions"/>.</param>
    /// <returns>The <see cref="ILoggingBuilder"/> so that additional calls can be chained.</returns>
    //?[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SimpleFileFormatterOptions))]
    public static ILoggingBuilder AddSimpleFile(this ILoggingBuilder builder, Action<SimpleFileRollingOptions> configure)
    {
      if (configure == null) throw new ArgumentNullException(nameof(configure));

      builder.AddSimpleFile();
      builder.Services.Configure(configure);

      return builder;
    }
  }

  [ProviderAlias("SimpleFile")]
  public class SimpleFileLoggerProvider : ILoggerProvider, ISupportExternalScope, IDisposable
  {
    private readonly SimpleFileLoggerFormatterBase formatter;
    private readonly IAssemblyInfoProvider assemblyInfoProvider;
    internal SimpleFileRollingOptions Options;
    private readonly IDisposable? optionsReloadToken;


    private IExternalScopeProvider? scopeProvider;
    private SimpleFileRolling? simpleFileRolling;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SimpleFileFormatterOptions))]
    public SimpleFileLoggerProvider(
      SimpleFileLoggerFormatterBase formatter,
      IOptionsMonitor<SimpleFileRollingOptions> options,
      IAssemblyInfoProvider assemblyInfoProvider)
    {
      this.formatter = formatter;
      this.assemblyInfoProvider = assemblyInfoProvider;
    
      Options = null!; //will be initialized via ReloadLoggerOptions
      ReloadLoggerOptions(options.CurrentValue);
      optionsReloadToken = options.OnChange(ReloadLoggerOptions);
    }

    private void ReloadLoggerOptions(SimpleFileRollingOptions options)
    {
      this.Options = options;
    }

    //public SimpleFileLoggerProvider(IOptions<SimpleFileLogOptions> options, IAssemblyInfoProvider? assemblyInfoProvider)
    //  : this(options.Value, assemblyInfoProvider)
    //{
    //}

    /// <inheritdoc />
    public ILogger CreateLogger(string name)
    {
      return new SimpleFileLogger(name, formatter, scopeProvider, Options, simpleFileRolling ??= SimpleFileRolling.Instance(this.Options, this.assemblyInfoProvider));
    }

    /// <inheritdoc />
    public void Dispose()
    {
      optionsReloadToken?.Dispose();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
      this.scopeProvider = scopeProvider;
    }
  }

  public class SimpleFileLogger : ILogger
  {
    private readonly string name;
    private readonly SimpleFileLoggerFormatterBase formatter;
    private readonly IExternalScopeProvider? externalScopeProvider;
    private readonly SimpleFileRollingOptions options;
    private readonly SimpleFileRolling simpleFileRolling;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleFileLogger"/> class.
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="options">The <see cref="SimpleFileRollingOptions"/>.</param>
    /// <param name="formatter">Formatter to format the text</param>
    /// <param name="externalScopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
    /// <param name="simpleFileRolling"></param>
    internal SimpleFileLogger(
      string name, 
      SimpleFileLoggerFormatterBase formatter,
      IExternalScopeProvider? externalScopeProvider,
      SimpleFileRollingOptions options, 
      SimpleFileRolling simpleFileRolling)
    {
      this.name = name ?? throw new ArgumentNullException(nameof(name));
      this.formatter = formatter;

      this.options = options ?? throw new ArgumentNullException(nameof(options));

      this.externalScopeProvider = externalScopeProvider;
      this.simpleFileRolling = simpleFileRolling ?? throw new ArgumentNullException(nameof(simpleFileRolling));
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
      return externalScopeProvider?.Push(state);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
      return logLevel != LogLevel.None;
    }

    /// <inheritdoc />
    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatterFunc)
    {
      if (!IsEnabled(logLevel))
      {
        return;
      }

      if (formatter == null) 
        throw new InvalidOperationException("Formatter not set");

      var sb = new StringBuilder();

      LogEntry<TState> logEntry = new LogEntry<TState>(logLevel, name, eventId, state, exception, formatterFunc);
      this.formatter.Write(in logEntry, externalScopeProvider, sb);

      if (sb.Length == 0)
      {
        return;
      }
      
      this.simpleFileRolling.WriteLine(sb.ToString());
    }

    internal static string? BuildScope(IExternalScopeProvider scopeProvider)
    {
      var builder = new StringBuilder();
      bool hasScope = false;
      scopeProvider.ForEachScope((scope, sb) =>
        {
          hasScope = true;
          sb.Append("/");
          if (scope is IEnumerable<KeyValuePair<string, object>> properties)
          {
            sb.Append(string.Join(";", properties.Select(e => $"{e.Key}:{e.Value?.ToString()}")));
          }
          else if (scope != null)
          {
            sb.Append(scope.ToString());
          }
        },
        builder);
      if (hasScope)
        return builder.ToString();
      return null;
    }
  }

  #region Formatter
  /// <summary>
  /// Allows custom log messages formatting
  /// </summary>
  public abstract class SimpleFileLoggerFormatterBase
  {
    /// <summary>
    /// Initializes a new instance of <see cref="SimpleFileLoggerFormatterBase"/>.
    /// </summary>
    /// <param name="name"></param>
    protected SimpleFileLoggerFormatterBase(string name)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the name associated with the simple file log formatter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Writes the log message to the specified TextWriter.
    /// </summary>
    /// <remarks>
    /// if the formatter wants to write colors to the console, it can do so by embedding ANSI color codes into the string
    /// </remarks>
    /// <param name="logEntry">The log entry.</param>
    /// <param name="scopeProvider">The provider of scope data.</param>
    /// <param name="sb">The string builder for output.</param>
    /// <typeparam name="TState">The type of the object to be written.</typeparam>
    public abstract void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, StringBuilder sb);
  }

  internal sealed class SimpleFileLoggerFormatter : SimpleFileLoggerFormatterBase, IDisposable
  {
    private const string LoglevelPadding = ": ";
    private static readonly string messagePadding = new string(' ', GetLogLevelString(LogLevel.Information).Length + LoglevelPadding.Length);
    private static readonly string newLineWithMessagePadding = Environment.NewLine + messagePadding;

    private readonly IDisposable? optionsReloadToken;

    public SimpleFileLoggerFormatter(IOptionsMonitor<SimpleFileFormatterOptions> options)
        : base("simple")
    {
      FormatterOptions = null!; //will be initialized via ReloadLoggerOptions
      ReloadLoggerOptions(options.CurrentValue);
      optionsReloadToken = options.OnChange(ReloadLoggerOptions);
    }

    private void ReloadLoggerOptions(SimpleFileFormatterOptions options)
    {
      FormatterOptions = options;
    }

    public void Dispose()
    {
      optionsReloadToken?.Dispose();
    }

    internal SimpleFileFormatterOptions FormatterOptions { get; set; }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, StringBuilder sb)
    {
      string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
      if (logEntry.Exception == null && message == null)
      {
        return;
      }
      LogLevel logLevel = logEntry.LogLevel;
      string logLevelString = GetLogLevelString(logLevel);

      string? timestamp = null;
      string? timestampFormat = FormatterOptions.TimestampFormat;
      if (timestampFormat != null)
      {
        DateTimeOffset dateTimeOffset = GetCurrentDateTime();
        timestamp = dateTimeOffset.ToString(timestampFormat);
      }
      if (timestamp != null)
      {
        sb.Append(timestamp);
        if (timestamp.LastOrDefault() != ' ')
          sb.Append(" ");
      }
      
      sb.Append(logLevelString);

      CreateDefaultLogMessage(sb, logEntry, message, scopeProvider);
    }

    private void CreateDefaultLogMessage<TState>(StringBuilder sb, in LogEntry<TState> logEntry, string message, IExternalScopeProvider? scopeProvider)
    {
      bool singleLine = FormatterOptions.SingleLine;
      bool separatorLine = FormatterOptions.SeparatorLine;
      int eventId = logEntry.EventId.Id;
      Exception? exception = logEntry.Exception;

      // Example:
      // info: ConsoleApp.Program[10]
      //       Request received

      // category and event id
      sb.Append(LoglevelPadding);
      sb.Append(logEntry.Category);
      if (eventId != 0)
      {
        sb.Append('[');

#if NETCOREAPP
        Span<char> span = stackalloc char[10];
        if (eventId.TryFormat(span, out int charsWritten))
          sb.Append(span.Slice(0, charsWritten));
        else
#endif
          sb.Append(eventId.ToString());

        sb.Append(']');
      }

      if (!singleLine)
      {
        sb.AppendLine();
      }

      // scope information
      WriteScopeInformation(sb, scopeProvider, singleLine);
      WriteMessage(sb, message, singleLine);

      // Example:
      // System.InvalidOperationException
      //    at Namespace.Class.Function() in File:line X
      if (exception != null)
      {
        // exception message
        WriteMessage(sb, exception.ToString(), singleLine);
      }
      if (singleLine && separatorLine)
      {
        sb.AppendLine();
      }
    }

    private static void WriteMessage(StringBuilder sb, string message, bool singleLine)
    {
      if (!string.IsNullOrEmpty(message))
      {
        if (singleLine)
        {
          sb.Append(' ');
          WriteReplacing(sb, Environment.NewLine, " ", message);
        }
        else
        {
          sb.Append(messagePadding);
          WriteReplacing(sb, Environment.NewLine, newLineWithMessagePadding, message);
          sb.AppendLine();
        }
      }

      static void WriteReplacing(StringBuilder sb, string oldValue, string newValue, string message)
      {
        string newMessage = message.Replace(oldValue, newValue);
        sb.Append(newMessage);
      }
    }

    private DateTimeOffset GetCurrentDateTime()
    {
      return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
      return logLevel switch
      {
        //LogLevel.Trace => "trce",
        //LogLevel.Debug => "dbug",
        //LogLevel.Information => "info",
        //LogLevel.Warning => "warn",
        //LogLevel.Error => "fail",
        //LogLevel.Critical => "crit",

        //this better supported by VS Code (SyntaxHighlighting)
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
      };
    }

    private void WriteScopeInformation(StringBuilder sb, IExternalScopeProvider? scopeProvider, bool singleLine)
    {
      if (FormatterOptions.IncludeScopes && scopeProvider != null)
      {
        bool paddingNeeded = !singleLine;
        scopeProvider.ForEachScope((scope, state) =>
        {
          ////scope starts with =>
          //if (paddingNeeded)
          //{
          //  paddingNeeded = false;
          //  state.Append(messagePadding);
          //  state.Append("=> ");
          //}
          //else
          //{
          //  state.Append(" => ");
          //}
          //state.Append(scope);

          //scope ends with '=> '
          if (paddingNeeded)
          {
            paddingNeeded = false;
            state.Append(messagePadding);
          }
          else
            state.Append(" ");
          state.Append(scope);
          state.Append("=> ");

        }, sb);

        if (!paddingNeeded && !singleLine)
        {
          sb.AppendLine();
        }
      }
    }

    private readonly struct ConsoleColors
    {
      public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
      {
        Foreground = foreground;
        Background = background;
      }

      public ConsoleColor? Foreground { get; }

      public ConsoleColor? Background { get; }
    }
  }
  #endregion Formatter

  #region FormatterOptions
  /// <summary>
  /// Options for the built-in console log formatter.
  /// </summary>
  public class SimpleFileFormatterOptions
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleFileFormatterOptions"/> class.
    /// </summary>
    public SimpleFileFormatterOptions() { }

    /// <summary>
    /// When <see langword="true" />, the entire message gets logged in a single line.
    /// </summary>
    public bool SingleLine { get; set; } = true;

    /// <summary>
    /// When <see langword="true" />, the messages are separated by a blank line.
    /// </summary>
    public bool SeparatorLine { get; set; }

    /// <summary>
    /// Includes scopes when <see langword="true" />.
    /// </summary>
    public bool IncludeScopes { get; set; }

    /// <summary>
    /// Gets or sets format string used to format timestamp in logging messages. Defaults to <c>"O"</c> (2024-07-27T13:56:17.2504385+02:00).
    /// </summary>
    //[StringSyntax(StringSyntaxAttribute.DateTimeFormat)]
    public string? TimestampFormat { get; set; } = "O"; //2024-07-27T13:56:17.2504385+02:00

    /// <summary>
    /// Gets or sets indication whether or not UTC timezone should be used to format timestamps in logging messages. Defaults to <c>false</c>.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }

    //internal virtual void Configure(IConfiguration configuration) => configuration.Bind(this);
  }
  #endregion FormatterOptions

}
