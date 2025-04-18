using LgpCore.AdmParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Runtime.InteropServices;
using FluentAssertions;
using LgpCore.Gpo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCoreTests
{
  public class PolicyTests : ServicedTestBase
  {
    private AdmFolder admFolder;

    [SetUp]
    public override void SetUp()
    {
      base.SetUp();
      admFolder = AdmFolder.SystemDefault();
      admFolder.Language = "de-DE"; // "en-US
      admFolder.ParseAsync().GetAwaiter().GetResult();
    }

    public static void ShowValues(Dictionary<PolicyElement, object?>? values)
    {
      if (values == null || !values.Any())
        return;
      string ValueToString(object? value)
      {
        if (value == null)
          return "<null>";
        if (value is ICollection collection)
          return $"[{collection.Count} items] " + string.Join(", ", collection.Cast<object?>()
            .Select(o => ValueToString(o)));
        if (value is KeyValuePair<string, string> pair)
          return $"'{pair.Key}':'{pair.Value}'";
        if (value is uint uintValue)
          return $"{uintValue}u";
        if (value is ulong ulongValue)
          return $"{ulongValue}ul";
        if (value is string sValue)
          return $"\"{sValue}\"";
        return $"'{value}'";
      }
      Console.WriteLine($"{values.Count} Values:");
      foreach (var tuple in values)
      {
        Console.WriteLine($"  \"{tuple.Key.Id}\", {ValueToString(tuple.Value)} ({tuple.Key.GetType().Name})");
      }
    }

    [TestCaseSource(typeof(PolicyTestCasesClass))]
    public void GetStateTest(PolicyTestCaseData testData) //Jobs based
    {
      var policy = admFolder.AllPolicies[testData.PolicyPrefixedName];
      Console.WriteLine($"Policy:{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      Console.WriteLine($"Class: {testData.PolicyClass}");

      var result = GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, testData.PolicyClass) { DryRun = true };
          context.Logger = this.ServiceProvider.GetService<ILogger<PolicyJob>>();

          var job = new PolicyJob(policy);
          var state = job.GetState(context);
          Console.WriteLine($"{state}");
          if (state == PolicyState.Enabled)
          {
            var values = job.GetValues(context);
            ShowValues(values);
          }


          //gpo.Save();
          Console.WriteLine();
          return state;

        }
      });
    }

    [Explicit]
    [TestCaseSource(typeof(PolicyTestCasesClass))]
    public void SetEnabledTest(PolicyTestCaseData testData) //Jobs based
    {
      var policy = admFolder.AllPolicies[testData.PolicyPrefixedName];
      Console.WriteLine($"Policy:{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      Console.WriteLine($"Class: {testData.PolicyClass}");

      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, testData.PolicyClass);
          context.Logger = this.ServiceProvider.GetService<ILogger<PolicyJob>>();

          var job = new PolicyJob(policy);
          job.Enable(context, testData.Values ?? throw new InvalidOperationException());

          context.Save();
          Console.WriteLine();
        }
      });
    }

    [Explicit]
    [TestCaseSource(typeof(PolicyTestCasesClass))]
    public void SetDisabledTest(PolicyTestCaseData testData) //Jobs based
    {
      var policy = admFolder.AllPolicies[testData.PolicyPrefixedName];
      Console.WriteLine($"Policy:{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      Console.WriteLine($"Class: {testData.PolicyClass}");

      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, testData.PolicyClass);
          context.Logger = this.ServiceProvider.GetService<ILogger<PolicyJob>>();

          var job = new PolicyJob(policy);
          job.Disable(context);

          context.Save();
          Console.WriteLine();
        }
      });
    }

    [Explicit]
    [TestCaseSource(typeof(PolicyTestCasesClass))]
    public void SetNotConfiguredTest(PolicyTestCaseData testData) //Jobs based
    {
      var policy = admFolder.AllPolicies[testData.PolicyPrefixedName];
      Console.WriteLine($"Policy:{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      Console.WriteLine($"Class: {testData.PolicyClass}");

      GpoHelper.RunInSta(() =>
      {
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, testData.PolicyClass);
          context.Logger = this.ServiceProvider.GetService<ILogger<PolicyJob>>();

          var job = new PolicyJob(policy);
          Console.WriteLine($"State before: {job.GetState(context)}");
          job.NotConfigured(context);

          context.Save();
        }
        using (GpoHelper.InitGpo(out var gpo))
        {
          using var context = new GpoContext(gpo, testData.PolicyClass);
          context.Logger = this.ServiceProvider.GetService<ILogger<PolicyJob>>();

          var job = new PolicyJob(policy);
          Console.WriteLine($"State after: {job.GetState(context)}");
        }
      });
    }

    [Explicit]
    [TestCaseSource(typeof(PolicyTestCasesClass))]
    public void AllSatesRoundTripTest(PolicyTestCaseData testData) //Jobs based
    {
      var policy = admFolder.AllPolicies[testData.PolicyPrefixedName];
      Console.WriteLine($"Policy:{policy.PrefixedName()} Cat:{policy.CategoryPath()} - {policy.DisplayNameResolved()}");
      Console.WriteLine($"Class: {testData.PolicyClass}");

      var state = policy.GetState(testData.PolicyClass);
      Console.WriteLine($"Initial State: {state}");

      switch (state)
      {
        case PolicyState.Unknown:
        case PolicyState.NotConfigured:
        case PolicyState.Suspect:
        {
          policy.Enable(testData.PolicyClass,
            testData.Values ?? throw new InvalidOperationException("No Values provided"),
            null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Enable: {state}");
          state.Should().Be(PolicyState.Enabled);

          var values = policy.GetValues(testData.PolicyClass);
          ShowValues(values);
          Console.WriteLine();

          policy.Disable(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Disable: {state}");
          state.Should().Be(PolicyState.Disabled);

          policy.NotConfigure(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after NotConfigured: {state}");
          state.Should().Be(PolicyState.NotConfigured);
          break;
        }
        case PolicyState.Enabled:
        {
          var initialValues = policy.GetValues(testData.PolicyClass);
          ShowValues(initialValues);
          Console.WriteLine();

          policy.Disable(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Disable: {state}");
          state.Should().Be(PolicyState.Disabled);

          policy.NotConfigure(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after NotConfigured: {state}");
          state.Should().Be(PolicyState.NotConfigured);

          var initialValuesWithoutNulls = initialValues?
            .ToDictionary(e => e.Key, e => e.Value ?? throw new InvalidOperationException());
          policy.Enable(testData.PolicyClass, initialValuesWithoutNulls ?? throw new InvalidOperationException("No Values provided"), null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Enable: {state}");

          state.Should().Be(PolicyState.Enabled);
          var values = policy.GetValues(testData.PolicyClass);
          ShowValues(values);

          break;
        }
        case PolicyState.Disabled:
        {
          policy.Enable(testData.PolicyClass,
            testData.Values ?? throw new InvalidOperationException("No Values provided"), null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Enable: {state}");
          state.Should().Be(PolicyState.Enabled);

          var values = policy.GetValues(testData.PolicyClass);
          ShowValues(values);
          Console.WriteLine();

          policy.NotConfigure(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after NotConfigured: {state}");
          state.Should().Be(PolicyState.NotConfigured);

          policy.Disable(testData.PolicyClass, null);
          state = policy.GetState(testData.PolicyClass);
          Console.WriteLine($"State after Disable: {state}");
          state.Should().Be(PolicyState.Disabled);
          break;
        }
        default:
          throw new ArgumentOutOfRangeException();
      }

    }

    public class PolicyTestCasesClass : IEnumerable
    {
      public IEnumerator GetEnumerator()
      {
        var index = 1;
        var testCaseDatas = GetTestCases();
        foreach (var testCaseData in testCaseDatas)
        {
          yield return new TestCaseData(testCaseData)
            .SetName($"{index++:00} {testCaseData.TestName ?? testCaseData.PolicyPrefixedName}");
        }
      }

      private IEnumerable<PolicyTestCaseData> GetTestCases()
      {
        //complex sample, EnumElement, Decimal Value
        yield return new PolicyTestCaseData("windows.EnableSmartScreen")
        {
          Values = new Dictionary<string, object>()
          {
            {"EnableSmartScreenDropdown", "SmartScreen_Warn"},
          }
        };
        ;

        //complex sample, EnumElements, DecimalElement
        yield return new PolicyTestCaseData("bits.BITS_SetTransferPolicyOnCostedNetwork")
        {
          Values = new Dictionary<string, object>()
          {
            {"BITS_TransferPolicyForegroundPriorityValue", "BITS_TransferPolicyAlwaysTransfer"},
            {"BITS_TransferPolicyForegroundPriorityValueCustom", 254u},
            {"BITS_TransferPolicyHighPriorityValue", "BITS_TransferPolicyAlwaysTransfer"},
            {"BITS_TransferPolicyHighPriorityValueCustom", 254u},
            {"BITS_TransferPolicyNormalPriorityValue", "BITS_TransferPolicyAlwaysTransfer"},
            {"BITS_TransferPolicyNormalPriorityValueCustom", 254u},
            {"BITS_TransferPolicyLowPriorityValue", "BITS_TransferPolicyAlwaysTransfer"},
            {"BITS_TransferPolicyLowPriorityValueCustom", 254u},
          }
        };

        //DecimalElement (storeAsText)
        yield return new PolicyTestCaseData("controlpaneldisplay.CPL_Personalization_ScreenSaverTimeOut")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"ScreenSaverTimeOutFreqSpin", 1020u},
          }
        };

        //sample for list element (explicitValue)
        yield return new PolicyTestCaseData("MicrosoftEdge.Favorites")
        {
          Values = new Dictionary<string, object>()
          {
            {"ProvisionedFavorites_List", new List<KeyValuePair<string, string>> { new ("web de", "www.web.de")}},
          }
        };

        //sample for list element (just values)
        yield return new PolicyTestCaseData("fullarmor.DefaultIndexedPaths_1")
        {
          Values = new Dictionary<string, object>()
          {
            {"DefaultIndexedPaths", new List<string> {"D:\\"}},
          }
        };

        //sample for simple EnumElement
        yield return new PolicyTestCaseData("windows.ExplorerRibbonStartsMinimized")
        {
          Values = new Dictionary<string, object>()
          {
            {"ExplorerRibbonStartsMinimizedDropdown", "ExplorerRibbonStartsMinimized_StartsMinimized"},
          }
        };

        //sample for BooleanElement with Decimal Values
        yield return new PolicyTestCaseData("inetres.Survey")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"Survey", true}, //DWord 0!
          }
        };

        //sample for BooleanElement with no True/False Values and one with String Values 
        yield return new PolicyTestCaseData("inetres.MediaSettings")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"BlockMedia", true}, //no Values -> DWord 1
            {"PlayByDefault", true}, //string Values
          }
        };

        //sample for BooleanElement with just no Values to test the special behavior (false -> not '0' but delete-value) 
        yield return new PolicyTestCaseData("devinst.DriverSearchPlaces")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"DriverSearchPlaces_Floppies", true}, //no Values
            {"DriverSearchPlaces_CD", false}, //no Values
            {"DriverSearchPlaces_WindowsUpdate", true}, //no Values
          }
        };

        //sample for BooleanElement with one item has True and False values (Decimal) + standard enabel/disable value
        yield return new PolicyTestCaseData("cloudcontent.ConfigureWindowsSpotlight")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"ConfigureWindowsSpotlight_Checkbox", true}, //Decimal value
          }
        };

        //
        //sample for TextElement
        yield return new PolicyTestCaseData("inetres.BackgroundColorPol")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"BackgroundColor", "192,192,192"},
          }
        };

        //sample for MultiTextElement
        yield return new PolicyTestCaseData("inetres.SiteDiscoveryDomainAllowList")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            {"SiteDiscoveryDomainAllowList", new string[] {"Test.com", "Test2.com"}},
          }
        };

        //sample for no value and no elements
        yield return new PolicyTestCaseData("windows.NoCDBurning")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          { }
        };

        //sample for only enable values (with enum element)
        yield return new PolicyTestCaseData("inetres.FontSize")
        {
          PolicyClass = PolicyClass.User,
          Values = new Dictionary<string, object>()
          {
            { "FontSizeDefault", "Font2"}
          }
        };

      }
    }

    public class PolicyTestCaseData
    {
      public PolicyTestCaseData(string policyPrefixedName)
      {
        PolicyPrefixedName = policyPrefixedName;
      }

      public string PolicyPrefixedName { get; set; }
      public string? TestName { get; set; } 
      public Dictionary<string, object>? Values { get; set; }
      public PolicyClass PolicyClass { get; set; } = PolicyClass.Machine;
    }
  }
}
