using LgpCore.AdmParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LgpCore.Gpo
{
  public static class PolicyExtensions
  {
    public static PolicyState GetState(this Policy policy, PolicyClass policyClass, ILogger? logger = null)
    {
      return GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, policyClass);
          context.Logger = logger;
          var job = new PolicyJob(policy);
          return job.GetState(context);
        }
      });
    }

    public static List<(Policy policy, PolicyClass policyClass, PolicyState state)> GetStates(
      this IEnumerable<Policy> policies)
    {
      return GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var contextMachine = new GpoContext(gpo, PolicyClass.Machine);
          using var contextUser = new GpoContext(gpo, PolicyClass.User);

          var result = new List<(Policy policy, PolicyClass policyClass, PolicyState state)>();
          foreach (var policy in policies)
          {
            var job = new PolicyJob(policy);
            if (policy.IsInClass(PolicyClass.Machine))
              result.Add((policy, PolicyClass.Machine, job.GetState(contextMachine)));
            if (policy.IsInClass(PolicyClass.User))
              result.Add((policy, PolicyClass.User, job.GetState(contextUser)));
          }

          return result;
        }
      });
    }

    public static void Enable(this Policy policy, PolicyClass policyClass, Dictionary<string, object> values,
      ILogger? logger) =>
      policy.Enable(policyClass, policy.ToElementValues(values), logger);

    public static void Enable(this Policy policy, PolicyClass policyClass, Dictionary<PolicyElement, object> values,
      ILogger? logger)
    {
      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, policyClass);
          context.Logger = logger;

          var job = new PolicyJob(policy);
          job.Enable(context, values ?? throw new InvalidOperationException());

          context.Save();
        }
      });
    }

    public static void Disable(this Policy policy, PolicyClass policyClass, ILogger? logger)
    {
      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, policyClass);
          context.Logger = logger;

          var job = new PolicyJob(policy);
          job.Disable(context);

          context.Save();
        }
      });
    }

    public static void NotConfigure(this Policy policy, PolicyClass policyClass, ILogger? logger)
    {
      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, policyClass);
          context.Logger = logger;

          var job = new PolicyJob(policy);
          job.NotConfigured(context);

          context.Save();
        }
      });
    }

    public static Dictionary<PolicyElement, object?>? GetValues(this Policy policy, PolicyClass policyClass)
    {
      return GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, policyClass);

          var job = new PolicyJob(policy);
          return job.GetValues(context);
        }
      });
    }

    public static string BuildCommand(this Policy policy, PolicyClass policyClass, PolicyCommandType policyCommandType,
      ElementValues? elementValues)
    {
      var command = policyCommandType switch
      {
        PolicyCommandType.Enable => CommandLine.CommandNameEnable,
        PolicyCommandType.Disable => CommandLine.CommandNameDisable,
        PolicyCommandType.NotConfigure => CommandLine.CommandNameNotConfigure,
        PolicyCommandType.GetState => CommandLine.CommandNameGetState,
        _ => throw new ArgumentOutOfRangeException(nameof(policyCommandType), policyCommandType, null)
      };
      var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
      var result = $"{exeName} {command} {policy.PrefixedName()} {policyClass}";
      if (policyCommandType == PolicyCommandType.Enable)
      {
        if (elementValues == null)
          throw new ArgumentNullException(nameof(elementValues),
            "Element values must be provided for enabling a policy.");

        result += elementValues!.CompletedValues.ValuesToCommandLine();
      }

      return result;
    }

    public static List<(PolicyElement? element, PolicyValueItemAction action)> ReportRegistrySettings(
      this Policy policy, PolicyClass policyClass, PolicyState policyState)
    {
      var job = new PolicyJob(policy);
      return job.ReportRegistrySettings(policyState);
    }

    public static List<(PolicyElement? element, List<(string regKey, string rawRegValueName, string sAction,
        RegistryValueKind RawValueKind, string sValue, string sCurrentRegValue)> items)>
      ReportRegistrySettingsEx(this Policy policy, PolicyClass policyClass, PolicyState policyState)
    {
      (string regKey, string rawRegValueName, string sAction, RegistryValueKind rawValueKind, string sValue)
        GetActionInfo(PolicyValueItemAction action)
      {
        var regKey = action.RegKey;
        var rawRegValueName = action.RawRegValueName;
        var sAction = action.PolicyValueDeleteType switch
        {
          PolicyValueDeleteType.None => "SetValue",
          PolicyValueDeleteType.DeleteValue => "DeleteValue",
          PolicyValueDeleteType.DeleteValues => "DeleteKey",
          _ => throw new ArgumentOutOfRangeException()
        };
        var sValue = action.PolicyValueDeleteType switch
        {
          PolicyValueDeleteType.None => action.Value != null
            ? $"{action.Value?.ToString()}"
            : "<null>",
          _ => "-"
        };
        return (regKey, rawRegValueName, sAction, action.RawValueKind, sValue);
      }

      using var rootReg = policyClass switch
      {
        PolicyClass.Machine => Registry.LocalMachine,
        PolicyClass.User => Registry.CurrentUser,
        _ => throw new ArgumentOutOfRangeException(nameof(policyClass), policyClass,
          "Invalid PolicyClass for registry reporting")
      };

      string GetCurrentValueText(string regKey, string regValueName)
      {
        //current Value
        using var key = rootReg.OpenSubKey(regKey, false);
        var regValue = key?.GetValue(regValueName, null);
        var regValueKind = regValue != null && key != null
          ? key.GetValueKind(regValueName)
          : RegistryValueKind.Unknown;
        var sCurrentRegValue = regValue != null
          ? $"[Value]'{regValue}'[/] ({regValueKind})"
          : "<null>";
        return sCurrentRegValue;
      }

      var job = new PolicyJob(policy);
      var elementItems = job.ReportRegistrySettings(policyState)
        .GroupBy(e => e.element, e => e.action)
        .Select(g =>
        {
          var element = g.Key;
          var actions = g;

          Console.WriteLine($"{(element == null ? "<simple items>" : $"{element!.GetType().Name}:'{element!.Id}'")}");


          //Group same actions (pointing to same RegValue) and sum-up possible values
          var groupedActions = actions
            .Select(GetActionInfo)
            .GroupBy(e => (e.regKey, e.rawRegValueName, e.sAction, e.rawValueKind))
            .Select(e => 
            (
              e.Key.regKey,
              e.Key.rawRegValueName,
              e.Key.sAction,
              e.Key.rawValueKind,
              sValue: string.Join("|", e.Select(a => a.sValue)),
              sCurrentRegValue: GetCurrentValueText(e.Key.regKey, e.Key.rawRegValueName)
            ))
            .ToList();
          return (element: element, items: groupedActions);
        })
        .ToList();
      return elementItems;
    }
  }
}

