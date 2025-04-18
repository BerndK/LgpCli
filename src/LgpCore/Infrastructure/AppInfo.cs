using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;

namespace Infrastructure
{
  public class AppInfo
  {
    public AppInfo()
    {
      var mainAsm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
      Title = mainAsm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? string.Empty;
      Desc = mainAsm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
      Ver = mainAsm.GetName().Version ?? new Version(1, 0);
      Company = mainAsm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
      Copyright = mainAsm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
      IsAdmin = Uac.IsAdmin();
    }

    public string Title { get; }
    public string Desc { get; }
    public Version Ver { get; }
    public string Company { get; }
    public string Copyright { get; }
    public bool IsAdmin { get; }
  }
}
