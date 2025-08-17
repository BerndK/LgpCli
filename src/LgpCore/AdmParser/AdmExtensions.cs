using Infrastructure;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using LgpCore.Gpo;

namespace LgpCore.AdmParser
{
  public static partial class AdmExtensions
  {
    public static string? Prefix(this Policy policy) => policy?.Category?.CategoryIdent.PolicyNamespace.Prefix;
    public static string PrefixedName(this Policy p) => $"{p.Prefix()}.{p.Name}";
    public static string DisplayNameResolved(this Policy policy) => policy.Content.GetLocatedString(policy.DisplayName);
    public static SupportedOnDefinition? SupportedOn(this Policy policy)
    {
      if (string.IsNullOrWhiteSpace(policy.SupportedOnRef))
        return null;
      var admContent = policy.Content.GetNsRelatedContent(policy.SupportedOnRef, out var name);
      if (admContent == null)
        return null;
      return admContent.SupportedOn?.DefinitionsByName.GetValueOrDefault(name);

    }

    public static string? ExplainTextResolved(this Policy policy) =>
      policy.Content.GetLocatedStringNullable(policy.ExplainText);

    public static string DisplayNameResolved(this LgpCategory category) =>
      category.Content?.GetLocatedString(category.DisplayName) ?? category.DisplayName;

    public static string DisplayNameResolved(this SupportedProduct supportedProduct) =>
      supportedProduct.Content.GetLocatedString(supportedProduct.DisplayName);

    public static string DisplayNameResolved(this SupportedOnDefinition definition) =>
      definition.Content.GetLocatedString(definition.DisplayName);

    public static string DisplayNameResolved(this SupportedMinorVersion supportedMinorVersion) =>
      supportedMinorVersion.Content.GetLocatedString(supportedMinorVersion.DisplayName);

    public static string DisplayNameResolved(this SupportedMajorVersion supportedMajorVersion) =>
      supportedMajorVersion.Content.GetLocatedString(supportedMajorVersion.DisplayName);

    public static string DisplayNameResolved(this EnumItem enumItem) =>
      enumItem.Parent.Parent.Content.GetLocatedString(enumItem.DisplayName);

    public static string SRegKeyParented(this PolicyElement policyElement) =>
      policyElement.RegKey ?? policyElement.Parent.RegKey;

    public static string? RegValueNameParented(this PolicyElementBase policyElementBase) =>
      policyElementBase.RegValueName ?? policyElementBase.Parent.RegValueName;

    public static string? CategoryPath(this Policy? p)
    {
      string? BuildCategoryNames(LgpCategory? category)
      {
        if (category?.Parent == null)
          return category?.DisplayNameResolved();
        return $"{BuildCategoryNames(category.Parent)}\\{category.DisplayNameResolved()}";
      }
      
      if (p == null)
        return null;

      return BuildCategoryNames(p.Category);
    }

    public static string? CategoryPath(this LgpCategory? lgpCategory)
    {
      string? BuildCategoryNames(LgpCategory? category)
      {
        if (category?.Parent == null)
          return category?.DisplayNameResolved();
        return $"{BuildCategoryNames(category.Parent)}\\{category.DisplayNameResolved()}";
      }

      if (lgpCategory == null)
        return null;

      return BuildCategoryNames(lgpCategory);
    }

    public static List<string> CategoryPaths(this LgpCategory lgpCategory)
    {
      var result = new List<string>();

      void BuildCategoryNames(LgpCategory? category)
      {
        if (category?.Parent == null)
        {
          result.Add(category?.DisplayNameResolved() ?? string.Empty);
        }
        else
        {
          BuildCategoryNames(category.Parent);
          result.Add(category.DisplayNameResolved());
        }
      }

      BuildCategoryNames(lgpCategory);
      return result;
    }

    public static bool HasPolicyWithClass(this LgpCategory category, PolicyClass policyClass)
    {
      return category.GetPoliciesRecursive(policyClass).Any();
    }

    public static IEnumerable<Policy> GetPoliciesRecursive(this LgpCategory category, PolicyClass policyClass)
    {
      return category.Policies
        .Where(p => p.IsInClass(policyClass))
        .Concat(category.Items.SelectMany(c => c.GetPoliciesRecursive(policyClass)));
    }

    public static bool IsInClass(this Policy policy, PolicyClass policyClass)
    {
      return policy.Class == policyClass || policyClass == PolicyClass.Both || policy.Class == PolicyClass.Both;
    }

    public static void CheckClass(this Policy policy, PolicyClass @class)
    {
      if (!(policy.Class == @class || policy.Class == PolicyClass.Both))
        throw new InvalidOperationException($"Policy {policy.PrefixedName()} is not defined for {@class}");
    }

    public static void CheckClassIsNotBoth(this PolicyClass @class)
    {
      if (!(@class == PolicyClass.User || @class == PolicyClass.Machine))
        throw new InvalidOperationException($"Define Policy class to {PolicyClass.User} or {PolicyClass.Machine}");
    }

    public static Dictionary<PolicyElement, object> ToElementValues(this Policy policy,
      Dictionary<string, object> values)
    {
      var result = new Dictionary<PolicyElement, object>();
      foreach (var pair in values)
      {
        if (!policy.ElementById(pair.Key, out var element))
          throw new InvalidOperationException($"Element {pair.Key} not found in policy {policy.PrefixedName()}");
        result[element] = pair.Value;
      }

      return result;
    }

    public static bool ElementById(this Policy policy, string id, [NotNullWhen(true)] out PolicyElement? policyElement)
    {
      policyElement = policy.Elements.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
      return policyElement != null;
    }

    public static bool? IsRequired(this PolicyElement e)
    {
      switch (e)
      {
        //case ListElement listElement:
        case DecimalElement decimalElement:
          return decimalElement.Required;
        case EnumElement enumElement:
          return enumElement.Required;
        case BooleanElement booleanElement:
          return booleanElement.Required;
        case LongDecimalElement longDecimalElement:
          return longDecimalElement.Required;
        case MultiTextElement multiTextElement:
          return multiTextElement.Required;
        case TextElement textElement:
          return textElement.Required;
        default:
          return null;
      }
    }

    public static object? NullValue(this PolicyElement e)
    {
      switch (e)
      {
        case ListElement listElement:
          return listElement.ExplicitValue
            ? new List<KeyValuePair<string, string>>()
            : new List<string>();
        case DecimalElement decimalElement:
          return (uint)0;
        case EnumElement enumElement:
          return enumElement.Items.FirstOrDefault()?.Id();
        case BooleanElement booleanElement:
          return false;
        case LongDecimalElement longDecimalElement:
          return (ulong)0;
        case MultiTextElement multiTextElement:
          return Array.Empty<string>();
        case TextElement textElement:
          return string.Empty;
        default:
          return null;
      }
    }

    public static string LanguageDisplayName(string language)
    {
      CultureInfo? ci;
      try
      {
        ci = CultureInfo.GetCultureInfo(language);
      }
      catch (CultureNotFoundException)
      {
        ci = null;
      }

      if (ci == null)
        return language;
      return $"{ci.DisplayName} [{ci.EnglishName}]";
    }

    public static List<object> Search(this AdmFolder admFolder, string searchText, bool searchName, bool searchTitle,
      bool searchDescription, bool searchCategories, PolicyClass policyClass)
    {
      var result = new List<object>();

      if (searchCategories)
      {
        result.AddRange(admFolder.AllCategories
          .SearchItems(searchText, c => c.DisplayNameResolved(), "|", StringComparison.OrdinalIgnoreCase)
          .Where(c => c.HasPolicyWithClass(policyClass))
          );
      }

      if (searchName || searchTitle || searchDescription)
      {
        string StringSelector(Policy policy)
        {
          var s = string.Empty;
          if (searchName)
            s += policy.PrefixedName();
          if (searchTitle)
            s += policy.DisplayNameResolved();
          if (searchDescription)
            s += policy.ExplainTextResolved();
          return s;
        }

        result.AddRange(admFolder.AllPolicies.Values
          .SearchItems(searchText, StringSelector, "|", StringComparison.OrdinalIgnoreCase)
          .Where(p => p.IsInClass(policyClass))
        );
      }

      return result;
    }

    public static AdmContent? GetNsRelatedContent(this AdmContent localContent, string? @ref, out string name)
    {
      if (string.IsNullOrWhiteSpace(@ref))
      {
        name = string.Empty;
        return null;
      }
      if (AdmExtensions.TryParsePrefixedName(@ref, out var prefix, out name))
      {
        if (string.IsNullOrWhiteSpace(prefix))
          return localContent;
        var ns = localContent.UsingNamespaces.GetValueOrDefault(prefix);
        if (ns == null)
          return null;

        return localContent.Parent?.Contents.GetValueOrDefault(ns.Namespace);
      }
      else
        return null;
    }



    //resolves a prefixed Name by using the local using namespaces and then find the related Content (that defines the related namespace as target)
    public static PolicyNamespace? GetNs(this AdmContent localContent, string? prefix)
    {
      if (string.IsNullOrWhiteSpace(prefix))
        return localContent.TargetNamespace;
      var ns = localContent.UsingNamespaces.GetValueOrDefault(prefix);
      if (ns == null)
        return null;
      return localContent.Parent?.Contents.GetValueOrDefault(ns.Namespace)?.TargetNamespace;
    }

    //e.g. "$(string.BITS_TransferPolicyStandard)"
    [GeneratedRegex(@"^\$\((?<resourceType>[^.]+)\.(?<resourceKey>[^)]+)\)$", RegexOptions.Compiled)]
    public static partial Regex ResourceRegex();

    /// <summary>
    /// <seealso cref="AdmContent.DisplayStringId"/>
    /// </summary>
    public static bool TryParseResourceExpression(string expression, out string? resourceType, out string? resourceKey)
    {
      resourceType = default;
      resourceKey = default;

      if (string.IsNullOrWhiteSpace(expression))
        return false;

      var match = ResourceRegex().Match(expression);

      if (!match.Success)
        return false;

      resourceType = match.Groups["resourceType"].Value;
      resourceKey = match.Groups["resourceKey"].Value;
      return true;
    }

    //e.g. "windows:SUPPORTED_Windows_10_0" or "SUPPORTED_Windows_10_0"
    [GeneratedRegex(@"^((?<Prefix>\w+):)?(?<Name>.+)$", RegexOptions.Compiled)]
    public static partial Regex PrefixedNameRegex();

    public static bool TryParsePrefixedName(string prefixedName, out string? prefix, out string name)
    {
      var match = PrefixedNameRegex().Match(prefixedName);
      if (!match.Success)
      {
        prefix = default;
        name = string.Empty;
        return false;
      }
      var prefixGroup = match.Groups["Prefix"];
      prefix = prefixGroup.Success 
        ? prefixGroup.Value 
        : default;
      name = match.Groups["Name"].Value;
      return true;
    }
  }
}
