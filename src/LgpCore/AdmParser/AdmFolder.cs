using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Infrastructure;

namespace LgpCore.AdmParser
{
  public partial class AdmFolder
  {
    private Dictionary<string, AdmContent>? contents;
    private LgpCategory? rootCategory;
    private List<LgpCategory>? allCategories;
    private Dictionary<string, Policy>? allPolicies;
    private List<SupportedProduct>? allSupportedProducts;
    private List<SupportedOnDefinition>? allSupportedOnDefinitions;
    private string language;

    public AdmFolder(string folder) : this (new DirectoryInfo(folder))
    { }

    public const string DefaultLanguage = "en-US";
    public AdmFolder(DirectoryInfo folder)
    {
      Folder = folder;

      language = Thread.CurrentThread.CurrentUICulture.Name; //DefaultLanguage;
    }

    public DirectoryInfo Folder { get; }

    public string Language
    {
      get => language;
      set
      {
        if (language != value)
        {
          language = value;
          if (contents != null)
          {
            Parallel.ForEach(contents.Values, content =>
            {
              content.Language = value;
              content.ParseResources();
            });
          }
        }
      }
    }

    [GeneratedRegex("[a-zA-Z]{2}-[a-zA-Z]{2}", RegexOptions.Compiled)]
    internal partial Regex LanguageFolderRegEx();

    public List<string> AvailableLanguages()
    {
      return Folder.GetDirectories()
        .Where(d => LanguageFolderRegEx().IsMatch(d.Name))
        .Select(d => d.Name)
        .ToList();
    }

    public static AdmFolder SystemDefault()
    {
      return new AdmFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "PolicyDefinitions"));
    }

    public async Task ParseAsync()
    {
      await Task.Run(() =>
      {
        this.contents = GetContents().ToDictionary(c => c.TargetNamespace.Namespace, StringComparer.OrdinalIgnoreCase);
        this.rootCategory = this.BuildRootCategory(out allCategories);
      });
    }

    public LgpCategory RootCategory
    {
      get => rootCategory ??= this.BuildRootCategory(out allCategories);
    }

    /// <summary>
    /// All contents (key is TargetNamespace)
    /// </summary>
    public Dictionary<string, AdmContent> Contents
    {
      get => contents ??= GetContents().ToDictionary(c => c.TargetNamespace.Namespace, StringComparer.OrdinalIgnoreCase);
    }

    public List<LgpCategory> AllCategories => allCategories ?? throw new NullReferenceException();

    /// <summary>
    /// All policies (key is PrefixedName)
    /// </summary>
    public Dictionary<string, Policy> AllPolicies
    {
      get
      {
        var localRootCategory = RootCategory; //make sure that Categories are created
        return allPolicies ??= Contents.Values.SelectMany(c => c.Policies).ToDictionary(AdmExtensions.PrefixedName, StringComparer.OrdinalIgnoreCase);
      }
    }

    public List<SupportedProduct> AllSupportedProducts
    {
      get
      {
        return allSupportedProducts ??= Contents.Values
          .Where(c => c.SupportedOn != null)
          .SelectMany(c => c.SupportedOn!.Products).ToList();
      }
    }

    public List<SupportedOnDefinition> AllSupportedOnDefinitions
    {
      get
      {
        return allSupportedOnDefinitions ??= Contents.Values
          .Where(c => c.SupportedOn != null)
          .SelectMany(c => c.SupportedOn!.Definitions).ToList();
      }
    }

    public List<AdmContent> GetContents()
    {
      var files = Folder.GetFiles("*.admx");

      ////Create structure for admx files
      //var contents = files
      //  .Select(fi =>
      //  {
      //    var admContent = new AdmContent(fi, Language);
      //    return admContent;
      //  })
      //  .ToList();
      //var docsRoots = contents
      //  .Select(c => XDocument.Load(c.AdmxFile.FullName).Root);
      //Console.WriteLine(docsRoots.MaxStructure());

      //return contents;
      //Create structure for adml files
      //var contents = files
      //  .Select(fi =>
      //  {
      //    var admContent = new AdmContent(fi, Language);
      //    return admContent;
      //  })
      //  .ToList();
      //var docsRoots = contents
      //  .Select(c => XDocument.Load(c.ResourceFile.FullName).Root);
      //Console.WriteLine(docsRoots.MaxStructure());

      //return contents;

      return files
        .AsParallel()
        .Select(fi =>
        {
          var admContent = new AdmContent(fi, Language, this);
          admContent.Parse();
          return admContent;
        })
        .ToList();
    }
  }
}
