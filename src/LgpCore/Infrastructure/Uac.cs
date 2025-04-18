using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure
{
  public static class Uac
  {
    public static bool IsAdmin()
    {
      WindowsIdentity identity = WindowsIdentity.GetCurrent();
      WindowsPrincipal principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RequestAdminRoleRestart(Func<bool>? confirmFunc = null)
    {
      if (!IsAdmin())
      {
        if (confirmFunc == null || confirmFunc())
        {
          // Restart program and run as admin
          var currentProcess = Process.GetCurrentProcess();
          if (currentProcess.MainModule == null)
            throw new NullReferenceException("currentProcess.MainModule should not be null");
          var exeName = currentProcess.MainModule.FileName;
          ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
          startInfo.Verb = "runas";
          startInfo.UseShellExecute = true;
          //this will throw Win32Exception (0x80004005) in case user does not accept restart UAC dialog, not sure what happens if UAC is off
          Process.Start(startInfo);

          //kill current process
          Process.GetCurrentProcess().Kill();
        }
        else
        {
          //cancelled
          throw new AbortException("Restart cancelled");
        }
      }
    }
  }
}
