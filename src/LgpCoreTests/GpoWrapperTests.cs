using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LgpCore.Gpo;

namespace LgpCoreTests
{
  public class GpoWrapperTests
  {
    [RequiresThread(ApartmentState.STA)]
    [TestCase(GpoSection.Root)]
    [TestCase(GpoSection.Machine)]
    [TestCase(GpoSection.User)]
    public void GpoPathTest(GpoSection section)
    {
	    using (GpoHelper.InitGpo(out var gpo))
      {
        Console.WriteLine($"GpoSection: {section}");
#if UseAOT
        var sb = new char[256];
        var capacity = sb.Length - 1;
#else
        var sb = new StringBuilder(255);
        var capacity = sb.Capacity;
#endif

        gpo.GetName(sb, capacity);
        Console.WriteLine($"Name: {sb.ToString()}");
        
        gpo.GetDisplayName(sb, capacity);
        Console.WriteLine($"DisplayName: {sb.ToString()}");
        
        gpo.GetFileSysPath(section, sb, capacity);
        Console.WriteLine($"FileSysPath: {sb.ToString()}");

        gpo.GetPath(sb, capacity);
        Console.WriteLine($"Path: {sb.ToString()}");
      }
    }

    [RequiresThread(ApartmentState.STA)]
    [TestCase(GpoSection.Machine)]
    [TestCase(GpoSection.User)]
    public void EnumSettingsStaTest(GpoSection section)
    {
      var settings = GpoHelper.EnumSettings(section);

      //var settings = GpoHelper.RunStaThread(() => GpoHelper.EnumSettings((GpoWrapper.GpoSection)5));

      foreach (var setting in settings)
      {
        Console.WriteLine($"{setting.Path}|{setting.Name} '{setting.Value}' ({setting.ValueKind})");
      }
    }
  }
}
