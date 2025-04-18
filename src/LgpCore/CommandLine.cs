using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Runtime.ExceptionServices;
using Infrastructure;
using LgpCore.AdmParser;
using LgpCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCore
{
  //see https://learn.microsoft.com/en-us/dotnet/standard/commandline/
  public class CommandLine
  {
    private Parser? parser;
    private CommandLineBuilder? builder;
    public const string CommandNameShow = "show";
    public const string CommandNameEnable = "enable";
    public const string CommandNameDisable = "disable";
    public const string CommandNameNotConfigure = "notconfigure";
    public const string CommandNameGetState = "get-state";
    public const string CommandNameSearch = "search";
    public const string CommandNameInteractive = "interactive";
    public const string CommandNameBatch = "batch";
    public static ContextProviderBinder<IServiceProvider> ServiceProviderBinder { get; }
    public static Option<string[]> SearchTokensOption { get; }
    public static Option<bool> SearchNameOption { get; }
    public static Option<bool> SearchTitleOption { get; }
    public static Option<bool> SearchDescriptionOption { get; }
    public static Option<bool> SearchCategoryOption { get; }
    public static Argument<string> PolicyArgument { get; }
    public static Argument<PolicyClass?> PolicyClassArgument { get; }
    public static Option<string[]> KeyOption { get; }
    public static Option<string[]> ValueOption { get; }
    public static KeyValuePairsBinder<string, string> KeyAndValueOption { get; }
    public static Option<GetStateMode> GetStateModeOption { get; }
    public static Argument<FileInfo> BatchFileArgument { get; }
    public static Option<bool> ContinueOnErrorOption { get; }
    public RootCommand RootCommand { get; }
    public Command<IServiceProvider, string, PolicyClass?> ShowCommand { get; }
    public Command<IServiceProvider, string, PolicyClass?, List<(string, List<string>)>, GetStateMode> EnableCommand { get; }
    public Command<IServiceProvider, string, PolicyClass?, GetStateMode> DisableCommand { get; }
    public Command<IServiceProvider, string, PolicyClass?, GetStateMode> NotConfigureCommand { get; }
    public Command<IServiceProvider, string, PolicyClass?> GetStateCommand { get; }
    public Command<IServiceProvider, string[], bool, bool, bool, bool, PolicyClass?> SearchCommand { get; }
    public Command<IServiceProvider, FileInfo, bool> BatchCommand { get; }
    public Command<IServiceProvider> InteractiveCommand { get; }

    static CommandLine()
    {
      ServiceProviderBinder = new ContextProviderBinder<IServiceProvider>();
      SearchTokensOption = new Option<string[]>(
          name: "--search-tokens",
          description: "search tokens for searching policies.")
      { IsRequired = true, AllowMultipleArgumentsPerToken = true };
      SearchTokensOption.AddAlias("-s");

      SearchNameOption = new Option<bool>(
        name: "--search-name",
        description: "search policy's prefixed name.",
        getDefaultValue: () => true);
      SearchNameOption.AddAlias("-sn");

      SearchTitleOption = new Option<bool>(
        name: "--search-title",
        description: "search policy's title.",
        getDefaultValue: () => true);
      SearchTitleOption.AddAlias("-st");

      SearchDescriptionOption = new Option<bool>(
        name: "--search-desc",
        description: "search policy's description.",
        getDefaultValue: () => true);
      SearchDescriptionOption.AddAlias("-sd");

      SearchCategoryOption = new Option<bool>(
        name: "--search-cat",
        description: "search also for category names.",
        getDefaultValue: () => false);
      SearchCategoryOption.AddAlias("-sc");

      PolicyArgument = new Argument<string>(
        name: "policy",
        description: "Prefixed name of a policy.");

      PolicyClassArgument = new Argument<PolicyClass?>(
          name: "policyclass",
          description: "Context for a policy.")
        { Arity = ArgumentArity.ZeroOrOne };

      KeyOption = new Option<string[]>(
          name: "--key",
          description: "name for an element value for setting a policy.")
        { Arity = ArgumentArity.ZeroOrMore };
      KeyOption.AddAlias("-k");

      ValueOption = new Option<string[]>(
          name: "--value",
          description: "value for an element value for setting a policy.")
        { Arity = ArgumentArity.ZeroOrMore };
      ValueOption.AddAlias("-v");
      ValueOption.AllowMultipleArgumentsPerToken = true;

      KeyAndValueOption = new KeyValuePairsBinder<string, string>(KeyOption, ValueOption); //this one handles the key and value options, these options are added automatically

      GetStateModeOption = new Option<GetStateMode>(
        name: "--get-state",
        description: "reports also the state before or after setting it",
        getDefaultValue: () => GetStateMode.Both);
      GetStateModeOption.AddAlias("-gs");

      BatchFileArgument = new Argument<FileInfo>(
        name: "batchfile",
        description: "a file with batch commands to process.")
      { Arity = ArgumentArity.ExactlyOne };

      ContinueOnErrorOption = new Option<bool>(
        name: "--continue-on-error",
        description: "continue batch processing on error.",
        getDefaultValue: () => false);
      ContinueOnErrorOption.AddAlias("-coe");
    }

    public enum GetStateMode
    {
      None,
      Before,
      After,
      Both
    }

    public CommandLine(AppInfo? appInfo)
    {

      ShowCommand = TypedCommand.Create(CommandNameShow, "Shows a policy.",  ServiceProviderBinder, PolicyArgument, PolicyClassArgument);

      EnableCommand = TypedCommand.Create(CommandNameEnable, "sets a policy to enabled/active state",
        ServiceProviderBinder,
        PolicyArgument,
        PolicyClassArgument,
        KeyAndValueOption,
        GetStateModeOption);

      DisableCommand = TypedCommand.Create(CommandNameDisable, "sets a policy to disable state",
        ServiceProviderBinder,
        PolicyArgument, 
        PolicyClassArgument, 
        GetStateModeOption);

      NotConfigureCommand = TypedCommand.Create(CommandNameNotConfigure, "sets a policy to not configured state",
        ServiceProviderBinder,
        PolicyArgument,
        PolicyClassArgument,
        GetStateModeOption);

      GetStateCommand = TypedCommand.Create(CommandNameGetState, "gets a policy's state",
        ServiceProviderBinder,
        PolicyArgument,
        PolicyClassArgument);
      
      SearchCommand = TypedCommand.Create(CommandNameSearch, "searches for policies",
        ServiceProviderBinder,
        SearchTokensOption,
        SearchNameOption,
        SearchTitleOption,
        SearchDescriptionOption,
        SearchCategoryOption,
        PolicyClassArgument);

      BatchCommand = TypedCommand.Create(CommandNameBatch, "process a batch file",
        ServiceProviderBinder,
        BatchFileArgument,
        ContinueOnErrorOption);

      InteractiveCommand = new Command<IServiceProvider>(CommandNameInteractive, "Shows the commandline UI.", ServiceProviderBinder)
      {
        ShowCommand 
      };

      RootCommand = new RootCommand(appInfo != null
        ? $"{appInfo.Desc} {appInfo.Copyright}"
        : "Tool to manage Local Group Policies")
      {
        InteractiveCommand,
        EnableCommand,
        DisableCommand,
        NotConfigureCommand,
        GetStateCommand,
        SearchCommand,
        BatchCommand
      };
      
      //rootCommand.AddGlobalOption(fileOption);


      //these are the same as the above, but with the old way of defining the commands (simple commands, not TypedCommands):
      //rootCommand.SetHandler((serviceProvider) => MainCli.ShowPage(serviceProvider, null), serviceProviderBinder);
      //interactiveCommand.SetHandler((serviceProvider) => MainCli.ShowPage(serviceProvider, null), serviceProviderBinder);
      //showCommand.SetHandler((IServiceProvider serviceProvider, string policyPrefixedName, PolicyClass? policyClass) =>
      //{
      //  var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      //  var policy = admFolder.AllPolicies.GetValueOrDefault(policyPrefixedName);
      //  if (policy == null)
      //  {
      //    CliTools.ErrorMessage($"Policy '{policyPrefixedName}' not found.");
      //    return;
      //  }
      //  MainCli.ShowPage(serviceProvider, () => PolicyCli.ShowPage(serviceProvider, policy, policyClass ?? PolicyClass.Both));
      //}, serviceProviderBinder, policyArgument, policyClassArgument);
    }

    public Parser Build(IServiceProvider serviceProvider, Action<CommandLineBuilder>? configureBuilder = null)
    {
      builder = new CommandLineBuilder(RootCommand)
        .UseDefaults()
        .UseExceptionHandler(OnException)
        .UseService<IServiceProvider>(() => serviceProvider);
      if (configureBuilder != null)
        configureBuilder(builder);
      this.Logger = serviceProvider.GetService<ILogger>();
      Parser = builder.Build();
      return Parser;
    }

    public ILogger? Logger { get; set; }

    private void OnException(Exception ex, InvocationContext invocationContext)
    {
      Logger?.LogError(ex.ToString());

      this.LastException = ex;
      this.LastExceptionInvocationContext = invocationContext;
      bool handled = true;
      ExceptionThrown?.Invoke(ex, invocationContext, ref handled);
      //invocationContext.Console.Error.WriteLine($"Exception: {ex}");  
      if (!handled)
      {
        //throw new ApplicationException(null, ex); //this would rethrow the exception (as inner) but add another stack frame and another exception type!
        ExceptionDispatchInfo.Capture(ex).Throw();
      }
      else
      {
        var saveColor = Console.ForegroundColor;
        try
        {
          Console.ForegroundColor = ConsoleColor.Red;
          invocationContext.Console.Error.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
          Console.ForegroundColor = saveColor;
        }
      }
    }

    public delegate void ExceptionDelegate(Exception ex, InvocationContext invocationContext, ref bool handled);
    public event ExceptionDelegate? ExceptionThrown; 
    public InvocationContext? LastExceptionInvocationContext { get; set; }

    public Exception? LastException { get; set; }

    public Parser Parser
    {
      get => parser ?? throw new NullReferenceException();
      private set => parser = value;
    }

    public CommandLineBuilder Builder
    {
      get => builder ?? throw new NullReferenceException();
      private set => builder = value;
    }
  }

}
