using Cli;
using Infrastructure;
using LgpCore;
using LgpCore.AdmParser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCli
{
  public static class BatchCmdCli
  {
    public static void ShowPage(IServiceProvider serviceProvider)
    {
      bool loop = true;
      var logger = serviceProvider.GetRequiredService<ILogger>();
      do
      {
        Console.Clear();
        CliTools.WriteLine(CliTools.TitleColor, $"Manage Batch Command File:");

        Console.WriteLine("---------------------------------------------------------------------------");
        var batchCmd = serviceProvider.GetRequiredService<BatchCmd>();
        var admFolder = serviceProvider.GetRequiredService<AdmFolder>();
        CliTools.Markup($"Current File:");
        var currentFile = batchCmd.CurrentFile;
        if (currentFile != null)
        {
          CliTools.MarkupLine($"[Value]{currentFile.FullName}[/]");
          CliTools.Markup($"CanWrite:");
          if (batchCmd.CanWrite.GetValueOrDefault())
          {
            CliTools.MarkupLine($"[Success]Can write to file[/]");
          }
          else
          {
            CliTools.MarkupLine($"[Error]Can't write to file[/]");
          }

          var lines = batchCmd.Lines;
          if (lines != null)
          {
            var commands = batchCmd.GetCommandInfos(false) ?? Enumerable.Empty<BatchCmd.BatchLineInfo>().ToList();
            CliTools.MarkupLine($"Lines: [Value]{commands.Count} commands[/] [DarkGray]({lines.Count} lines)[/]");
            foreach (var info in commands)
            {
              CliTools.Markup($" {info.Index,3} ");
              switch (info.CommandName)
              {
                case CommandLine.CommandNameGetState:
                  CliTools.Markup($"[GetState]{info.CommandName}[/]");
                  break;
                case CommandLine.CommandNameEnable:
                  CliTools.Markup($"[Enable]{info.CommandName}[/]");
                  break;
                case CommandLine.CommandNameDisable:
                  CliTools.Markup($"[Disable]{info.CommandName}[/]");
                  break;
                case CommandLine.CommandNameNotConfigure:
                  CliTools.Markup($"[NotConfigure]{info.CommandName}[/]");
                  break;
                default:
                  CliTools.Markup($"{info.CommandName}");
                  break;
              }
              
              Policy? policy = info.PolicyPrefixedName != null ? admFolder.AllPolicies.GetValueOrDefault(info.PolicyPrefixedName) : null;
              CliTools.MarkupLine($" [PrefixedName]{info.PolicyPrefixedName}[/] [Class]{info.PolicyClass}[/] [Title]{policy?.DisplayNameResolved()}[/] {policy?.CategoryPath()}");

              if (info.Errors?.Any() ?? false)
              {
                CliTools.MarkupLine($"    [Error]{info.Errors.Count} Errors in batch command line[/]");
                foreach (var infoError in info.Errors)
                  CliTools.MarkupLine($"      [Error]{infoError}[/]");
              }

              if (string.IsNullOrWhiteSpace(info.PolicyPrefixedName) || !admFolder.AllPolicies.TryGetValue(info.PolicyPrefixedName, out _))
              {
                CliTools.MarkupLine($"      [Error]'{info.PolicyPrefixedName}' - policy not found[/]");
              }
            }
          }
          else
          {
            CliTools.MarkupLine($"[Warning]No Commands defined[/]");
          }
        }
        else
        {
          CliTools.MarkupLine($"[Value]Not defined[/]");
        }

        var menuItems = new List<MenuItem>();
        menuItems.Add("S", "Select file for defining batch command", () => SelectBatchCmFile(serviceProvider, batchCmd));
        menuItems.Add("Esc", "Exit", () => { loop = false; });

        CliTools.ShowMenu(null, menuItems.ToArray());
      } while (loop);
    }

    private static void SelectBatchCmFile(IServiceProvider serviceProvider, BatchCmd batchCmd)
    {
      var currentFile = batchCmd.CurrentFile;
      var defaultFile = currentFile?.FullName;
      if (string.IsNullOrWhiteSpace(defaultFile))
        defaultFile = Path.Combine(ToolBox.GetStartupPath() + "BatchCommands.lgp");

      var filename = CliDialogs.SaveFileDialog("Select file for defining batch commands", "*.lgp", false, defaultFile);
      if (filename != null)
      {
        batchCmd.CurrentFile = new FileInfo(filename);
      }
    }
  }
}
