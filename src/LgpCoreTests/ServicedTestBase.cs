using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

namespace LgpCoreTests
{
  public class ServicedTestBase
  {
    private ServiceProvider? serviceProvider;
    public IServiceProvider ServiceProvider => serviceProvider ?? throw new InvalidOperationException();

    [SetUp]
    public virtual void SetUp()
    {
      var serviceCollection = new ServiceCollection();
      DefineServices(serviceCollection);

      serviceProvider = serviceCollection.BuildServiceProvider();
    }

    protected virtual void DefineServices(ServiceCollection serviceCollection)
    {
      //serviceCollection.AddKeyedSingleton<string>("DataPath", DataPath);
      //config
      var configurationBuilder = new ConfigurationBuilder()
//          .AddJsonWritableFile("Settings.json")
        //.AddCommandLine(new string[] { "ConfigSection2:Value1=one" })
        ;
      var config = configurationBuilder.Build();
      serviceCollection.AddSingleton<IConfigurationRoot>(config);
      serviceCollection.AddSingleton<IConfiguration>(config);
      
      serviceCollection.AddLogging(builder => builder
        //.AddConsole(options =>
        //{
        //  options.IncludeScopes = true;
        //  //options.SingleLine = true;

        //})
        .AddSimpleConsole(options =>
        {
          options.IncludeScopes = true;
          options.SingleLine = true;
          //options.TimestampFormat = "HH:mm:ss ";
        })
        //.AddCustomFormatter(options => { })
        //.AddDebug() //adds DebugLoggerProvider
        .SetMinimumLevel(LogLevel.Debug)
      //.AddFilter(null, LogLevel.Debug) //sets min LogLevel for all providers, all categories
      //.AddFilter<DebugLoggerProvider>("*", LogLevel.Debug) //sets Loglevel only for providers of type DebugLoggerProvider
      //.AddFilter(nameof(Module), LogLevel.Warning) //sets min LogLevel for all providers, category 'Module'
      );
    }

    [TearDown]
    public virtual void TearDown()
    {
      serviceProvider?.Dispose();
    }

  }
}
