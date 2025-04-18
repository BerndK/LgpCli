using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cli;
using LgpCore.AdmParser;
using LgpCore.Gpo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCli
{
  public static class SearchCli
  {
    public static void ShowPage(IServiceProvider serviceProvider)
    {
      bool loop = true;
      AdmFolder admFolder = serviceProvider.GetRequiredService<AdmFolder>();
      bool searchName = true;
      bool searchTitle = true;
      bool searchDescription = true;
      bool searchCategories = true;
      PolicyClass policyClass = PolicyClass.Both;
      string? searchText = null;

      DefineSearchText(ref searchText);

      do
      {
        Console.Clear();
        CliTools.WriteLine(CliTools.TitleColor, $"Search Categories and Policies");
        Console.WriteLine("---------------------------------------------------------------------------");
        ReportAllCount(admFolder);
        
        var menuItems = new List<MenuItem>();

        if (searchText != null)
        {
          Console.WriteLine();
          CliTools.Markup($"searching for '[White]{searchText}[/]'");
          var foundItems = admFolder.Search(searchText, searchName, searchTitle, searchDescription, searchCategories, policyClass);
          if (foundItems.Any())
          {
            CliTools.MarkupLine($" -> Found {foundItems.Count} items");
            foreach (var foundItem in foundItems)
            {
              menuItems.Add(ItemDisplayText(foundItem), () =>
              {
                switch (foundItem)
                {
                  case LgpCategory c:
                    if (MainCli.SelectPolicy(c, out var policy, policyClass))
                    {
                      PolicyCli.ShowPage(serviceProvider, policy, policyClass);
                    }
                    break;
                  case Policy p:
                    PolicyCli.ShowPage(serviceProvider, p, policyClass);
                    break;
                }
              }, () => true);
            }
          }
          else
          {
            CliTools.MarkupLine(" -> No items found!");
          }
        }

        menuItems.AddSeparator("--- Modify search options ---");
        menuItems.AddCheckBox("N", "Search Name (Id)", searchName, b => searchName = b);
        menuItems.AddCheckBox("T", "Search Title", searchTitle, b => searchTitle = b);
        menuItems.AddCheckBox("D", "Search Description", searchDescription, b => searchDescription = b);
        menuItems.AddCheckBox("C", "Search Categories", searchCategories, b => searchCategories = b);
        menuItems.Add("PC", $"Policy Class [Class]{policyClass}[/]", () => policyClass = (PolicyClass) (((int) policyClass + 1) % Enum.GetValues<PolicyClass>().Count()), () => true);
        menuItems.Add("S", $"Modify Search text '[White]{searchText}[/]'", () => DefineSearchText(ref searchText), () => true);
        menuItems.Add("CS", "Clear Search text", () => searchText = null, () => true);
        
        menuItems.Add("Esc", "Exit", () => { loop = false; });

        CliTools.ShowMenu(null, menuItems.ToArray());
      } while (loop);

    }

    public static void ReportAllCount(AdmFolder admFolder)
    {
      CliTools.MarkupLine($"{admFolder.AllCategories.Count} [Category]Categories[/], {admFolder.AllPolicies.Count} [Policy]Policies[/], Language:{AdmExtensions.LanguageDisplayName(admFolder.Language)}");
    }

    public static string ItemDisplayText(object item)
    {
      switch (item)
      {
        case LgpCategory c:
          return $"[Category]Category[/] {c.DisplayNameResolved()}";
        case Policy p:
          return $"[Policy]Policy[/] [Class]{p.Class}[/] [Title]{p.DisplayNameResolved()}[/] [PrefixedName]({p.PrefixedName()})[/]";
        default:
          return item.ToString() ?? string.Empty;
      }
    }

    private static void DefineSearchText(ref string? searchText)
    {
      var saveSearchText = searchText;
      if (CliTools.InputQuery("Search Text (use '|' to separate tokens)", out saveSearchText, saveSearchText))
      {
        searchText = saveSearchText;
      }
    }
  }
}
