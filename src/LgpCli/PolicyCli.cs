using System.Collections;
using Cli;
using LgpCore;
using LgpCore.AdmParser;
using LgpCore.Gpo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LgpCli
{
  public static class PolicyCli
  {
    public static void ShowPage(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass)
    {
      if (policyClass == PolicyClass.Both)
      {
        if (policy.Class == PolicyClass.Both)
        {
          //make sure to have a dedicated class (not both)
          if (!MainCli.SelectPolicyClass(false, out policyClass))
            return;
        }
        else
          policyClass = policy.Class;
      }

      var config = serviceProvider.GetRequiredService<IConfigurationRoot>();
      var appSection = config.AppSection();

      HandleRecentUsed(appSection, policy, policyClass);

      if (!bool.TryParse(appSection["showExplanations"], out var showExplanations))
        showExplanations = true;
      ElementValues? elementValues = null;
      bool loop = true;
      var logger = serviceProvider.GetRequiredService<ILogger>();
      AdmFolder? admFolder = null;
      do
      {
        CliErrorHandling.HandleShowExceptions(() =>
        {
          Console.Clear();
          admFolder ??= serviceProvider.GetRequiredService<AdmFolder>();

          CliTools.MarkupLine($"[Policy]Policy[/]      : [Title]{policy.DisplayNameResolved()}[/]");
          CliTools.MarkupLine($"[Category]Category[/]    : {policy.CategoryPath()}");
          CliTools.MarkupLine($"PrefixedName: [PrefixedName]{policy.PrefixedName()}[/]");
          CliTools.MarkupLine($"Class       : [Class]{policyClass}[/]");
          CliTools.MarkupLine($"SupportedOn : [SupportedOn]{policy.SupportedOn()?.DisplayNameResolved()}[/]");
          if (showExplanations)
            CliTools.MarkupLine($"ExplainText : [ExplainText]{policy.ExplainTextResolved()}[/]");
          Console.WriteLine("---------------------------------------------------------------------------");
          PolicyState state;
          try
          {
            var poilicyJobLogger = serviceProvider.GetService<ILogger<PolicyJob>>();
            state = policy.GetState(policyClass, poilicyJobLogger);

            CliTools.MarkupLine($"State:{StateMarkupString(state)}");

            //use the values from system
            if (elementValues == null || elementValues.ValuesSource is  ElementValues.Source.Defaults or ElementValues.Source.CurrentOnSystem)
            {
              if (state == PolicyState.Enabled)
              {
                var policyValues = policy.GetValues(policyClass);
                if (policyValues != null)
                  elementValues = new ElementValues(policy, policyClass, policyValues, ElementValues.Source.CurrentOnSystem);
                else
                  elementValues = policy.DefaultElementValues(policyClass);
              }
              else
                elementValues = policy.DefaultElementValues(policyClass);

              //try to get values from current BatchFile
              if (elementValues.ValuesSource == ElementValues.Source.Defaults)
                GetValuesFromBatchFile(serviceProvider, elementValues, true);
            }

          }
          catch (Exception e)
          {
            //Console.WriteLine(e.ToString());
            CliTools.ErrorMessage($"not able to get state: {e.Message}", false);
            if (e is UnauthorizedAccessException || e.InnerException is UnauthorizedAccessException)
            {
              CliTools.WarnMessage("Access denied, try to run as administrator", false);
            }
              
            state = PolicyState.Unknown;
          }

          ShowElementValues(elementValues);
          var menuItems = new List<MenuItem>();
          menuItems.Add("E", "[Enable]Enable[/] Policy", () =>
          {
            EnablePolicy(serviceProvider, policy, policyClass, elementValues!.CompletedValues);
            elementValues = null;
          }, () => state != PolicyState.Unknown && elementValues != null && elementValues.AllValuesSet);
          menuItems.Add("D", "[Disable]Disable[/] Policy", () => DisablePolicy(serviceProvider, policy, policyClass), () => state != PolicyState.Unknown);
          menuItems.Add("N", "[NotConfigure]NotConfigure[/] Policy", () => NotConfigurePolicy(serviceProvider, policy, policyClass), () => state != PolicyState.Unknown);
          menuItems.Add("M", "Modify Values", () => EditValuesCli.ShowPage(serviceProvider, elementValues!), () => elementValues?.Values.Any());
          if (policy.Class == PolicyClass.Both)
            menuItems.Add("C", $"Switch PolicyClass to [Class]{(policyClass == PolicyClass.Machine ? PolicyClass.User : PolicyClass.Machine)}[/]", () => policyClass = policyClass == PolicyClass.Machine ? PolicyClass.User : PolicyClass.Machine, () => true);
          menuItems.AddCheckBox("EXP", "Show Explanations", showExplanations, b =>
          {
            showExplanations = b;
            appSection["showExplanations"] = b.ToString();
            config.SaveJsonProvider();
          });
          menuItems.Add("GC", "Get current values from system", () => { GetCurrentValuesFromSystem(elementValues); });
          menuItems.Add("GD", "Get default values", () => elementValues?.SetDefaults()); 
          menuItems.Add("GB", "Get Values from current batch file", () => GetValuesFromBatchFile(serviceProvider, elementValues!, false), () => true);
          menuItems.Add("B", "Build Commandline for this policy", () => BuildCommandLineText(serviceProvider, policy, policyClass, elementValues), () => state != PolicyState.Unknown);
          menuItems.Add("RS", "Report all GPO settings in registry", () => MainCli.ReportGpoSettingsRegistry(policyClass), () => true);
          menuItems.Add("RR", "Report registry settings for this policy", () => ReportRegistrySettings(serviceProvider, policy, policyClass), () => true);
          menuItems.Add("R", "Refresh", () => {}, () => true);
          
          menuItems.Add("Esc", "Exit", () => { loop = false; });

          CliTools.ShowMenu(null, menuItems.ToArray());
        }, logger);
      } while (loop);
    }

    public static void ShowElementValues(ElementValues? elementValues)
    {
      if (elementValues == null)
      {
        CliTools.MarkupLine("Data Elements: [White]<null>[/]");
        return;
      }
      if (elementValues.Values.Any())
        CliTools.Markup($"Data Elements - [White]{elementValues.Values.Count} values[/]");
      else
        CliTools.Markup("Data Elements: [White]no values[/]");
      CliTools.MarkupLine($" (source: [White]{elementValues.ValuesSource}[/]):");

      if (elementValues.Values.Any())
      {
        var maxTypeNameLen = elementValues.Values.Keys.Max(e => e.TypeNameSlim().Length);
        var maxElementIdLen = elementValues.Values.Keys.Max(e => e.Id.Length);
        foreach (var (key, value) in elementValues.Values)
        {
          CliTools.Markup($"  [ElemType]{key.TypeNameSlim().PadRight(maxTypeNameLen)}[/] [ElemId]{key.Id.PadRight(maxElementIdLen)}[/]: ");
          if (value == null)
          {
            CliTools.MarkupLine("<null>");
            continue;
          }

          if (key is EnumElement enumElement)
          {
            var enumItem = enumElement.GetItem(value.ToString()!, false);
            if (enumItem != null)
              CliTools.MarkupLine($"[White]{value}[/] [DarkGray]'{enumItem.DisplayNameResolved()}'[/]");
            else
              CliTools.MarkupLine($"[White]{value}[/] [Red]invalid value[/]");
            continue;
          }

          if (value is ICollection coll) //List<string> or List<KeyValuePair<string, string>> or string[]
          {
            Console.WriteLine($"{coll.Count} items");
            foreach (var item in coll)
            {
              switch (item)
              {
                case KeyValuePair<string, string> pair:
                  Console.WriteLine($"    '{pair.Key}':'{pair.Value}'");
                  break;
                case string:
                  Console.WriteLine($"    \"{item}\"");
                  break;
                default:
                  CliTools.MarkupLine($"    {item} [Red](unexpected type {item?.GetType()})[/]");
                  break;
              }
            }

            continue;
          }

          if (value is uint uintValue)
          {
            CliTools.MarkupLine($"[White]{uintValue}[/] [DarkGray](uint)[/]");
            continue;
          }

          if (value is ulong ulongValue)
          {
            CliTools.MarkupLine($"[White]{ulongValue}[/] [DarkGray](ulong)[/]");
            continue;
          }

          if (value is string sValue)
          {
            CliTools.MarkupLine($"\"[White]{sValue}[/]\"");
            continue;
          }

          if (value is bool boolValue)
          {
            CliTools.MarkupLine($"[White]{boolValue}[/]");
            continue;
          }

          CliTools.MarkupLine($"{value} [Red](unexpected type {value?.GetType()})[/]");
        }
      }
    }

    public static string StateMarkupString(PolicyState state)
    {
      return state switch
      {
        PolicyState.Unknown => $"[Red]{state}[/]",
        PolicyState.NotConfigured => $"[NotConfigure]{state}[/]",
        PolicyState.Enabled => $"[Enable]{state}[/]",
        PolicyState.Disabled => $"[Disable]{state}[/]",
        PolicyState.Suspect => $"[Red]{state}[/]",
        _ => throw new ArgumentOutOfRangeException()
      };
    }
    private static void HandleRecentUsed(IConfigurationSection appSection, Policy policy, PolicyClass policyClass)
    {
      var prefixedNameWithClass = $"{policy.PrefixedName()}|{policyClass}";
      var lastUsedSection = appSection.GetSection("recentUsed");
      var values = lastUsedSection.Items()
        .OrderBy(e => e.Key)
        .Select(e => e.Value)
        .ToList();
      
      var idx = values.FindIndex(e => string.Equals(e, prefixedNameWithClass, StringComparison.OrdinalIgnoreCase));
      if (idx >= 0)
      {
        values.RemoveAt(idx);
      }
      values.Insert(0, prefixedNameWithClass);
      foreach (var (index, item) in values.Index().Take(20)) //limit to 20 items
      {
        lastUsedSection[$"{index:000}"] = item;
      }

      appSection.SaveJsonProvider();
    }

    private static void EnablePolicy(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass, Dictionary<PolicyElement, object> values)
    {
      var logger = serviceProvider.GetKeyedService<ILogger>(null);
      policy.Enable(policyClass, values, logger);
    }

    private static void DisablePolicy(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass)
    {
      var logger = serviceProvider.GetKeyedService<ILogger>(null);
      policy.Disable(policyClass, logger);
    }

    private static void NotConfigurePolicy(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass)
    {
      var logger = serviceProvider.GetKeyedService<ILogger>(null);
      policy.NotConfigure(policyClass, logger);
    }

    public static void GetCurrentValuesFromSystem(ElementValues? elementValues)
    {
      if (elementValues != null && !elementValues.GetCurrent())
        CliTools.WarnMessage("Not able to get current Values, probably policy is not enabled.");
    }

    public static void GetValuesFromBatchFile(IServiceProvider serviceProvider, ElementValues elementValues, bool quiet = false)
    {
      var batchCmd = serviceProvider.GetRequiredService<BatchCmd>();
      if (!(batchCmd.CurrentFile?.Exists ?? false))
      {
        if (!quiet)
          CliTools.WarnMessage("No batch file selected or file does not exist.");
        return;
      }

      var info = batchCmd.GetCommandInfo(CommandLine.CommandNameEnable, elementValues.Policy.PrefixedName(), elementValues.PolicyClass);
      if (info == null)
      {
        if (!quiet)
          CliTools.WarnMessage("No batch command found for this policy.");
        return;
      }
      
      if (info.ElementValues == null)
      {
        if (!quiet)
          CliTools.WarnMessage("No element values found in batch command.");
        return;
      }

      elementValues.FromBatchFile(info.ElementValues);
    }

    private static void BuildCommandLineText(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass, ElementValues? elementValues)
    {
      void HandleCommand(PolicyCommandType policyCommandType)
      {
        var cmd = policy.BuildCommand(policyClass, policyCommandType, elementValues);
        Console.WriteLine(cmd);
        
        var batchCmd = serviceProvider.GetRequiredService<BatchCmd>();
        if (batchCmd.CanWrite.GetValueOrDefault() && CliTools.BooleanQuestion($"Do you want to add this command to command file: {batchCmd.CurrentFile?.Name}", out var addCommand) && addCommand)
        {
          if (batchCmd.AddCommand(cmd, out var status))
          {
            CliTools.SuccessMessage("Successfully Added.");
          }
          else
          {
            CliTools.ErrorMessage($"Error: {status}");
          }
        }
        else
        {
          CliTools.EnterToContinue();
        }
        
      }

      bool loop = false;
      do
      {
        loop = false; //exit the loop by default
        var menuItems = new List<MenuItem>();
        menuItems.Add("E", "[Enable]Enable[/] Policy", () => HandleCommand(PolicyCommandType.Enable), () => elementValues != null && elementValues.AllValuesSet);
        menuItems.Add("D", "[Disable]Disable[/] Policy", () => HandleCommand(PolicyCommandType.Disable));
        menuItems.Add("N", "[NotConfigure]NotConfigure[/] Policy", () => HandleCommand(PolicyCommandType.NotConfigure));
        menuItems.Add("G", "[GetState]Get State of Policy[/]", () => HandleCommand(PolicyCommandType.GetState));
        
        menuItems.Add("Esc", "Cancel", () => { });

        CliTools.ShowMenu(null, menuItems.ToArray());

      } while (loop);
    }

    private static void ReportRegistrySettings(IServiceProvider serviceProvider, Policy policy, PolicyClass policyClass)
    {
      var policyStates = new List<PolicyState>
      {
        PolicyState.Enabled,
        PolicyState.Disabled,
        PolicyState.NotConfigured,
      };

      if (CliTools.SelectItem(policyStates, "Select a state to be reported", PolicyState.Enabled, out var policyState, state => state.ToString()))
      {
        var groups = policy.ReportRegistrySettings(policyClass, policyState)
          .GroupBy(e => e.element, e => e.action);

        using var rootReg = policyClass switch
        {
          PolicyClass.Machine => Registry.LocalMachine,
          PolicyClass.User => Registry.CurrentUser,
          _ => throw new ArgumentOutOfRangeException(nameof(policyClass), policyClass, "Invalid PolicyClass for registry reporting")
        };

        CliTools.WarnMessage("This feature is in BETA. Don't write registry values directly to set Policies! Use 'Enable' to set a policy!", false);
        Console.WriteLine();

        foreach (var group in groups)
        {
          Console.WriteLine($"{(group.Key == null ? "<simple items>" : $"{group.Key!.GetType().Name}:'{group.Key!.Id}'")}");
          var items = group;
          foreach (var item in items)
          {
            //items per element
            var sAction = item.PolicyValueDeleteType switch
            {
              PolicyValueDeleteType.None => "SetValue",
              PolicyValueDeleteType.DeleteValue => "DeleteValue",
              PolicyValueDeleteType.DeleteValues => "DeleteKey",
              _ => throw new ArgumentOutOfRangeException()
            };
            var sValue = item.PolicyValueDeleteType switch
            {
              PolicyValueDeleteType.None => item.Value != null
                ? $"{item.Value?.ToString()}"
                : "<null>",
              _ => "-"
            };

            //current Value
            using var regKey = rootReg.OpenSubKey(item.RegKey, false);
            var regValue = regKey?.GetValue(item.RawRegValueName, null);
            var regValueKind = regValue != null && regKey != null
              ? regKey.GetValueKind(item.RawRegValueName)
              : RegistryValueKind.Unknown;
            var sCurrentRegValue = regValue != null 
              ? $"[Value]'{regValue}'[/] ({regValueKind})"
              : "<null>";


            //Console.WriteLine($"  {item.Action}: {item.RegKey}|{item.RegValueName} '{item.Value ?? "<null>"}' ({item.ValueKind})");
            CliTools.MarkupLine($"  {sAction,-11}: {item.RegKey}|{item.RawRegValueName} '{sValue}' ({item.RawValueKind}) Current:{sCurrentRegValue}");
          }
        }
        CliTools.EnterToContinue();
      }
    }
  }
}
