using Cli;
using LgpCore.AdmParser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using System.Diagnostics;
using LgpCore.Gpo;

namespace LgpCli
{
  public static class EditValuesCli
  {
    public static void ShowPage(IServiceProvider serviceProvider, ElementValues elementValues)
    {
      var policy = elementValues.Policy;
      bool loop = true;
      var logger = serviceProvider.GetRequiredService<ILogger>();
      do
      {
        CliErrorHandling.HandleShowExceptions(() =>
        {
          Console.Clear();

          CliTools.WriteLine(CliTools.TitleColor, $"Edit Values for:");
          CliTools.Markup($"[Policy]Policy[/]");
          CliTools.WriteLine(CliTools.TitleColor, $"      : {policy.DisplayNameResolved()}");
          CliTools.MarkupLine($"[Category]Category[/]    : {string.Join("\\", policy.Category?.CategoryPaths() ?? new List<string>())}");
          CliTools.MarkupLine($"PrefixedName: [PrefixedName]{policy.PrefixedName()}[/]");
          CliTools.MarkupLine($"Class       : [Class]{elementValues.PolicyClass}[/]");
          Console.WriteLine("---------------------------------------------------------------------------");

          if (elementValues.Values.Any())
            CliTools.Markup($"[White]{elementValues.Values.Count} values[/]:");
          else
            CliTools.Markup("[White]no values[/]");
          CliTools.MarkupLine($" (source: [White]{elementValues.ValuesSource}[/])");

          var menuItems = new List<MenuItem>();
          var presentation = elementValues.Policy.Presentation();
          if (presentation != null)
          {
            foreach (var control in presentation.Controls)
            {
              Console.WriteLine();             
              if (control is TextControl textControl)
              {
                CliTools.MarkupLine($"{textControl.Text}");
              }
              else
              {
                bool GetElement<T>([NotNullWhen(true)] out T? policyElement) where T : PolicyElement
                {
                  //all other controls should have RefId and shall be linked to an Element
                  var element = (control as PresentationControlWithRefId)?.Element(policy);
                  if (element == null)
                  {
                    CliTools.MarkupLine($"[Red]Element not found for {control.GetType().Name}: {(control as PresentationControlWithRefId)?.RefId} [/]");
                    policyElement = null;
                    return false;
                  }
                  policyElement = element as T;
                  if (policyElement == null)
                    CliTools.MarkupLine($"[Red]Element is not of type {typeof(T).Name} but is {element.GetType().Name} [/]");
                  else
                    CliTools.MarkupLine($"[ElemType]{policyElement.TypeNameSlim()}[/] [ElemId]{policyElement.Id}[/]");
                  return policyElement != null;
                }

                bool GetValue<T>(PolicyElement policyElement, out T? value)
                {
                  if (elementValues.Values.TryGetValue(policyElement, out var locValue))
                  {
                    if (locValue == null)
                    {
                      value = default!;
                      return true; //true, because the item is found, but value is not yet set
                    }
                    if (locValue is T locT)
                    {
                      value = locT;
                      return true;
                    }
                    CliTools.MarkupLine($"[Red]Value is not of type {typeof(T).Name} but is {locValue.GetType().Name} [/]");
                    value = default!;
                    return false;
                  }
                  CliTools.MarkupLine($"[Red]Element not found for {policyElement.GetType().Name}:{policyElement.Id}[/]");
                  value = default!;
                  return false;
                }

                //Build the presentation items
                //this is done in two steps,
                //a) the controls including values are shown here directly
                //b) the related values are edited in separate methods (via Menuitems (these items are hidden - they are registered and handled, but not printed at all, the number is printed manually))

                switch (control)
                {
                  case ComboBoxControl comboBoxControl: //seems to be not used ('<comboBox' not in the ADMX file) Which element is this for? They just use DropDownList
                    CliTools.MarkupLine(comboBoxControl.Label);
                    CliTools.MarkupLine($"[DarkYellow]ComboBoxControl is not supported!!![/]");
                    break;
                  case DecimalTextBoxControl decimalTextBoxControl: //DecimalElement->DecimalTextBoxControl
                  {
                    if (GetElement<DecimalElement>(out var decimalElement) && GetValue<uint>(decimalElement, out var value))
                    {
                      menuItems.Add(new MenuItem(string.Empty, () => EditDecimal(decimalTextBoxControl, decimalElement, elementValues)) {HiddenButActive = true});
                      CliTools.MarkupLine(decimalTextBoxControl.Text);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count} [/][[ [White]{value}[/] ]]");
                    }

                    break;
                  }
                  case DropdownListControl dropdownListControl: //EnumElement->DropdownListControl
                  {
                    if (GetElement<EnumElement>(out var enumElement) && GetValue<string>(enumElement, out var value))
                    {
                      var enumItem = enumElement.GetItem(value, false);
                      menuItems.Add(new MenuItem(string.Empty, () => EditDropdownList(dropdownListControl, enumElement, elementValues)) { HiddenButActive = true });
                      CliTools.MarkupLine(dropdownListControl.Text);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count} [/][[ [White]{value}[/] \u25bc]] '{enumItem?.DisplayNameResolved()}'"); //▼⤋↓🡓⇓ see also http://xahlee.info/comp/unicode_arrows.html or charmap (windows)
                    }

                    break;
                  }
                  case ListBoxControl listBoxControl: //ListElement->ListBoxControl
                  {
                    if (GetElement<ListElement>(out var listElement))
                    {
                      if (!listElement.ExplicitValue)
                      {
                        if (GetValue<List<string>>(listElement, out var values))
                        {
                          menuItems.Add(new MenuItem(string.Empty, () => EditListBoxNonExplicit(listBoxControl, listElement, elementValues)) { HiddenButActive = true });
                          CliTools.MarkupLine(listBoxControl.Text);
                          values ??= new List<string>();
                          CliTools.MarkupLine($"[Yellow] {menuItems.Count}[/] {values.Count} items");
                          foreach (var item in values)
                            CliTools.MarkupLine($"  [[ '[White]{item}[/]' ]]");
                        }
                      }
                      else
                      {
                        if (GetValue<List<KeyValuePair<string, string>>>(listElement, out var values))
                        {
                          menuItems.Add(new MenuItem(string.Empty, () => EditListBoxExplicit(listBoxControl, listElement, elementValues)) { HiddenButActive = true });
                          CliTools.MarkupLine(listBoxControl.Text);
                          values ??= new List<KeyValuePair<string, string>>();
                          CliTools.MarkupLine($"[Yellow] {menuItems.Count}[/] {values.Count} items");
                          foreach (var pair in values)
                            CliTools.MarkupLine($"  [[ '{pair.Key}':'[White]{pair.Value}[/]' ]]");
                        }
                      }
                    }

                    break;
                  }
                  case LongDecimalTextBoxControl longDecimalTextBoxControl: //LongDecimalElement->LongDecimalTextBoxControl (not used!?)
                  {
                    if (GetElement<LongDecimalElement>(out var longDecimalElement) && GetValue<ulong>(longDecimalElement, out var value))
                    {
                      menuItems.Add(new MenuItem(string.Empty, () => EditLongDecimal(longDecimalTextBoxControl, longDecimalElement, elementValues)) { HiddenButActive = true });
                      CliTools.MarkupLine(longDecimalTextBoxControl.Text);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count} [/][[ [White]{value}[/] ]]");
                    }

                    break;
                  }
                  case MultiTextBoxControl multiTextBoxControl: //MultiTextElement->MultiTextBoxControl
                  {
                    if (GetElement<MultiTextElement>(out var multiTextElement) && GetValue<string[]>(multiTextElement, out var values))
                    {
                      menuItems.Add(new MenuItem(string.Empty, () => EditMultiText(multiTextBoxControl, multiTextElement, elementValues)) { HiddenButActive = true });
                      values ??= Array.Empty<string>();
                      CliTools.MarkupLine(multiTextBoxControl.Text);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count}[/] {values.Length} items");
                      foreach (var item in values)
                        CliTools.MarkupLine($"  [[ '[White]{item}[/]' ]]");
                    }
                    break;
                  }
                  case CheckBoxControl checkBoxControl: //BooleanElement->CheckBoxControl
                  {
                    if (GetElement<BooleanElement>(out var booleanElement) && GetValue<bool>(booleanElement, out var value))
                    {
                      menuItems.Add(new MenuItem(string.Empty, () => EditBoolean(checkBoxControl, booleanElement, elementValues)) { HiddenButActive = true });
                      CliTools.MarkupLine(checkBoxControl.Text);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count}[/] [[ [White]{value}[/] ]]");
                    }
                    break;
                  }
                  case TextBoxControl textBoxControl: //TextElement->TextBoxControl
                  {
                    if (GetElement<TextElement>(out var textElement) && GetValue<string>(textElement, out var value))
                    {
                      menuItems.Add(new MenuItem(string.Empty, () => EditText(textBoxControl, textElement, elementValues)) { HiddenButActive = true });
                      CliTools.MarkupLine(textBoxControl.Label);
                      CliTools.MarkupLine($"[Yellow] {menuItems.Count}[/] [[ '[White]{value}[/]' ]]");
                    }
                    break;
                  }
                  case TextControl: //no Element, just text, has been handled above
                  default:
                    throw new ArgumentOutOfRangeException(nameof(control));

                }
              }
            }
          }
          
          menuItems.AddSeparator("--- editor functions ---");
          menuItems.Add("GC", "Get current values from system", () => { PolicyCli.GetCurrentValuesFromSystem(elementValues); });
          menuItems.Add("GD", "Get default values", () => elementValues.SetDefaults());
          menuItems.Add("GB", "Get Values from current batch file", () => PolicyCli.GetValuesFromBatchFile(serviceProvider, elementValues), () => true);

          menuItems.Add("Esc", "Exit", () => { loop = false; });

          CliTools.ShowMenu("select the number of the Control/Element you want to modify", menuItems.ToArray());
        }, logger);
      } while (loop);

    }

    private static void EditDecimal(DecimalTextBoxControl decimalTextBoxControl, DecimalElement decimalElement, ElementValues elementValues)
    {
      while (true)
      {
        var defaultValue = elementValues.Values.TryGetValue(decimalElement, out var locValue) && locValue is uint locUint
          ? locUint.ToString()
          : decimalTextBoxControl.DefaultValue.ToString();
        if (!CliTools.InputQuery(decimalTextBoxControl.Text, out var sValue, defaultValue))
        {
          return; //escape
        }
        
        if (uint.TryParse(sValue, out var value))
        {
          elementValues.Values[decimalElement] = value;
          elementValues.ValuesSource = ElementValues.Source.Edited;
          return;
        }
        else
        {
          CliTools.WarnMessage($"Not able to convert '{sValue}' to uint, try again");
        }
      }
    }

    private static void EditLongDecimal(LongDecimalTextBoxControl longDecimalTextBoxControl, LongDecimalElement longDecimalElement, ElementValues elementValues)
    {
      while (true)
      {
        var defaultValue = elementValues.Values.TryGetValue(longDecimalElement, out var locValue) && locValue is ulong locUlong
          ? locUlong.ToString()
          : longDecimalTextBoxControl.DefaultValue.ToString();
        if (!CliTools.InputQuery(longDecimalTextBoxControl.Text, out var sValue, defaultValue))
        {
          return; //escape
        }

        if (ulong.TryParse(sValue, out var value))
        {
          elementValues.Values[longDecimalElement] = value;
          elementValues.ValuesSource = ElementValues.Source.Edited;
          return;
        }
        else
        {
          CliTools.WarnMessage($"Not able to convert '{sValue}' to ulong, try again");
        }
      }
    }

    private static void EditBoolean(CheckBoxControl checkBoxControl, BooleanElement booleanElement, ElementValues elementValues)
    {
      //just toggle the value (no interactive input here)
      var oldValue = (elementValues.Values[booleanElement] as bool?) ?? checkBoxControl.DefaultChecked;
      elementValues.Values[booleanElement] = !oldValue;
      elementValues.ValuesSource = ElementValues.Source.Edited;
    }

    public static void EditComboBox(ComboBoxControl control, PolicyElement policyElement)
    {
      throw new NotImplementedException("The ComboBoxControl is not supported so far");
      //no idea to which policyElement this control belongs, seems that they use DropdownListControl instead
    }

    private static void EditDropdownList(DropdownListControl dropdownListControl, EnumElement enumElement, ElementValues elementValues)
    {
      var enumItems = enumElement.Items;
      var defaultItem = elementValues.Values[enumElement] != null
        ? enumElement.GetItem((string)elementValues.Values[enumElement]!, false)
        : enumItems[(int)dropdownListControl.DefaultItem];
      if (!dropdownListControl.NoSort)
        enumItems = enumItems.OrderBy(e => e.DisplayNameResolved()).ToList();
      if (CliTools.SelectItem(enumItems, dropdownListControl.Text, defaultItem, out var selected, e => $"{e?.DisplayNameResolved()} [DarkGray]{e?.Id()}[/]"))
      {
        elementValues.Values[enumElement] = selected.Id();
        elementValues.ValuesSource = ElementValues.Source.Edited;
      }
    }

    private static void EditText(TextBoxControl textBoxControl, TextElement textElement, ElementValues elementValues)
    {
      var oldValue = (elementValues.Values[textElement] as string) ?? textBoxControl.DefaultValueLabel;
      if (CliTools.InputQuery(textBoxControl.Label, out var newValue, oldValue))
      {
        if (string.IsNullOrWhiteSpace(newValue) && textElement.Required)
        {
          CliTools.WarnMessage("You need to provide a value.");
          return;
        }
        elementValues.Values[textElement] = newValue;
        elementValues.ValuesSource = ElementValues.Source.Edited;
      }
    }

    private static void EditMultiText(MultiTextBoxControl multiTextBoxControl, MultiTextElement multiTextElement, ElementValues elementValues)
    {
      var oldValues = (elementValues.Values[multiTextElement] as string[]) ?? Array.Empty<string>();
      var newValues = EditMultipleStrings(multiTextBoxControl.Text, oldValues);
      if (newValues != null)
      {
        if (!newValues.Any() && multiTextElement.Required)
        {
          CliTools.WarnMessage("You need to provide a value.");
          return;
        }
        elementValues.Values[multiTextElement] = newValues.ToArray();
        elementValues.ValuesSource = ElementValues.Source.Edited;
      }
    }

    private static void EditListBoxNonExplicit(ListBoxControl listBoxControl, ListElement listElement, ElementValues elementValues)
    {
      var oldValues = (elementValues.Values[listElement] as List<string>) ?? new List<string>();
      var newValues = EditMultipleStrings(listBoxControl.Text, oldValues);
      if (newValues != null)
      {
        if (!newValues.Any())
        {
          CliTools.WarnMessage("You need to provide a value.");
          return;
        }
        elementValues.Values[listElement] = newValues;
        elementValues.ValuesSource = ElementValues.Source.Edited;
      }
    }

    private static void EditListBoxExplicit(ListBoxControl listBoxControl, ListElement listElement, ElementValues elementValues)
    {
      var oldValues = (elementValues.Values[listElement] as List<KeyValuePair<string, string>>) ?? new List<KeyValuePair<string, string>>();
      var newValues = EditKvps(listBoxControl.Text, oldValues);
      if (newValues != null)
      {
        if (!newValues.Any())
        {
          CliTools.WarnMessage("You need to provide a value.");
          return;
        }
        elementValues.Values[listElement] = newValues;
        elementValues.ValuesSource = ElementValues.Source.Edited;
      }
    }

    public static List<string>? EditMultipleStrings(string prompt, IEnumerable<string> oldValues)
    {
      List<string>? values = new List<string>(oldValues);
      bool loop = true;
      do
      {
        CliTools.MarkupLine($"{prompt}");
        CliTools.MarkupLine($"{values.Count} items");
        var menuItems = new List<MenuItem>();

        for (var i = 0; i < values.Count; i++)
        {
          var value = values[i];
          var index = i;
          menuItems.Add($"[[ '[White]{value}[/]' ]]", () =>
          {
            if (CliTools.InputQuery("Modify value", out var newValue, value))
            {
              values[index] = newValue;
            }
          });
        }

        menuItems.AddSeparator("--- editor functions ---");
        menuItems.Add("+", "Add Item", () => { values.Add(string.Empty); });
        menuItems.Add("-", "Remove Item", () => {
          if (CliTools.InputQuery("Enter number to delete", out var sNo))
          {
            if (int.TryParse(sNo, out var no))
            {
              no--; //1-based to 0-based
              if (no >= 0 && no < values.Count)
                values.RemoveAt(no);
              else
                CliTools.WarnMessage("Number out of range");
            }
          }
        });
        menuItems.Add("EX", "Use external default Editor to edit list", () =>
        {
          using (var tempFile = ToolBox.CreateTempFile())
          {
            File.WriteAllLines(tempFile.Value, values);
            var process = new Process
            {
              StartInfo = new ProcessStartInfo
              {
                FileName = tempFile.Value,
                UseShellExecute = true,
                //Arguments = args,
                Verb = "edit",
              }
            };

            process.Start();
            Console.WriteLine("Edit file in editor that should popup, save file and close editor to continue.");
            Console.WriteLine("Wait for editor to exit...");
            process.WaitForExit();
            values = File.ReadAllLines(tempFile.Value)
              .Where(line => !string.IsNullOrWhiteSpace(line))
              .ToList();
          }
        });
        menuItems.Add("OK", "Quit editing", () => { loop = false; });
        menuItems.Add("Esc", "Exit", () =>
        {
          values = null;
          loop = false;
        });

        CliTools.ShowMenu(null, menuItems.ToArray());

      } while (loop);

      return values;
    }

    public static List<KeyValuePair<string, string>>? EditKvps(string prompt, IEnumerable<KeyValuePair<string, string>> oldValues)
    {
      List<KeyValuePair<string, string>>? values = new List<KeyValuePair<string, string>>(oldValues);
      bool loop = true;
      do
      {
        CliTools.MarkupLine($"{prompt}");
        CliTools.MarkupLine($"{values.Count} items");
        var menuItems = new List<MenuItem>();

        for (var i = 0; i < values.Count; i++)
        {
          var value = values[i];
          var index = i;
          menuItems.Add($"[[ '[White]{value}[/]' ]]", () =>
          {
            var oldValue = values[index];
            if (CliTools.InputQuery("Modify Key", out var newKey, oldValue.Key) && 
                CliTools.InputQuery("Modify Value", out var newValue, oldValue.Value))
            {
              values[index] = new KeyValuePair<string, string>(newKey, newValue);
            }
          });
        }

        menuItems.AddSeparator("--- editor functions ---");
        menuItems.Add("+", "Add Item", () => { values.Add(new KeyValuePair<string, string>((values.Count+1).ToString(), string.Empty)); });
        menuItems.Add("-", "Remove Item", () => {
          if (CliTools.InputQuery("Enter number to delete", out var sNo))
          {
            if (int.TryParse(sNo, out var no))
            {
              no--; //1-based to 0-based
              if (no >= 0 && no < values.Count)
                values.RemoveAt(no);
              else
                CliTools.WarnMessage("Number out of range");
            }
          }
        });
        menuItems.Add("EX", "Use external default Editor to edit list", () =>
        {
          using (var tempFile = ToolBox.CreateTempFile())
          {
            File.WriteAllLines(tempFile.Value, values.Select(kvp=>$"{kvp.Key}|{kvp.Value}"));
            var process = new Process
            {
              StartInfo = new ProcessStartInfo
              {
                FileName = tempFile.Value,
                UseShellExecute = true,
                //Arguments = args,
                Verb = "edit",
              }
            };

            process.Start();
            Console.WriteLine("Edit file in editor that should popup, save file and close editor to continue.");
            Console.WriteLine("Wait for editor to exit...");
            process.WaitForExit();
            values = File.ReadAllLines(tempFile.Value)
              .Where(line => !string.IsNullOrWhiteSpace(line))
              .Select(line => line.Split('|'))
              .Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]))
              .ToList();
          }
        });
        menuItems.Add("OK", "Quit editing", () => { loop = false; });
        menuItems.Add("Esc", "Exit", () =>
        {
          values = null;
          loop = false;
        });

        CliTools.ShowMenu(null, menuItems.ToArray());

      } while (loop);

      return values;
    }
  }
}
