using Microsoft.Extensions.Configuration;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LgpCore.AdmParser;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace LgpCore
{
  public class BatchCmd
  {
    public const string batchCmdFileValueName = "batchCmdFile";
    private readonly IConfigurationSection? appSection;
    private readonly CommandLine commandLine;
    private readonly Parser parser;
    private FileInfo? batchFile;

    private BatchCmd(IServiceProvider serviceProvider)
    {
      this.commandLine = serviceProvider.GetRequiredService<CommandLine>();
      this.parser = this.commandLine.Build(serviceProvider);
    }

    public BatchCmd([FromKeyedServices("app")]IConfigurationSection appSection, IServiceProvider serviceProvider) : this(serviceProvider)
    {
      this.appSection = appSection;
    }

    public static BatchCmd Create(IServiceProvider serviceProvider, FileInfo batchFile)
    {
      var result = new BatchCmd(serviceProvider);
      result.batchFile = batchFile;
      return result;
    }

    public FileInfo? CurrentFile
    {
      get
      {
        if (appSection != null && batchFile != null)
          throw new InvalidOperationException("appSection and batchFile are both set");
        if (batchFile != null)
          return batchFile;
        if (appSection != null)
        {
          var path = appSection[batchCmdFileValueName];
          if (string.IsNullOrWhiteSpace(path))
            return null;
          return new FileInfo(path);
        }

        return null;
      }
      set
      {
        if (appSection == null)
          throw new InvalidOperationException("appSection is null");
        appSection[batchCmdFileValueName] = value?.FullName;
        appSection.SaveJsonProvider();
      }
    }

    public bool? CanWrite
    {
      get
      {
        var currentFile = CurrentFile;
        if (currentFile == null)
          return null;
        try
        {
          using (var fs = File.Open(currentFile.FullName, FileMode.OpenOrCreate))
          {
            return fs.CanWrite;
          }
        }
        catch (UnauthorizedAccessException)
        {
          return false;
        }
      }
    }

    public List<string>? Lines
    {
      get
      {
        var currentFile = CurrentFile;
        if (!(currentFile?.Exists ?? false))
          return null;

        return File.ReadLines(currentFile.FullName).ToList();
      }
    }

    public List<BatchLineInfo>? GetCommandInfos(bool includeFilteredLines)
    {
      return Lines?
        .Select(l => l.Trim())
        .Index()
        .Select(e => ParseCommandLine(this.parser, e.Item, e.Index))
        .Where(e => includeFilteredLines || !e.Filtered)  
        .ToList();
    }

    public class BatchLineInfo
    {
      public required string RawLine { get; init; }
      public required int Index { get; init; }
      public string? TrimmedLine { get; set; }
      public string? CommandName { get; set; }
      public string? PolicyPrefixedName { get; set; }
      public PolicyClass? PolicyClass { get; set; }
      public List<(string, List<string>)>? ElementValues { get; set; }
      public List<string>? Errors { get; set; }
      public bool Filtered { get; set; }
    }

    public static BatchLineInfo ParseCommandLine(Parser parser, string rawLine, int index)
    { 
      var result = new BatchLineInfo
      {
        RawLine = rawLine,
        Index = index,
      };

      var trimmedLine = rawLine.Trim();
      if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("#"))
      {
        result.TrimmedLine = rawLine.Trim();
        try
        {
          //parse command
          var args = CommandLineExtensions.CommandLineToArgs(result.TrimmedLine);
          var parseResult = parser.Parse(args);
          if (!parseResult.Errors.Any())
          {
            result.CommandName = parseResult.CommandResult.Command.Name;
            result.PolicyPrefixedName = parseResult.GetValueForArgument(CommandLine.PolicyArgument);
            result.PolicyClass = parseResult.GetValueForArgument(CommandLine.PolicyClassArgument);
            var invocationContext = new InvocationContext(parseResult);
            var valueSource = CommandLine.KeyAndValueOption as IValueSource;
            var valueDescriptor = CommandLine.KeyAndValueOption as IValueDescriptor;
            
            //invocationContext.BindingContext;
            var oElemValues = CommandLineExtensions.GetValueForHandlerParameter(valueDescriptor, invocationContext);
            result.ElementValues = oElemValues as List<(string, List<string>)>;
          }
          else
          {
            result.Errors = parseResult.Errors.Select(e => e.Message).ToList();
          }
        }
        catch
        {
          //no error handling
        }
      }
      else
      {
        result.Filtered = true;
      }

      return result;
    }
    public bool AddCommand(string sCommand, out string? status)
    {
      status = null;
      try
      {
        var currentFile = CurrentFile;
        if (currentFile == null)
        {
          status = "No file selected";
          return false;
        }
        bool needsNewLine = false;
        string allText = string.Empty;
        if (currentFile.Exists)
        {
          allText = File.ReadAllText(currentFile.FullName);
          needsNewLine = !string.IsNullOrEmpty(allText) && !allText.EndsWith(Environment.NewLine);
        }
        if (needsNewLine)
          allText += Environment.NewLine;
        allText += sCommand;
        File.WriteAllText(currentFile.FullName, allText);
        return true;
      }
      catch (Exception e)
      {
        status = e.Message;
        return false;
      }
    }

    public BatchLineInfo? GetCommandInfo(string? commandName, string prefixedName, PolicyClass policyClass)
    {
      return GetCommandInfos(false)?.Find(info => 
        (commandName == null || info.CommandName == commandName) &&
        string.Equals(info.PolicyPrefixedName, prefixedName) && 
        info.PolicyClass == policyClass);
    }
  }
}
