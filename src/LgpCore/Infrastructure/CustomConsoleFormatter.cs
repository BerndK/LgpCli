using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Infrastructure
{
  public static class CustomConsoleFormatterExtensions
  {
    public static ILoggingBuilder AddCustomFormatter(
      this ILoggingBuilder builder, Action<CustomConsoleFormatterOptions> configure)
    {
      builder.Services.AddSingleton<IExternalScopeProvider, CustomExternalScopeProvider>();
      //return builder.AddConsole(options => options.FormatterName = CustomConsoleFormatter.FormatterName)
      ////builder.AddSimpleConsole(options => options.SingleLine = true)
      //.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>(configure);
      return builder.AddConsole(options =>
        {
          options.FormatterName = CustomConsoleFormatter.FormatterName;
        })
        //.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>(configure)
        ;
    }
  }

  public sealed class CustomConsoleFormatterOptions : ConsoleFormatterOptions
  {
    public string? ScopePrefixPushed { get; set; } = "==> ";
    public string? ScopePrefixReleased { get; set; } = "<== ";
    public int Indentation { get; set; } = 2;
  }

  public sealed class CustomConsoleFormatter : ConsoleFormatter, IDisposable
  {
    private readonly IDisposable? _optionsReloadToken;
    private CustomConsoleFormatterOptions _formatterOptions;
    public const string FormatterName = "customConsole";

    public CustomConsoleFormatter(IOptionsMonitor<CustomConsoleFormatterOptions> options)
      : base(FormatterName)
    {
      (_optionsReloadToken, _formatterOptions) = (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
    }

    private void ReloadLoggerOptions(CustomConsoleFormatterOptions options) =>
      _formatterOptions = options;

    private bool scopeHandlerInstalled = false;
    public override void Write<TState>(
      in LogEntry<TState> logEntry,
      IExternalScopeProvider? scopeProvider,
      TextWriter textWriter)
    {
      if (!scopeHandlerInstalled && scopeProvider is ICustomExternalScopeProvider cesp)
      {
        //do the scopes for first time
        int count = 0;
        scopeProvider.ForEachScope((o, state) =>
        {
          ScopeHandler(o, Indent(count), textWriter, scopeProvider, true);
          count++;
        }, (object?)null);

        cesp.ScopeChanged += (state, pushed) => ScopeHandler(state, Indent(scopeProvider), textWriter, scopeProvider, pushed);
        scopeHandlerInstalled = true;
      }
      
      string? message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);

      if (message is null)
      {
        return;
      }

      //textWriter.Write("*");
      textWriter.Write(Indent(scopeProvider));
      textWriter.WriteLine(message);
    }

    private string Indent(IExternalScopeProvider? scopeProvider)
    {
      if (scopeProvider == null)
        return string.Empty;
      int count = 0;
      if (scopeProvider is ICustomExternalScopeProvider cesp)
        count = cesp.Level;
      else
        scopeProvider.ForEachScope((o, state) => count++, (object?)null);
      return Indent(count);
    }

    private string Indent(int count) => new string(' ', count * _formatterOptions.Indentation);
    private void ScopeHandler(object? o, string indent, TextWriter textWriter, IExternalScopeProvider? scopeProvider, bool pushed)
    {
      textWriter.WriteLine($"{indent}{(pushed ? _formatterOptions.ScopePrefixPushed : _formatterOptions.ScopePrefixReleased)}{o}");
    }

    public void Dispose()
    {
      _optionsReloadToken?.Dispose();
    }
  }
}
