using Infrastructure;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace LgpCoreTests
{
  public class JsonWritableConfigurationTests
  {
    [Test]
    public void StoreJsonConfigTest()
    {
      //this tries a different approach to modify the json file based on the default JsonConfigurationProvider
      string sJson = """
        {
          "ConfigSection1": {
            //Some comment
            "Value1": "1-one",
            "Value2": 12,
            "SubSection1":{
              "SubValue1": "1-1-one",
              "SubValue2": 112
            }
          },
          "ConfigSection2": {
            "Value1": "2-one",
            "Value2": 22
          },
          "RootValue1": 1
        }
        """;

      using var jsonFile = ToolBox.CreateTempFile();
      File.WriteAllText(jsonFile.Value, sJson);

      using var ms = ToolBox.StringToUtf8MemoryStream(sJson);

      var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(jsonFile.Value)
        .AddCommandLine(new string[] { "ConfigSection2:Value1=one" })
      ;
      Console.WriteLine($"{jsonFile.Value} {File.Exists(jsonFile.Value)}");

      var config = configurationBuilder.Build();
      
      var section = config.GetSection("ConfigSection2");
      
      config["ConfigSection2:Value1"] = "2-one Modifed";
      config["ConfigSection3:Value1"] = "ValueForNewSection";
      section["Value3"] = "three";
      //config["ConfigSection2:Value3"] = "three";
      var earlierKeys = Enumerable.Empty<string>();

      void HandleSections(IEnumerable<IConfigurationSection> sections, int indent)
      {
        foreach (var section in sections)
        {
          Console.WriteLine($"{new string(' ', indent * 2)}[{section.Path}] Value:{section.Value}");
          HandleSections(section.GetChildren(), indent + 1);
        }
      }

      HandleSections(config.GetChildren(), 0);
      Console.WriteLine();
      Console.WriteLine(File.ReadAllText(jsonFile.Value));

      //var saved = config.SaveJsonProvider();
      var saved = section.SaveJsonProvider();

      Console.WriteLine(File.ReadAllText(jsonFile.Value));
    }

    [Test]
    public void JsonConfigUpdateFullExampleTest()
    {
      //please make sure that your app has the right to write in this folder (typically not the case as it is in Program Files for normal user)
      //consider to use e.g. ProgramData or AppData folder
      var startupPath = System.AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar)
                        ?? throw new InvalidOperationException();

      var config = new ConfigurationManager();
      config
        .SetBasePath(startupPath)
        .AddJsonFile("settings.json", optional:true);

      ServiceCollection services = new ServiceCollection();
      services.AddSingleton<IConfigurationRoot>(config);
      services.AddSingleton<IConfiguration>(config);
      //... add other services
      var serviceProvider = services.BuildServiceProvider();

      //...

      var configRoot = serviceProvider.GetRequiredService<IConfigurationRoot>();
      var section = configRoot.GetSection("UserSettings");
      section["LastFolderUsed"] = "C:\\Temp";
      configRoot.SaveJsonProvider(); //this will save the changes to the json file, file is created if not exists
    }
  }
}
