using Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgpCli
{
  public static class TemplateCli
  {
    public static void ShowPage(IServiceProvider serviceProvider)
    {
      bool loop = true;
      var logger = serviceProvider.GetRequiredService<ILogger>();
      do
      {
        Console.Clear();
        CliTools.WriteLine(CliTools.TitleColor, $"Dialog:");
        CliTools.Markup($"[Policy]Policy[/]");
        Console.WriteLine("---------------------------------------------------------------------------");

        var menuItems = new List<MenuItem>();
        menuItems.Add("G", "Get current values from system", () => { CliTools.WarnMessage("Not implemented."); });
        menuItems.Add("Esc", "Exit", () => { loop = false; });

        CliTools.ShowMenu(null, menuItems.ToArray());
      } while (loop);
    }
  }
}
