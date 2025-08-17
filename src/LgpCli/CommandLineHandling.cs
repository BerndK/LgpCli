using LgpCore;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine.Parsing;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using Cli;
using Infrastructure;
using LgpCore.AdmParser;
using LgpCore.Gpo;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LgpCli
{
  public static class CommandLineHandling
  {
    private static bool HasGreetingShown = false;

    public static void Invoke(IServiceProvider serviceProvider, string[] args)
    {
      var logger = serviceProvider.GetRequiredService<ILogger>();
      logger.LogInformation($"Command Line: {args.Length} Args: '{string.Join(' ', args)}'");

      //define all the options arguments and commands
      var commandLine = serviceProvider.GetRequiredService<CommandLine>();

      //define Handlers
      commandLine.RootCommand.SetHandler(provider => MainCli.ShowPage(provider, null), CommandLine.ServiceProviderBinder);
      commandLine.InteractiveCommand.SetHandler(provider => MainCli.ShowPage(serviceProvider, null));
      commandLine.ShowCommand.SetHandler(HandleShowCommand);
      commandLine.SearchCommand.SetHandler(HandleSearchCommand);
      commandLine.GetStateCommand.SetHandler(HandleGetStateCommand);
      commandLine.EnableCommand.SetHandler(HandleEnableCommand);
      commandLine.DisableCommand.SetHandler(HandleDisableCommand);
      commandLine.NotConfigureCommand.SetHandler(HandleNotConfigureCommand);
      commandLine.BatchCommand.SetHandler(HandleBatchCommand);

      //start
      commandLine.BuildParser(serviceProvider);
      var parseResult = commandLine.Parser.Parse(args);
      logger.LogInformation($"Command Line: {parseResult}");
      parseResult.Invoke();
    }

    private static void Greeting(IServiceProvider serviceProvider)
    {
      if (HasGreetingShown)
        return;
      HasGreetingShown = true;
      var appInfo = serviceProvider.GetRequiredService<AppInfo>();
      CliTools.MarkupLine($"[Title]{appInfo.Title}[/] [DarkGreen]{appInfo.Ver}[/] ({appInfo.Copyright}) - {appInfo.Desc} [[{(appInfo.IsAdmin ? "[Green]admin[/]" : "[Red]not admin[/]")}]]");
    }

    private static void HandleGetStateCommand(IServiceProvider serviceProvider, string policyPrefixedName,  PolicyClass? policyClassNullable)
    {
      HandleGetStateCommandWithPrefix(serviceProvider, policyPrefixedName, policyClassNullable, null);
    }
    private static void HandleGetStateCommandWithPrefix(IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClassNullable, string? prefix)
    {
      Greeting(serviceProvider);

      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      var policy = admFolder.AllPolicies.GetValueOrDefault(policyPrefixedName);
      if (policy == null)
      {
        CliTools.ErrorMessage($"Policy '{policyPrefixedName}' not found.", false);
        return;
      }

      var logger = serviceProvider.GetService<ILogger<PolicyJob>>();
      void ReportState(PolicyClass policyClass)
      {
        var state = policy.GetState(policyClass, logger);
        CliTools.MarkupLine($"{prefix}State:{PolicyCli.StateMarkupString(state)} for [PrefixedName]{policy.PrefixedName()}[/] ([Class]{policyClass}[/]) [Title]{policy.DisplayNameResolved()}[/]");
        if (state == PolicyState.Enabled)
        {
          var elementValues = ElementValues.CurrentOnSystem(policy, policyClass);
          PolicyCli.ShowElementValues(elementValues);
        }
      }

      policyClassNullable ??= PolicyClass.Both;
      if (policyClassNullable == PolicyClass.Both)
      {
        ReportState(PolicyClass.Machine);
        ReportState(PolicyClass.User);
      }
      else
      {
        ReportState(policyClassNullable.Value);
      }
    }

    private static void HandlePolicyCommand(IServiceProvider serviceProvider, string policyPrefixedName,
      PolicyClass? policyClassNullable, CommandLine.GetStateMode getStateMode, string actionText, Action<Policy, PolicyClass, ILogger?> policyAction, Action<Policy, PolicyClass>? policyPreAction = null)
    {
      Greeting(serviceProvider);
      var logger = serviceProvider.GetKeyedService<ILogger>(null);
      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      var policy = admFolder.AllPolicies.GetValueOrDefault(policyPrefixedName);
      if (policy == null)
      {
        CliTools.ErrorMessage($"Policy '{policyPrefixedName}' not found.", false);
        return;
      }

      policyClassNullable ??= policy.Class;
      if (policyClassNullable == PolicyClass.Both)
      {
        CliTools.ErrorMessage("PolicyClass 'Both' is not allowed for this command.", false);
        return;
      }

      var policyClass = policyClassNullable.Value;
      CliTools.MarkupLine($"{actionText} [Policy]Policy[/] [PrefixedName]{policy.PrefixedName()}[/] ([Class]{policyClass}[/]) [Title]{policy.DisplayNameResolved()}[/]");
      
      policyPreAction?.Invoke(policy, policyClass);

      if (getStateMode is CommandLine.GetStateMode.Before or CommandLine.GetStateMode.Both)
      {
        HandleGetStateCommandWithPrefix(serviceProvider, policyPrefixedName, policyClass, "Old");
      }

      Console.WriteLine("action...");
      policyAction(policy, policyClass, logger);

      if (getStateMode is CommandLine.GetStateMode.After or CommandLine.GetStateMode.Both)
      {
        HandleGetStateCommandWithPrefix(serviceProvider, policyPrefixedName, policyClass, "New");
      }
    }

    private static void HandleEnableCommand(IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClassNullable, List<(string, List<string>)> elemArgs, CommandLine.GetStateMode getStateMode)
    {
      HandlePolicyCommand(serviceProvider, policyPrefixedName, policyClassNullable, getStateMode, 
        "Command :[Enable]Enable[/]", (policy, policyClass, logger) =>
        {
          var elementValues = ElementValues.FromCommandLine(policy, policyClass, elemArgs);
          policy.Enable(policyClass, elementValues.CompletedValues, logger);
        },
        (policy, policyClass) =>
        {
          var elementValues = ElementValues.FromCommandLine(policy, policyClass, elemArgs);
          PolicyCli.ShowElementValues(elementValues);
        });
    }

    private static void HandleDisableCommand(IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClassNullable, CommandLine.GetStateMode getStateMode)
    {
      HandlePolicyCommand(serviceProvider, policyPrefixedName, policyClassNullable, getStateMode,
        "Command :[Disable]Disable[/]", (policy, policyClass, logger) => policy.Disable(policyClass, logger));
    }

    private static void HandleNotConfigureCommand(IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClassNullable, CommandLine.GetStateMode getStateMode)
    {
      HandlePolicyCommand(serviceProvider, policyPrefixedName, policyClassNullable, getStateMode,
        "Command :[NotConfigure]NotConfigure[/]", (policy, policyClass, logger) => policy.NotConfigure(policyClass, logger));
    }

    public static void HandleShowCommand(IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClass)
    {
      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      var policy = admFolder.AllPolicies.GetValueOrDefault(policyPrefixedName);
      if (policy == null)
      {
        CliTools.ErrorMessage($"Policy '{policyPrefixedName}' not found.");
        return;
      }
      MainCli.ShowPage(serviceProvider, () => PolicyCli.ShowPage(serviceProvider, policy, policyClass ?? PolicyClass.Both));
    }

    private static void HandleSearchCommand(IServiceProvider serviceProvider, string[] searchTokens, bool searchName, bool searchTitle, bool searchDescription, bool searchCategory, PolicyClass? policyClass)
    {
      Greeting(serviceProvider);
      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      var searchText = string.Join("|", searchTokens);
      var objects = admFolder.Search(searchText, searchName, searchTitle, searchDescription, searchCategory, policyClass ?? PolicyClass.Both);
      SearchCli.ReportAllCount(admFolder);
      CliTools.MarkupLine($"searching for '{searchText}' -> Found {objects.Count} items:");
      foreach (var obj in objects)
      {
        CliTools.MarkupLine($"{SearchCli.ItemDisplayText(obj)}");
      }
    }

    private static void HandleBatchCommand(IServiceProvider serviceProvider, FileInfo batchFileInfo, bool continueOnError)
    {
      var logger = serviceProvider.GetService<ILogger>();
      var commandLine = serviceProvider.GetRequiredService<CommandLine>();
      
      void Message(string message)
      {
        logger?.LogInformation(message);
        Console.WriteLine(message);
      }
      void WarnMessage(string message)
      {
        logger?.LogWarning(message);
        CliTools.WriteLine(CliTools.WarnColor, message);
      }
      void ErrorMessage(string message)
      {
        logger?.LogError(message);
        CliTools.WriteLine(CliTools.ErrorColor, message);
      }

      Greeting(serviceProvider);
      var batchCmd = BatchCmd.Create(serviceProvider, batchFileInfo);
      var infos = batchCmd.GetCommandInfos(false);
      if (infos == null)
      {
        ErrorMessage("No commands found in batch file.");
        return;
      }

      bool hadError = false;

      //just grab the error to stop processing
      //the error handling is done by the commandline (send full text to logger, and exception message to console)
      commandLine.ExceptionThrown += (Exception ex, InvocationContext invocationContext, ref bool handled) => hadError = true;

      Message($"Processing {infos.Count} batch commands:");
      foreach (var info in infos)
      {
        try
        {
          Message($"Processing batch command line {info.Index}: {info.TrimmedLine}");
          if (info.Errors?.Any() ?? false)
          {
            ErrorMessage($"{info.Errors.Count} Errors in batch command line");
            foreach (var infoError in info.Errors)
            {
              ErrorMessage($"  {infoError}");
            }

            hadError = true;
          }
          else
          {
            var args = CommandLineExtensions.CommandLineToArgs(info.TrimmedLine);
            var parseResult = commandLine.Parser.Parse(args);
            parseResult.Invoke();
            
          }
        }
        catch (Exception e)
        {
          ErrorMessage(e.Message);
          hadError = true;
        }

        if (hadError && !continueOnError)
        {
          WarnMessage("Stopping batch processing due to error.");
          return;
        }
        Console.WriteLine();
      }
    }
  }
}
