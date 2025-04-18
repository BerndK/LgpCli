using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Infrastructure;
using Microsoft.Extensions.Logging.Console;

namespace LgpCoreTests
{
  public class CustomConsoleFormatterTest
  {
    [SetUp]
    public void SetUp()
    {
      var serviceCollection = new ServiceCollection();
      DefineServices(serviceCollection);

      serviceProvider = serviceCollection.BuildServiceProvider();

    }
    private ServiceProvider serviceProvider;
    
    [TearDown]
    public void TearDown()
    {
      if (serviceProvider is IDisposable disposable)
        disposable.Dispose();
    }

    private void DefineServices(ServiceCollection serviceCollection)
    {
      serviceCollection.AddLogging(builder => builder
        .AddSimpleConsole(options => options.IncludeScopes = true)
        .AddCustomFormatter(options => {})
      );
    }

    [Test]
    public void FormatterTest()
    {
      var logger = serviceProvider.GetRequiredService<ILogger<CustomConsoleFormatterTest>>();
      var Scope1 = logger.BeginScope("Scope1");
      logger.LogInformation("Hello, world Inside Scope 1!");
      var Scope2 = logger.BeginScope("Scope2");
      logger.LogInformation("Hello, world Inside Scope 2!");
      Scope2?.Dispose();
      logger.LogInformation("Hello, world Inside Scope 1!");
      Scope1?.Dispose();
      logger.LogInformation("Hello, world2 No Scope!");
    }

#if !RELEASE
    [Test]
    public void DiTestJustLogging()
    {
      var serviceCollection = new ServiceCollection();

      serviceCollection.AddLogging();

      var aServiceProvider = serviceCollection.BuildServiceProvider();

      TestLogOutput(aServiceProvider);
      Console.WriteLine(aServiceProvider.DebugDependencies(true));
      //DebugLoggerProviders(aServiceProvider);
    }

    [Test]
    public void DiTestAddConsole()
    {
      var serviceCollection = new ServiceCollection();

      serviceCollection.AddLogging(builder => builder
        .AddConsole()
        //.AddCustomFormatter(options => { })
      );

      var aServiceProvider = serviceCollection.BuildServiceProvider();

      TestLogOutput(aServiceProvider);

      Console.WriteLine(aServiceProvider.DebugDependencies(true));
      //DebugLoggerProviders(aServiceProvider);
    }

    [Test]
    public void DiTestSimpleConsole()
    {
      var serviceCollection = new ServiceCollection();

      serviceCollection.AddLogging(builder => builder
        //.AddSimpleConsole()
        //.AddSimpleConsole(options => options.IncludeScopes = true)
        .AddSimpleConsole(options => {})
        //.AddCustomFormatter(options => { }
      );

      var aServiceProvider = serviceCollection.BuildServiceProvider();

      TestLogOutput(aServiceProvider);

      Console.WriteLine(aServiceProvider.DebugDependencies(true));
      //DebugLoggerProviders(aServiceProvider);
    }

    [Test]
    public void DiTestAddConsoleWithCustomFormatter()
    {
      var serviceCollection = new ServiceCollection();

      serviceCollection.AddLogging(builder => builder
        .AddConsole()
        .AddCustomFormatter(options => { })
      );

      var aServiceProvider = serviceCollection.BuildServiceProvider();

      TestLogOutput(aServiceProvider);

      Console.WriteLine(aServiceProvider.DebugDependencies(true));
      //DebugLoggerProviders(aServiceProvider);
    }

    [Test]
    public void DiTestExternalScopeProvider()
    {
      var serviceCollection = new ServiceCollection();

      //serviceCollection.AddSingleton<IExternalScopeProvider, LoggerExternalScopeProvider>();
      serviceCollection.AddSingleton<IExternalScopeProvider, CustomExternalScopeProvider>();
      serviceCollection.AddSingleton<ICustomExternalScopeProvider>(provider => (ICustomExternalScopeProvider)provider.GetRequiredService<IExternalScopeProvider>());
      serviceCollection.AddLogging(builder => builder
        .AddConsole()
        .AddCustomFormatter(options => { options.IncludeScopes = true; })
      );

      var aServiceProvider = serviceCollection.BuildServiceProvider();

      //var scopeProvider = aServiceProvider.GetRequiredService<IExternalScopeProvider>();
      //var scopeProvider = aServiceProvider.GetRequiredService<ICustomExternalScopeProvider>();


      TestLogOutput(aServiceProvider);

      Console.WriteLine(aServiceProvider.DebugDependencies(true));
      //DebugLoggerProviders(aServiceProvider);
    }

    private static void TestLogOutput(ServiceProvider aServiceProvider)
    {
      Console.WriteLine();
      var logger = aServiceProvider.GetRequiredService<ILogger<CustomConsoleFormatterTest>>();
      logger.LogDebug("Hello, world!");
      logger.LogInformation("Hello, world!");
      logger.LogCritical("Hello, world!");
      var Scope1 = logger.BeginScope("Scope1");
      logger.LogInformation("Hello, world Inside Scope 1!");
      var Scope2 = logger.BeginScope("Scope2");
      logger.LogInformation("Hello, world Inside Scope 2!");
      Scope2?.Dispose();
      logger.LogInformation("Hello, world Inside Scope 1!");
      Scope1?.Dispose();
      logger.LogInformation("Hello, world2 No Scope!");

      Thread.Sleep(100);
    }

    [Test]
    public void DebugLoggerProvidersTest()
    {
      var serviceCollection = new ServiceCollection();

      serviceCollection.AddLogging(builder => builder
        .AddConsole()
        .AddCustomFormatter(options => { })
        .AddFilter<ConsoleLoggerProvider>("SomeCategory", LogLevel.Critical)
        .AddFilter("FilteredCategory", level => true)
      );

      var aServiceProvider = serviceCollection.BuildServiceProvider();
      //Console.WriteLine(aServiceProvider.DebugDependencies());
      DebugLoggerProviders(aServiceProvider);
      
    }
    public void DebugLoggerProviders(ServiceProvider aServiceProvider)
    {
      var loggerProviders = aServiceProvider.GetServices<ILoggerProvider>();
      Console.WriteLine("Registered LoggerProviders:");
      foreach (var loggerProvider in loggerProviders)
      {
        Console.WriteLine($"  {TypeNameHelper.FriendlyName(loggerProvider)}");
      }

      var loggerFactory = aServiceProvider.GetService<ILoggerFactory>() as LoggerFactory;
      var providerRegistrations = RuntimeReflectionHelper.GetField<ICollection>(typeof(LoggerFactory), loggerFactory, "_providerRegistrations");
      var providerRegistrationType = Type.GetType("Microsoft.Extensions.Logging.LoggerFactory.ProviderRegistration");
      if (providerRegistrations != null && providerRegistrationType != null)
      {
        Console.WriteLine("LoggerFactory.Providers:");
        foreach (var providerRegistration in providerRegistrations)
        {
          var loggerProvider = RuntimeReflectionHelper.GetField<ILoggerProvider>(providerRegistrationType, providerRegistration, "Provider") as ILoggerProvider;
          Console.WriteLine($"  {TypeNameHelper.FriendlyName(loggerProvider)}");
        }
      }
      var filterOptions = RuntimeReflectionHelper.GetField<LoggerFilterOptions>(typeof(LoggerFactory), loggerFactory, "_filterOptions");
      if (filterOptions != null)
      {
        Console.WriteLine($"LoggerFactory.FilterOptions: MinLevel: {filterOptions.MinLevel} CaptureScopes:{filterOptions.CaptureScopes}");
        
        if (filterOptions.Rules.Any())
        {
          Console.WriteLine("LoggerFactory.FilterOptions.Rules:");
          foreach (var rule in filterOptions.Rules)
          {
            Console.WriteLine($"  ProviderName: '{rule.ProviderName ?? "<all>"}' Category: '{rule.CategoryName}' LogLevel: {rule.LogLevel} CustomFilter: {rule.Filter != null}");
            //this is similar: Console.WriteLine(rule.ToString());
          }
        }
      }
    }
#endif
  }
}
