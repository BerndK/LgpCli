using System.Diagnostics;
using Cli;
using Infrastructure;
using Infrastructure.Logging.SimpleFile;
using LgpCore;
using LgpCore.AdmParser;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCli
{
  internal static class Program
  {
    [STAThread]
    static void Main(string[] args)
    {
      //test arguments using the 'original' way, not the CommandLineStringSplitter way
      //Console.WriteLine($"{args.Length} args: {string.Join(',', args.Select(a => $"'{a}'"))}");
      //return;

      Console.OutputEncoding = System.Text.Encoding.UTF8;
      //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      //Console.BufferWidth = 30000; //very slow if not used on new terminal app
      DefineCustomConsoleColors();

      var services = new ServiceCollection();
      DefineServices(services);
      var serviceProvider = services.BuildServiceProvider();

      var appInfo = serviceProvider.GetRequiredService<AppInfo>();
      var logger = serviceProvider.GetRequiredKeyedService<ILogger>(null);
      //Alternative: var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
      logger.LogInformation($"Startup {appInfo.Title} {appInfo.Ver} on '{Environment.MachineName}' Local {DateTime.Now} UTC:{DateTimeOffset.Now:O}");

      //Test logging
      //logger.LogTrace("Trace");
      //logger.LogDebug("Debug");
      //logger.LogInformation("Information");
      //logger.LogWarning("Warning");
      //logger.LogError("Error");
      //logger.LogCritical("Critical");
      try
      {
        CommandLineHandling.Invoke(serviceProvider, args);
      }
      finally
      {
        logger.LogInformation($"Shutting down {appInfo.Title} {appInfo.Ver}");
      }
    }

    private static void DefineCustomConsoleColors()
    {
      CliTools.CustomColorNames.Add("Policy", ConsoleColor.Magenta);
      CliTools.CustomColorNames.Add("Category", ConsoleColor.Cyan);
      CliTools.CustomColorNames.Add("PrefixedName", ConsoleColor.Yellow);
      CliTools.CustomColorNames.Add("Class", ConsoleColor.DarkCyan);
      CliTools.CustomColorNames.Add("ExplainText", ConsoleColor.DarkGray);
      CliTools.CustomColorNames.Add("SupportedOn", ConsoleColor.DarkMagenta);
      CliTools.CustomColorNames.Add("ElemId", ConsoleColor.Blue);
      CliTools.CustomColorNames.Add("ElemType", ConsoleColor.DarkGray);
      CliTools.CustomColorNames.Add("Enable", ConsoleColor.Green);
      CliTools.CustomColorNames.Add("Disable", ConsoleColor.DarkYellow);
      CliTools.CustomColorNames.Add("NotConfigure", ConsoleColor.White);
      CliTools.CustomColorNames.Add("GetState", ConsoleColor.DarkCyan);
      CliTools.CustomColorNames.Add("Title", ConsoleColor.White);
      CliTools.CustomColorNames.Add("Value", ConsoleColor.White);
      CliTools.CustomColorNames.Add("Warn", ConsoleColor.DarkYellow);
      CliTools.CustomColorNames.Add("Error", ConsoleColor.Red);
      CliTools.CustomColorNames.Add("Success", ConsoleColor.Green);
    }

    private static void DefineServices(ServiceCollection services)
    {
      var settingsPath = ToolBox.GetStartupPath();
      services.AddKeyedSingleton<string>("SettingsPath", settingsPath);

      //Directory.CreateDirectory(settingsPath);

      //config
      var config = new ConfigurationManager();
      config
        .SetBasePath(settingsPath)
        .AddJsonFile("Settings.json", optional: true) //reloadOnChange: true
        //.AddCommandLine(new string[] { "ConfigSection2:Value1=one" })
        ;

      services.AddSingleton<IConfigurationRoot>(config);
      services.AddSingleton<IConfiguration>(config);
      services.AddKeyedSingleton<IConfigurationSection>("app", config.AppSection());
      services.AddTransient<BatchCmd>();
      services.AddSingleton<CommandLine>();

      //config.Bind();
      //var value = config.GetValue<string>("ConfigSection2:Value1");
      //JsonConfigurationProvider prov = (JsonConfigurationProvider) config.Providers.First();

      //var myConfig = config.GetSection("ConfigSection1").Get<MyConfig>();

      //Console.WriteLine($"Value1 : {myConfig.Value1}");
      //Console.WriteLine($"Value2 : {myConfig.Value2}");

      //SimpleFile Logging:
      //SimpleFileRollingOptions: three options
      // a) do nothing and use defaults
      // b) use AddSimpleFile(options => ...) to configure the defaults for this app (see below)
      // c) provide related section "Logging:SimpleFile:SimpleFileRollingOptions" in IConfig (here we use "Settings.json")
      //here we use b) and c) (c overrides b)
      //optional: services.AddSingleton<IAssemblyInfoProvider>(new MockedAssemblyInfoProvider("TestCompany", "TestProduct", "1.2.3.4"));
      //per default EntryAssemblyInfoProvider (uses Attributes)
      services.AddLogging(builder => builder
        //.AddConsole() //adds LoggingProvider for Logging to Console
        //.AddDebug() //adds LoggingProvider for Logging to Debug-Output

        //.AddFilter("SbomCli", LogLevel.Trace)
        //.AddFilter("*.Some", LogLevel.Trace)
        //.AddFilter<SimpleFileLoggerProvider>("*", LogLevel.Trace)
        //see also https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0
        .AddSimpleFile(options => options.Location = LogFileLocation.ExecutableLogsDirectory)
        //.SetMinimumLevel(LogLevel.Debug)
        .AddConfiguration(config.GetSection("Logging")) //see settings.json, section logging
        
      );
      services.Configure<SimpleFileRollingOptions>(config.GetSection("Logging:SimpleFile:SimpleFileRollingOptions"));
      //Formatter for SimpleFile:
      //define an own Formatter: services.AddSingleton<SimpleFileLoggerFormatterBase, MyOwnSimpleFileLoggerFormatter>();
      //or configure the builtin one's options
      //services.Configure<SimpleFileFormatterOptions>(options => options.SingleLine = false);
      services.Configure<SimpleFileFormatterOptions>(options => options.IncludeScopes = true);
      services.Configure<SimpleFileFormatterOptions>(config.GetSection("Logging:SimpleFile:SimpleFileFormatterOptions"));

      string? language = config.AppSection()["admLanguage"];
      services.AddSingleton(Task.Run(() =>
      {
        var sw = Stopwatch.StartNew();
        var admFolder = AdmFolder.SystemDefault();
        if (language != null)
          admFolder.Language = language;
        //admFolder.Language = "de-DE"; // "en-US
        admFolder.ParseAsync().GetAwaiter().GetResult();
        sw.Stop();
        return new AdmFolderParseResult()
        {
          AdmFolder = admFolder,
          ElapsedMs = sw.ElapsedMilliseconds,
          TimeReported = false
        };
      }));
      
      services.AddSingleton<ILogger>(provider =>
        provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program).Assembly.GetName().Name ?? "Main"));

      services.AddSingleton<AdmFolder>(provider =>
      {
        var parseResult = provider.GetRequiredService<Task<AdmFolderParseResult>>().Result;
        if (!parseResult.TimeReported)
        {
          provider.GetService<ILogger>()?.LogDebug(
              $"Scanning AdmFolder with {parseResult.AdmFolder.Contents.Count} files, {parseResult.AdmFolder.AllPolicies.Count} policies - took {parseResult.ElapsedMs}ms");
          parseResult.TimeReported = true;
        }

        return parseResult.AdmFolder;
      });
      services.AddSingleton<AppInfo, AppInfo>();
    }

    public static IConfigurationSection AppSection(this IConfigurationRoot configurationRoot) => configurationRoot.GetSection("app");

    private class AdmFolderParseResult
    {
      public required AdmFolder AdmFolder { get; init; }
      public required long ElapsedMs { get; init; }
      public bool TimeReported { get; set; } = false;
    }
  }
}
