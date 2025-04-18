using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Cli;
using Infrastructure;
using LgpCore.AdmParser;
using LgpCore.Gpo;
using LgpCore.Infrastructure;

namespace LgpCli
{
  public class MainCli
  {
    static public void ShowPage(IServiceProvider serviceProvider, Action? initialCommand)
    {
      ShowMainPage(serviceProvider, initialCommand);
    }

    static void ShowMainPage(IServiceProvider serviceProvider, Action? initialCommand)
    {
      bool loop = true;
      var logger = serviceProvider.GetRequiredService<ILogger>();

      var appInfo = serviceProvider.GetRequiredService<AppInfo>();
      Console.Title = appInfo.Title;

      var appSection = serviceProvider.GetRequiredService<IConfigurationRoot>().AppSection();
      var lastUsedSection = appSection.GetSection("recentUsed");
      AdmFolder? admFolder = null;
      
      do
      {
        CliErrorHandling.HandleShowExceptions(() =>
        {
          if (initialCommand != null)
          {
            initialCommand();
            initialCommand = null;
          }
          Console.Clear();
          admFolder ??= serviceProvider.GetRequiredService<AdmFolder>();
          CliTools.MarkupLine($"[Title]{appInfo.Title}[/] [DarkGreen]{appInfo.Ver}[/] ({appInfo.Copyright}) - {appInfo.Desc} [[{(appInfo.IsAdmin ? "[Green]admin[/]" : "[Red]not admin[/]")}]]");
          Console.WriteLine("---------------------------------------------------------------------------");

          CliTools.MarkupLine($"{admFolder.AllCategories.Count} [Category]Categories[/], {admFolder.AllPolicies.Count} [Policy]Policies[/], {AdmExtensions.LanguageDisplayName(admFolder.Language)}");
          var menuItems = new List<MenuItem>();
          //menuItems.Add(new MenuSeparator("-- Default Workflow --"));
          menuItems.Add("P", "Select Policy", () => SelectShowPolicy(serviceProvider), () => true);
          menuItems.Add("S", "Search Policy", () => SearchCli.ShowPage(serviceProvider), () => true);
          menuItems.Add("L", "Last Used Policies", () => LastUsedPolicies(serviceProvider, admFolder, lastUsedSection), () => lastUsedSection.Items().Any());
          menuItems.Add("T", "Show Policy Category Tree", () => ShowPolicyCategoryTree(serviceProvider), () => true);

          menuItems.Add("A", "Report state for All policies", () => ReportStates(serviceProvider, admFolder), () => true);
          menuItems.Add("RS", "Report all settings in registry", () => ReportSettingsRegistry(), () => true);

          menuItems.Add("M", "Manage Command File", () => ManageCommandFile(serviceProvider), () => true);
          menuItems.Add("CL", "Change Language", () => ChangeLanguge(serviceProvider), () => true);
          menuItems.Add(new MenuItem("DebugDI", "DebugDI", () => DebugDi(serviceProvider), () => true) {HiddenButActive = true});

          menuItems.Add("Esc", "Exit", () => { loop = false; });

          CliTools.ShowMenu(null, menuItems.ToArray());
        }, logger);
      } while (loop);
    }

    public static void SelectShowPolicy(IServiceProvider serviceProvider)
    {
      if (!SelectPolicyClass(true, out var policyClass))
        return;
      if (!SelectPolicy(serviceProvider.GetRequiredService<AdmFolder>().RootCategory, out var policy, policyClass))
        return;

      PolicyCli.ShowPage(serviceProvider, policy, policyClass);
    }

    private static void LastUsedPolicies(IServiceProvider serviceProvider, AdmFolder admFolder,
      IConfigurationSection lastUsedSection)
    {
      var items = lastUsedSection.Items()
        .OrderBy(e => e.Key)
        .Select(x => x.Value)
        .WhereNotDefault()
        .Select(e =>
        {
          var idx = e.IndexOf('|');
          if (idx > 0)
          {
            string prefixedName = e[0..idx];
            string sPolicyClass = e[(idx + 1)..];
            var policyClass = Enum.Parse<PolicyClass>(sPolicyClass);
            return (policy: admFolder.AllPolicies[prefixedName], policyClass: policyClass);
          }
          return (policy: (Policy?)null, policyClass: PolicyClass.Both);
        })
        .Where(e => e.policy != null)
        .Select(tuple => (policy:tuple.policy!, policyClass:tuple.policyClass))
        .ToList();
      if (items.Any())
      {
        
        if(CliTools.SelectItem(items, "Select a policy", items.FirstOrDefault(), out var item, e => $"{e.policy.DisplayNameResolved()} [PrefixedName]({e.policy.PrefixedName()})[/] [Class]{e.policyClass}[/]"))
        {
          PolicyCli.ShowPage(serviceProvider, item.policy, item.policyClass);
        }
      }
      else
      {
        CliTools.WarnMessage("No policies found in LastUsed section.");
      }
    }

    private static void ShowPolicyCategoryTree(IServiceProvider serviceProvider)
    {
      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();

      var sTree = TreeVisualizer.Visualize(
        admFolder.RootCategory,
        lc => lc.Items,
        lc => $"{lc.DisplayNameResolved()} {lc.Policies.Count}", //[{lc.CategoryIdent.NamespacePrefix.Prefix}|{lc.Name}]
        out var leafs);

      Console.WriteLine(sTree);
      CliTools.EnterToContinue();
    }

    private static void ReportStates(IServiceProvider serviceProvider, AdmFolder admFolder)
    {
      var states = admFolder.AllPolicies.Values.GetStates();
      var countStates = states.CountBy(e => e.state).ToDictionary();
      CliTools.MarkupLine($"{admFolder.AllPolicies.Count} [Policy]Policies[/] - {states.Count} States: [Enable]Enabled[/]:{countStates.GetValueOrDefault(PolicyState.Enabled, 0)} [Disable]Disabled[/]:{countStates.GetValueOrDefault(PolicyState.Disabled, 0)} [NotConfigure]NotConfigured[/]:{countStates.GetValueOrDefault(PolicyState.NotConfigured, 0)}");

      var menuItems = new List<MenuItem>();
      foreach ( var item in states.Where(e => e.state != PolicyState.NotConfigured))
      {
        menuItems.Add($"{PolicyCli.StateMarkupString(item.state)} for [PrefixedName]{item.policy.PrefixedName()}[/] ([Class]{item.policyClass}[/]) [Title]{item.policy.DisplayNameResolved()}[/]", () => PolicyCli.ShowPage(serviceProvider, item.policy, item.policyClass), () => true);
      }
      menuItems.Add("Esc", "Exit", () => { });

      CliTools.ShowMenu(null, menuItems.ToArray());
    }

    public static void ReportSettingsRegistry()
    {
      if (!SelectPolicyClass(false, out var policyClass))
        return;
      ReportSettingsRegistry(policyClass);
    }

    public static void ReportSettingsRegistry(PolicyClass policyClass)
    {
      policyClass.CheckClassIsNotBoth();
      var section = policyClass == PolicyClass.User
        ? GpoSection.User
        : GpoSection.Machine;
      bool loop = true;
      do
      {
        var settings = GpoHelper.RunInSta(() => GpoHelper.EnumSettings(section));

        Console.WriteLine($"{settings.Count} in {policyClass} section:");
        foreach (var setting in settings)
        {
          Console.WriteLine($"{setting.Path}|{setting.Name} '{setting.Value}' ({setting.ValueKind})");
        }
        var menuItems = new List<MenuItem>();
        menuItems.Add("R", "Refresh", () => { });
        menuItems.Add("Esc", "Exit", () => { loop = false; });
        CliTools.ShowMenu(null, menuItems.ToArray());
      } while (loop);
    }

    private static void ManageCommandFile(IServiceProvider serviceProvider)
    {
       BatchCmdCli.ShowPage(serviceProvider);
   }

    private static void ChangeLanguge(IServiceProvider serviceProvider)
    {
      var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      var languages = admFolder.AvailableLanguages();
      if (!CliTools.SelectItem(languages, "Select language", AdmFolder.DefaultLanguage, out var language, l => AdmExtensions.LanguageDisplayName(l)))
        return;

      admFolder.Language = language;

      var config = serviceProvider.GetRequiredService<IConfigurationRoot>();
      config.AppSection()["admLanguage"] = language;
      config.SaveJsonProvider();
    }

    public static bool SelectPolicyClass(bool includeBoth, out PolicyClass policyClass)
    {
      Console.WriteLine("Getting Classes");
      var classes = Enum.GetValues<PolicyClass>()
        .Where(pc => includeBoth || pc != PolicyClass.Both)
        .ToList();
      return CliTools.SelectItem(classes, "Select policy class", PolicyClass.Machine, out policyClass,
        @class => @class.ToString());
    }

    public static bool SelectPolicy(LgpCategory category, [NotNullWhen(true)] out Policy? policy, PolicyClass policyClass)
    {
      List<object> items = category.Items
        .OrderBy(c => c.DisplayNameResolved())
        .Where(c => c.HasPolicyWithClass(policyClass))
        .Cast<object>()
        .ToList();
      items.AddRange(category.Policies.OrderBy(p => p.DisplayNameResolved()));
      while (true)
      {
        Console.WriteLine($"Current Category: {string.Join("\\", category.CategoryPaths())}");
        var result = CliTools.SelectItem(items, "Select category or policy", null, out var item, e =>
        {
          switch (e)
          {
            case LgpCategory c:
              return $"[Cyan]Category[/] {c.DisplayNameResolved()}" ;
            case Policy p:
              return $"[Magenta]Policy[/] {p.DisplayNameResolved()}";
            default:
              return e?.ToString();
          }
        });

        if (!result)
        {
          policy = null;
          return false;
        }
        else
        {
          switch (item)
          {
            case LgpCategory c:
              var success = SelectPolicy(c, out var locPolicy, policyClass);
              if (success)
              {
                policy = locPolicy!;
                return true;
              }
              break; //try again with parent category
            case Policy p:
              policy = p;
              return true;
            default:
              break;
          }
        }

      }
    }

    private static void DebugDi(IServiceProvider serviceProvider)
    {
#if !RELEASE
      Console.WriteLine(serviceProvider.DebugDependencies());
      CliTools.EnterToContinue();
#endif
    }
  }
}
