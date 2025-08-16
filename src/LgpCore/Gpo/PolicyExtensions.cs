using LgpCore.AdmParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public static List<(Policy policy, PolicyClass policyClass, PolicyState state)> GetStates(this IEnumerable<Policy> policies)
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

    public static void Enable(this Policy policy, PolicyClass policyClass, Dictionary<string, object> values, ILogger? logger) =>
      policy.Enable(policyClass, policy.ToElementValues(values), logger);

    public static void Enable(this Policy policy, PolicyClass policyClass, Dictionary<PolicyElement, object> values, ILogger? logger)
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

    public static List<(PolicyElement? element, PolicyValueItemAction action)> ReportRegistrySettings(this Policy policy, PolicyClass policyClass, PolicyState policyState)
    {
      var job = new PolicyJob(policy);
      return job.ReportRegistrySettings(policyState);
    }
  }
}
