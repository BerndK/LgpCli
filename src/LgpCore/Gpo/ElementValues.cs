using System.Collections.ObjectModel;
using LgpCore.AdmParser;
using System.Diagnostics.CodeAnalysis;
using Infrastructure;
using LgpCore.Infrastructure;

namespace LgpCore.Gpo
{
  public class ElementValues
  {
    public Policy Policy { get; }
    public PolicyClass PolicyClass { get; }
    public Dictionary<PolicyElement, object?> Values { get; private set; }
    public bool AllValuesSet => Values.All(e => e.Value != null || (e.Key.IsRequired().HasValue && !e.Key.IsRequired()!.Value));
    public Dictionary<PolicyElement, object> CompletedValues => Values.ToDictionary(
      e => e.Key, 
      e =>
      {
        var result = e.Value;
        if (result == null && e.Key.IsRequired().HasValue && !e.Key.IsRequired()!.Value)
          result = e.Key.NullValue() ?? throw new InvalidOperationException($"Default Value not set for {e.Key.Id} of {e.Key.Parent.PrefixedName()}");
        if (result == null)
          throw new InvalidOperationException($"Value not set for {e.Key.Id} of {e.Key.Parent.PrefixedName()}");
        return result;
      });
    public Source ValuesSource { get; set; }

    public ElementValues(Policy policy, PolicyClass policyClass, Dictionary<PolicyElement, object?> values, Source source)
    {
      policyClass.CheckClassIsNotBoth();

      //all values provided?
      var missingElements = policy.Elements
        .Where(e => !values.ContainsKey(e))
        .ToList();
      if (missingElements.Any())
        throw new InvalidOperationException($"Policy '{policy.PrefixedName()}' is missing values for elements: {string.Join(", ", missingElements.Select(e => e.Id))}");

      Policy = policy;
      PolicyClass = policyClass;
      Values = values;
      ValuesSource = source;
    }
    public enum Source
    {
      Unknown,
      Defaults,
      Edited,
      CommandLine,
      CurrentOnSystem
    }

    public void SetDefaults()
    {
      Values = Policy.DefaultValues();
      ValuesSource = ElementValues.Source.Defaults;
    }

    public bool GetCurrent()
    {
      var locValues = Policy.GetValues(this.PolicyClass);
      if (locValues != null && locValues.All(e => e.Value != null))
      {
        Values = locValues;
        ValuesSource = ElementValues.Source.CurrentOnSystem;
        return true;
      }

      return false;
    }

    public void FromBatchFile(List<(string key, List<string> values)> keyValues)
    {
      this.Values = this.Policy.ValuesFromCommandLine(keyValues)!;
      this.ValuesSource = ElementValues.Source.CommandLine;
    }

    public static ElementValues FromCommandLine(Policy policy, PolicyClass policyClass, List<(string key, List<string> values)> keyValues)
    {
      return new ElementValues(policy, policyClass, policy.ValuesFromCommandLine(keyValues)!, ElementValues.Source.CommandLine);
    }

    public static ElementValues? CurrentOnSystem(Policy policy, PolicyClass policyClass)
    {
      var values = policy.GetValues(policyClass);
      if (values == null)
        return null;
      return new ElementValues(policy, policyClass, values, ElementValues.Source.CurrentOnSystem);
    }

  }

  public static class PolicyElementValuesExtensions
  {
    public static ElementValues DefaultElementValues(this Policy policy, PolicyClass policyClass)
    {
      return new ElementValues(policy, policyClass, policy.DefaultValues(), ElementValues.Source.Defaults);
    }

    public static Dictionary<PolicyElement, object?> DefaultValues(this Policy policy)
    {
      return policy.Elements.ToDictionary(e => e, e => (e.IsRequired().HasValue && !e.IsRequired()!.Value) ? null : e.DefaultValue());
    }

    public static Dictionary<PolicyElement, object> ValuesFromCommandLine(this Policy policy, List<(string key, List<string> values)> keyValues)
    {
      return keyValues
        .Select(e =>
        {
          if (!policy.ValueFromCommandLine(e.key, e.values, out var policyElement, out var value))
            throw new InvalidOperationException($"Policy '{policy.PrefixedName()}' has no element with id '{e.key}'");
          return (policyElement, value);
        })
        .ToDictionary(e => e.policyElement, e => e.value);
    }

    public static bool ValueFromCommandLine(this Policy policy, string elemId, List<string> values, 
      [NotNullWhen(true)]out PolicyElement? policyElement,
      [NotNullWhen(true)] out object? value)
    {
      if (!policy.ElementById(elemId, out policyElement))
      {
        value = null;
        return false;
      };
      value = ValueFromCommandLine(policyElement, values);
      return true;
    }

    public static object ValueFromCommandLine(this PolicyElement policyElement, List<string> values)
    {
      switch (policyElement)
      {
        case ListElement listElement:
          if (listElement.ExplicitValue)
          {
            return values.Select(s =>
            {
              var parts = ToolBox.SplitEscaped(Escape.UnescapeStringFromCommandline(s),'=', 2);
              if (parts.Count != 2)
                throw new InvalidOperationException($"ListElement '{policyElement.Id}' should have key=value pairs, but has '{s}'");  
              return new KeyValuePair<string, string>(parts[0], parts[1]);
            }).ToList();
          }
          else
            return values
              .Select(Escape.UnescapeStringFromCommandline)
              .ToList();        
        case DecimalElement decimalElement:
          return uint.Parse(values.Single());
        case EnumElement enumElement:
          return values.Single();
        case BooleanElement booleanElement:
          return bool.Parse(values.Single());
        case LongDecimalElement longDecimalElement:
          return ulong.Parse(values.Single());
        case MultiTextElement multiTextElement:
          return values.
            Select(Escape.UnescapeStringFromCommandline)
            .ToArray();
        case TextElement textElement:
          return Escape.UnescapeStringFromCommandline(values.Single());
        default:
          throw new ArgumentOutOfRangeException(nameof(policyElement));
      }
    }

    public static string? ValuesToCommandLine(this Dictionary<PolicyElement, object> elemValues)
    {
      return string.Concat(elemValues
        .Select(e => ValueToCommandLine(e.Key, e.Value)));
    }

    //like "-k key1 -v value1 -v value2"
    public static string ValueToCommandLine(this PolicyElement policyElement, object value)
    {
      var values = policyElement.ValueToCommandLineValues(value, out var useQuotes);
      var quote = useQuotes ? "\"" : "";
      var sValues = string.Concat(values.Select(v => $" {CommandLine.ValueOption.ShortestAlias()} {quote}{v}{quote}"));
      return $" {CommandLine.KeyOption.ShortestAlias()} {policyElement.Id}{sValues}";
    }

    /// <summary>
    /// Convert the value of a PolicyElement to a list of strings for the command line
    /// </summary>
    public static List<string> ValueToCommandLineValues(this PolicyElement policyElement, object value, out bool useQuotes)
    {
      if (value == null)
        throw new InvalidOperationException($"ListElement '{policyElement.Id}' should have a value but is null");
      switch (policyElement)
      {
        case ListElement listElement:
          useQuotes = true;
          if (listElement.ExplicitValue)
          {
            if (value is not List<KeyValuePair<string, string>> kvps)
              throw new InvalidOperationException($"ListElement '{policyElement.Id}' should have key=value pairs, but has '{value}' ({value.GetType().Name})");
            return kvps.Select(kvp => Escape.EscapeStringForCommandline($"{ToolBox.EscapeSeparator(kvp.Key, '=')}={ToolBox.EscapeSeparator(kvp.Value, '=')}")).ToList();
          }
          else
          {
            if (value is not List<string> strings)
              throw new InvalidOperationException($"ListElement '{policyElement.Id}' should have a list of strings, but has '{value}' ({value.GetType().Name})");
            return strings
              .Select(Escape.EscapeStringForCommandline)
              .ToList();
          }        
        case DecimalElement decimalElement:
          useQuotes = false;
          if (value is not uint u)
            throw new InvalidOperationException($"DecimalElement '{policyElement.Id}' should have a uint value, but has '{value}' ({value.GetType().Name})");
          return new List<string>() {u.ToString()};
        case EnumElement enumElement:
          useQuotes = false;
          if (value is not string s)
            throw new InvalidOperationException($"EnumElement '{policyElement.Id}' should have a string value, but has '{value}' ({value.GetType().Name})");
          return new List<string>() {s};
        case BooleanElement booleanElement:
          useQuotes = false;
          if (value is not bool b)
            throw new InvalidOperationException($"BooleanElement '{policyElement.Id}' should have a bool value, but has '{value}' ({value.GetType().Name})");
          return new List<string>() { b.ToString() };
        case LongDecimalElement longDecimalElement:
          useQuotes = false;
          if (value is not ulong ul)
            throw new InvalidOperationException($"LongDecimalElement '{policyElement.Id}' should have a ulong value, but has '{value}' ({value.GetType().Name})");
          return new List<string>() { ul.ToString() };
        case MultiTextElement multiTextElement:
          useQuotes = true;
          if (value is not string[] stringsArr)
            throw new InvalidOperationException($"MultiTextElement '{policyElement.Id}' should have a string[] value, but has '{value}' ({value.GetType().Name})");
          return stringsArr.Select(Escape.EscapeStringForCommandline).ToList();
        case TextElement textElement:
          useQuotes = true; 
          if (value is not string s2)
            throw new InvalidOperationException($"TextElement '{policyElement.Id}' should have a string value, but has '{value}' ({value.GetType().Name})");
          return new List<string>() { Escape.EscapeStringForCommandline(s2) };
        default:
          throw new ArgumentOutOfRangeException(nameof(policyElement));
      }
    }

    public static Presentation? Presentation(this Policy policy)
    {
      if (policy.PresentationRef == null)
        return null;
      return policy.Content.GetPresentation(policy.PresentationRef);
    }

    /// <summary>
    /// Get the PresentationControl for the PolicyElement
    /// </summary>
    /// <param name="policyElement"></param>
    /// <returns>control</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static PresentationControlWithRefId PresentationControl(this PolicyElement policyElement)
    {
      var presentation = policyElement.Parent.Presentation() ?? throw new InvalidOperationException($"Policy should have a PresentationRef, if there is an Element, Policy:{policyElement.Parent.PrefixedName()}");
      return presentation.Controls
        .OfType<PresentationControlWithRefId>()
        .First(e => e.RefId == policyElement.Id);
    }

    public static PolicyElement Element(this PresentationControlWithRefId control, Policy policy)
    {
      return policy.Elements.First(e => e.Id == control.RefId) ?? throw new InvalidOperationException($"Policy has no element with id:'{control.RefId}', Policy:{policy.PrefixedName()}");
    }

    public static object? DefaultValue(this PolicyElement policyElement)
    {
      var control = policyElement.PresentationControl();
      return control switch
      {
        ComboBoxControl comboBoxControl => comboBoxControl.Default,
        CheckBoxControl checkBoxControl => checkBoxControl.DefaultChecked,
        DecimalTextBoxControl decimalTextBoxControl => decimalTextBoxControl.DefaultValue,
        DropdownListControl dropdownListControl => ((EnumElement)policyElement).Items[(int)dropdownListControl.DefaultItem].Id(),
        ListBoxControl listBoxControl => new List<string>(),
        LongDecimalTextBoxControl longDecimalTextBoxControl => longDecimalTextBoxControl.DefaultValue,
        MultiTextBoxControl multiTextBoxControl => new List<KeyValuePair<string, string>>(),
        TextBoxControl textBoxControl => textBoxControl.DefaultValueLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(control))
      };
    }

    public static string Id(this EnumItem enumItem) => AdmContent.DisplayStringId(enumItem.DisplayName);

    public static EnumItem? GetItem(this EnumElement enumElement, string? sEnumName, bool containsStringPrefix)
    {
      if (sEnumName == null)
        return null;
      var sEnumDisplayName = containsStringPrefix
        ? sEnumName
        : $"$(string.{sEnumName})";
      return enumElement.Items.FirstOrDefault(ei => string.Equals(ei.DisplayName, sEnumDisplayName, StringComparison.OrdinalIgnoreCase));
    }

    public static string TypeNameSlim(this PolicyElement policyElement)
    {
      var result = policyElement.GetType().Name;
      if (result.EndsWith("Element"))
        result = result[..^7];
      return result;
    }
  }
}
