using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using LgpCore.AdmParser;
using LgpCore.Gpo;

namespace LgpCoreTests
{
  public class AdmParserTests
  {
    [Test]
    public void ParseAllTest()
    {
      var sw = Stopwatch.StartNew();
      var admFolder = AdmFolder.SystemDefault();
      var contents = admFolder.GetContents();
      sw.Stop();
      Console.WriteLine($"Load {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Explicit]
    [Test]
    public void ParseCameraTest()
    {
      var admContent = new AdmContent(@"C:\Windows\PolicyDefinitions\Camera.admx", "en-US", null);
      admContent.Parse();
    }

    [Test]
    public async Task BuildRootCategoryTest()
    {
      var sw = Stopwatch.StartNew();
      var admFolder = AdmFolder.SystemDefault();
      await admFolder.ParseAsync();
      sw.Stop();
      Console.WriteLine($"Parse {sw.Elapsed.TotalMilliseconds:0.0}ms");

      var sTree = TreeVisualizer.Visualize(
        admFolder.RootCategory,
        lc => lc.Items,
        lc => $"[{lc.CategoryIdent.PolicyNamespace.Prefix}|{lc.Name}] {lc.DisplayNameResolved()} {lc.Policies.Count}",
        out var leafs);

      Console.WriteLine(sTree);
    }

    [Test]
    public async Task ListAllPoliciesTest()
    {
      var sw = Stopwatch.StartNew();
      var admFolder = AdmFolder.SystemDefault();
      await admFolder.ParseAsync();
      var dict = admFolder.AllPolicies;
      sw.Stop();
      Console.WriteLine($"Parse {sw.Elapsed.TotalMilliseconds:0.0}ms");

      Console.WriteLine($"{dict.Count} Policies");
      foreach (var policy in dict.Values)
      {
        Console.WriteLine($"{ policy.Prefix()}.{ policy.Name} [{ policy.DisplayNameResolved()}]");
      }
    }

    [Explicit]
    [Test]
    public void Statistics()
    {
      var admFolder = AdmFolder.SystemDefault();
      //admFolder.Parse();
      var enabledCount = admFolder.AllPolicies.Values.GroupBy(p => p.EnabledList.Count);
      Console.WriteLine("EnabledList");
      foreach (var group in enabledCount.OrderBy(g => g.Key))
      {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
      }
      Console.WriteLine();

      Console.WriteLine("DisabledList");
      var disabledCount = admFolder.AllPolicies.Values.GroupBy(p => p.DisabledList.Count);
      foreach (var group in disabledCount.OrderBy(g => g.Key))
      {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
      }
      Console.WriteLine();

      Console.WriteLine("Elements");
      var elementsCount = admFolder.AllPolicies.Values.GroupBy(p => p.Elements.Count);
      foreach (var group in elementsCount.OrderBy(g => g.Key))
      {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
      }
      Console.WriteLine();

      Console.WriteLine("EnabledList vs DisabledList");
      foreach (var e in admFolder.AllPolicies.Values.CountBy(p => $"{p.EnabledList.Count} - {p.DisabledList.Count}"))
      {
        Console.WriteLine($"  {e.Key}: {e.Value}");
      }
      Console.WriteLine();

      Console.WriteLine("DisabledList without EnabledList");
      foreach (var p in admFolder.AllPolicies.Values.Where(p => p.EnabledList.Count == 0  && p.DisabledList.Count > 0))
      {
        Console.WriteLine($"  {p.PrefixedName()} {p.EnabledList.Count} - {p.DisabledList.Count}");
      }
      Console.WriteLine();



      var names = admFolder.AllPolicies.Values
        .Select(p => (name:p.Name, policy:p))
        .GroupBy(n => n.name, e => e.policy)
        .Where(g => g.Count() != 1)
        .ToList();
      Console.WriteLine($"{names.Count} Names are not unique");
      foreach (var group in names)
      {
        Console.WriteLine($"  {group.Key}");
        foreach (var policy in group)
        {
          Console.WriteLine($"    {policy.Prefix()}.{policy.Name} [{policy.DisplayNameResolved()}]");
        }
      }

      var prefixedNames = admFolder.AllPolicies.Values
        .Select(p => $"{p.Prefix()}.{p.Name}")
        .GroupBy(n => n)
        .Where(g => g.Count() != 1)
        .ToList();
      Console.WriteLine($"{prefixedNames.Count} PrefixedNames are not unique");

      var polsWithoutPrefixCount = admFolder.AllPolicies.Values
        .Select(p => p.Prefix())
        .Where(e => string.IsNullOrEmpty(e))
        .Count();
      Console.WriteLine($"{polsWithoutPrefixCount} policies without Prefix");

      var polsWithDotInNameCount = admFolder.AllPolicies.Values
        .Where(p => p.Name.Contains('.'))
        .Count();
      Console.WriteLine($"{polsWithDotInNameCount} policies with '.' in Name");

      var bothPolicies = admFolder.AllPolicies.Values
        .Where(p => p.Class == PolicyClass.Both)
        .ToList();
      Console.WriteLine($"{bothPolicies.Count} policies have class 'both'");

      var regValueNameEmptyPolicies = admFolder.AllPolicies.Values
        .Where(p => p.RegValueName == null)
        .ToList();
      Console.WriteLine($"{regValueNameEmptyPolicies.Count} policies have no RegValueName");

      var regEnumItemsNames = admFolder.AllPolicies.Values
        .SelectMany(p => p.Elements)
        .OfType<EnumElement>()
        .SelectMany(e => e.Items.Select(ei => ei.DisplayName))
        .ToList();
      Console.WriteLine($"{regEnumItemsNames.Count} enumitem names, {regEnumItemsNames.Count(s => !s.StartsWith("$(string."))} does not start with '$(string.'");

      var listElementsWithNoRegKey = admFolder.AllPolicies.Values
        .SelectMany(p => p.Elements)
        .OfType<ListElement>()
        .Where(e => e.RegKey == null || string.Equals(e.RegKey, e.Parent.RegKey))
        .ToList();
      Console.WriteLine($"{listElementsWithNoRegKey.Count} listElements with suspect RegKey");

      var requiredValues = admFolder.AllPolicies.Values
        .SelectMany(p => p.Elements)
        .Select(e =>
        {
          switch (e)
          {
            //case ListElement listElement:
            case DecimalElement decimalElement:
              return (typeof(DecimalElement), decimalElement.Required);
            case EnumElement enumElement:
              return (typeof(EnumElement), enumElement.Required);
            //case BooleanElement booleanElement:
            case LongDecimalElement longDecimalElement:
              return (typeof(LongDecimalElement), longDecimalElement.Required);
            case MultiTextElement multiTextElement:
              return (typeof(MultiTextElement), multiTextElement.Required);
            case TextElement textElement:
              return (typeof(TextElement), textElement.Required);
            default:
              return ((Type?)null, (bool?)null);
          }
        })
        .Where(t => t.Item1 != default)
        .GroupBy(e => e.Item1!, e=> e.Item2.GetValueOrDefault())
        .ToList();
      Console.WriteLine($"Required values:");
      foreach (var g in requiredValues)
      {
        Console.WriteLine($"  {g.Key.Name} True:{g.Count(e => e)}, False:{g.Count(e => !e)}");
      }

      var boolElementInfos = admFolder.AllPolicies.Values
        .SelectMany(p => p.Elements)
        .OfType<BooleanElement>()
        .Select(e => ($"TrueValues {e.TrueValues.Count}:{string.Join(", ", e.TrueValues.Select(v => v.Value.GetType().Name))} FalseValues {e.FalseValues.Count}:{string.Join(", ", e.FalseValues.Select(v => v.Value.GetType().Name))} Required {e.IsRequired().ToString() ?? "<null>"}", e))
        .GroupBy(e => e.Item1, e => e.e)
        .ToList();
      Console.WriteLine($"BooleanElementInfos:");
      foreach (var boolElementInfo in boolElementInfos)
      {
        Console.WriteLine($"  {boolElementInfo.Count()} - {boolElementInfo.Key}");
        foreach (var be in boolElementInfo.Take(5))
        {
          Console.WriteLine($"    {be.Id} of policy {be.Parent.PrefixedName()}");  
        }
      }

      var justBoolElementInfos = admFolder.AllPolicies.Values
        .Where(p => p.Elements.Any())
        .Where(p => p.Elements.All(e => e is BooleanElement))
        .Where(p => p.Elements.OfType<BooleanElement>().All(e => !e.TrueValues.Any()))
        .ToList();
      Console.WriteLine($"JustBooleanElementInfos (with no dedicated TrueValues):");
      foreach (var p in justBoolElementInfos)
      {
        Console.WriteLine($"  {p.PrefixedName()} - {p.Name}");
      }

      Console.WriteLine($"BooleanElements with string:");
      foreach (var booleanElement in admFolder.AllPolicies.Values
                 .SelectMany(p => p.Elements)
                 .OfType<BooleanElement>())
      {
        if (booleanElement.TrueValues.Count > 0 && booleanElement.TrueValues.Any(e => e.Value is StringValue))
        {
          Console.WriteLine($"  {booleanElement.Parent.PrefixedName()}");
        }
      }

      var noEnDisables = admFolder.AllPolicies.Values
        .Where(p => p.EnabledList.Count == 0)
        .Where(p => p.EnabledValue == null)
        .Where(p => p.DisabledList.Count == 0)
        .Where(p => !string.IsNullOrWhiteSpace(p.RegValueName))
        .Where(p => p.Elements.Any())
        .Where(p =>
        {
          var policyBaseElements = p.Elements.OfType<PolicyElementBase>().ToList();
          return !policyBaseElements.Any() || policyBaseElements.All(e => !string.Equals(e.RegValueName, p.RegValueName, StringComparison.OrdinalIgnoreCase));
        })
        .ToList();
      Console.WriteLine($"No EnableList, no DisableList: {noEnDisables.Count}");
      foreach (var p in noEnDisables)
      {
        Console.WriteLine($"  Elements:{p.Elements.Count} {p.PrefixedName()} - {p.Name}");
      }

      var contentNamespaces = admFolder.Contents.Values
        .Select(c => c.TargetNamespace)
        .ToList();
      Console.WriteLine($"{admFolder.Contents.Count} Contents, {contentNamespaces.Count} ContentNs, {contentNamespaces.DistinctBy(ns=>ns.Namespace).Count()} NameSpaces, {contentNamespaces.DistinctBy(ns=>ns.Prefix).Count()} Prefixes");
    }

    [Explicit]
    [TestCase(true)]
    [TestCase(false)]
    public void SubKeyElementTest(bool filterSameEntries)
    {
      var admFolder = AdmFolder.SystemDefault();
      var regKeys = admFolder.AllPolicies.Values
        .SelectMany(p => p.Elements.Where(e => !filterSameEntries || !string.Equals(p.RegKey, e.RegKey, StringComparison.OrdinalIgnoreCase)))
        .Select(pe => pe.RegKey)
        .ToList();

      Console.WriteLine($"{regKeys.Count} RegKeys");
      foreach (var regKey in regKeys)
      {
        Console.WriteLine($"{regKey}");
      }
    }

    [Explicit]
    [TestCase(true)]
    [TestCase(false)]
    public void SubKeyValueTest(bool filterSameEntries)
    {
      var admFolder = AdmFolder.SystemDefault();
      var regKeys = admFolder.AllPolicies.Values
        .SelectMany(p => p.EnabledList.Where(e => !filterSameEntries || !string.Equals(p.RegKey, e.RegKey, StringComparison.OrdinalIgnoreCase))
          .Concat(p.DisabledList).Where(e => !filterSameEntries || string.Equals(p.RegKey, e.RegKey, StringComparison.OrdinalIgnoreCase)))
        .Select(pvi => pvi.RegKey)
        .ToList();

      Console.WriteLine($"{regKeys.Count} RegKeys");
      foreach (var regKey in regKeys)
      {
        Console.WriteLine($"{regKey}");
      }
    }

    [Explicit]
    [TestCase("Favorites")]
    [TestCase("ConfigureHTTPProxySettings")]
    [TestCase("BITS_SetTransferPolicyOnCostedNetwork")]
    [TestCase("DefaultIndexedPaths_1")]
    [TestCase("DeployAccelerators_1")]
    [TestCase("DeployAccelerators_2")]
    [TestCase("ExplorerRibbonStartsMinimized")]
    [TestCase("EnableSmartScreen")]
    [TestCase("CPL_Personalization_ScreenSaverTimeOut")]
    [TestCase("SiteDiscoveryEnableWMI")]
    public void FindPolicyByName(string policyName)
    {
      var admFolder = AdmFolder.SystemDefault();
      admFolder.Language = "de-DE"; // "en-US
      var policies = admFolder.AllPolicies.Values
        .Where(p => p.Name.Contains(policyName, StringComparison.OrdinalIgnoreCase))
        .ToList();

      foreach (var policy in policies)
      {
        Console.WriteLine($"{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      }
    }

    [Explicit]
    [Test]
    public void ListProductsAndSupports()
    {
      var admFolder = AdmFolder.SystemDefault();
      Console.WriteLine($"{admFolder.AllSupportedProducts.Count} Products");
      foreach (var product in admFolder.AllSupportedProducts)
      {
        Console.WriteLine($"{product.Name} '{product.DisplayNameResolved()}' {product.MajorVersions.Count} MajorVersions");
        foreach (var majorVersion in product.MajorVersions)
        {
          Console.WriteLine($"  {majorVersion.Name}[{majorVersion.VersionIndex}] '{majorVersion.DisplayNameResolved()}' {majorVersion.MinorVersions.Count} MinorVersions");
          foreach (var minorVersion in majorVersion.MinorVersions)
          {
            Console.WriteLine($"    {minorVersion.Name}[{minorVersion.VersionIndex}] '{minorVersion.DisplayNameResolved()}'");
          }
        }
      }

      Console.WriteLine();

      Console.WriteLine($"{admFolder.AllSupportedOnDefinitions.Count} Definitions");

      foreach (var def in admFolder.AllSupportedOnDefinitions)
      {
        Console.WriteLine($"{def.Name} '{def.DisplayNameResolved()}'");
        if (def.Condition != null)
        {
          if (def.Condition.Ranges.Count > 0)
          {
            Console.WriteLine($"  {def.Condition.Ranges.Count} Ranges");
            foreach (var range in def.Condition.Ranges)
            {
              Console.WriteLine($"  Min:{range.MinVersionIndex} - Max:{range.MaxVersionIndex} Ref:{range.Ref}");
            }
          }

          if (def.Condition.References.Count > 0)
          {
            Console.WriteLine($"  {def.Condition.References.Count} References");
            foreach (var reference in def.Condition.References)
            {
              Console.WriteLine($"  {reference.Ref}");
            }
          }
        }
      }
    }


  }
}
