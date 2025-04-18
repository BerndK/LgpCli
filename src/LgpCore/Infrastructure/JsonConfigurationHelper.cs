using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Configuration.Json
{
  /// <summary>
  /// Helper methods for storing changes with JsonConfigurationProvider
  /// </summary>
  /// <example>
  ///   //please make sure that your app has the right to write in this folder (typically not the case as it is in Program Files for normal user)
  ///   //consider to use e.g. ProgramData or AppData folder
  ///   var startupPath = System.AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar)
  ///                     ?? throw new InvalidOperationException();
  ///   
  ///   var config = new ConfigurationManager();
  ///   config
  ///     .SetBasePath(startupPath)
  ///     .AddJsonFile("settings.json", optional:true);
  ///   
  ///   ServiceCollection services = new ServiceCollection();
  ///   services.AddSingleton<IConfigurationRoot>(config);
  ///   services.AddSingleton<IConfiguration>(config);
  ///   //... add other services
  ///   var serviceProvider = services.BuildServiceProvider();
  ///   
  ///   //...
  ///   
  ///   var configRoot = serviceProvider.GetRequiredService<IConfigurationRoot>();
  ///   var section = configRoot.GetSection("UserSettings");
  ///   section["LastFolderUsed"] = "C:\\Temp";
  ///   configRoot.SaveJsonProvider(); //this will save the changes to the json file, file is created if not exists
  /// </example>
  public static class JsonConfigurationHelper
  {
    public static bool SaveJsonProvider(this IConfigurationRoot configurationRoot)
    {
      //try to get the last registered json provider
      var provider = configurationRoot.Providers.LastOrDefault(p => p is JsonConfigurationProvider) as JsonConfigurationProvider;
      if (provider == null)
        return false;

      var filepath = Filepath(provider);
      if (filepath == null)
        return false;

      string? sJson = File.Exists(filepath)
        ? File.ReadAllText(filepath)
        : null;

      var rootNode = sJson != null
        ? JsonNode.Parse(sJson, documentOptions: new JsonDocumentOptions() {CommentHandling = JsonCommentHandling.Skip})
        : new JsonObject();

      var rootObj = rootNode?.AsObject();
      if (rootObj == null)
        return false;

      var earlierKeys = Enumerable.Empty<string>();

      void HandleKeys(JsonObject parentNode, string? parentPath)
      {
        foreach (var key in provider.GetChildKeys(earlierKeys, parentPath).Distinct())
        {
          var fullKey = parentPath != null
            ? ConfigurationPath.Combine(parentPath, key)
            : key;
          if (provider.TryGet(fullKey, out var sValue))
          {
            var jsonValue = JsonValue.Create(sValue);
            parentNode[key] = jsonValue;
          }
          else
          {
            //probably a section
            var node = parentNode[key];
            var sectionObj = node == null
              ? (parentNode[key] = new JsonObject()).AsObject()
              : node.AsObject(); //this will throw if the node is not an object (e.g. there is a value with that key)
            HandleKeys(sectionObj, fullKey);
          }
        }
      }
      
      HandleKeys(rootObj,null);

      //save the json to file
      var options = new JsonSerializerOptions() { WriteIndented = true, TypeInfoResolver = JsonConfigurationHelperSourceGenerationContext.Default };
      sJson = rootNode?.ToJsonString(options);
      File.WriteAllText(filepath, sJson);
      return true;
    }

    internal static string? Filepath(FileConfigurationProvider fileConfigurationProvider)
    {
      IFileInfo? file = fileConfigurationProvider.Source.FileProvider?.GetFileInfo(fileConfigurationProvider.Source.Path ?? string.Empty);
      if (file == null)
        return null;
      return file.PhysicalPath;
    }

    public static bool SaveJsonProvider(this IConfigurationSection section)
    {
      var configurationSection = section as ConfigurationSection;
      if (configurationSection == null)
        return false;

      //see https://www.meziantou.net/accessing-private-members-without-reflection-in-csharp.htm
      [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_root")]
      extern static ref IConfigurationRoot GetConfigurationRootFromSection(ConfigurationSection @this);

      return SaveJsonProvider(GetConfigurationRootFromSection(configurationSection));
    }

    /// <summary>
    /// Get all items of a configuration section (without the item with the section's path and null value)
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Items(this IConfigurationSection section, bool includeSubSectionValues = false, bool includeNullValues = false)
    {
      return section.AsEnumerable()
        .Where(e => !(e.Value == null && e.Key == section.Path)
          && (includeSubSectionValues || ConfigurationPath.GetParentPath(e.Key) == section.Path)
          && (includeNullValues || e.Value != null));
      //a subsection is reported as a key (parentSection + sectionName but no keyName -> looks like a standard value) but with null value, we don't want to report this
      //even if a regular value has null value (in the file) it is reported as a value with EMPTY string!
      //-> filter all null values
      //however it is possible to have a value with null value, if you set this directly to null (it will be "" after loading a 'null' from json)
    }
  }

  [JsonSourceGenerationOptions(WriteIndented = true)]
  [JsonSerializable(typeof(string))]
  internal partial class JsonConfigurationHelperSourceGenerationContext : JsonSerializerContext
  {
  }
}
