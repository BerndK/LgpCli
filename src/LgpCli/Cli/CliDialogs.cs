//#define cli
#define nativeDialogs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
#if !nativeDialogs
using System.Windows.Forms;
using System.Windows.Forms.Design;
#endif
namespace Cli
{

  public static class CliDialogs
  {
    public static string? OpenFileDialog(string title, string? filter, string? initialDirectory = null)
    {
      if (filter != null && !filter.Contains("|"))
        filter = $"{filter}|{filter}|All files (*.*)|*.*";
      if (filter == null)
        filter = "All files (*.*)|*.*";
#if cli
      var res = CliTools.InputQuery2(title);
      return res.cancelled ? null : res.text;
#else
#if nativeDialogs
      if (filter != null)
        filter = filter.Replace("|", "\0") + "\0";
      return NativeDialogs.OpenFileDlg(title, filter, initialDirectory);
#else
      var dlg = new OpenFileDialog()
      {
        Title = title,
        Multiselect = false,
        CheckFileExists = true, 
        Filter = filter,
        InitialDirectory = initialDirectory
      };
      
      return dlg.ShowDialog(Win32WindowProvider.Default) == DialogResult.OK
        ? dlg.FileName
        : null;
#endif
#endif
    }    
    
    public static string? SaveFileDialog(string title, string? filter, bool overwritePrompt, string? initial = null)
    {
      string? filename = null;
      string? initialDirectory = null;
      if (initial != null)
      {
        if (Path.GetFileName(initial) == string.Empty)
          initialDirectory = initial;
        else
          filename = initial;
      }

      if (filter != null && !filter.Contains("|"))
        filter = $"{filter}|{filter}|All files (*.*)|*.*";
      if (filter == null)
        filter = "All files (*.*)|*.*";
#if cli
      var res = CliTools.InputQuery2(title);
      return res.cancelled ? null : res.text;
#else
#if nativeDialogs
      if (filter != null)
        filter = filter.Replace("|", "\0") + "\0";
      return NativeDialogs.SaveFileDlg(title, filter, overwritePrompt, filename, initialDirectory);
#else
      var dlg = new SaveFileDialog()
      {
        Title = title,
        CheckPathExists = true,
        OverwritePrompt= overwritePrompt, 
        Filter = filter,
        FileName = filename,
        InitialDirectory = initialDirectory
      };
      return dlg.ShowDialog(Win32WindowProvider.Default) == DialogResult.OK
        ? dlg.FileName
        : null;
#endif
#endif
    }

    public static string? SelectFolderDialog(string title, string? startPath = null)
    {
#if cli
      var res = CliTools.InputQuery2(title);
      return res.cancelled ? null : res.text;
#else
#if nativeDialogs
      var browseForFolder = new BrowseForFolder();
      return browseForFolder.SelectFolder(title, startPath, Win32WindowProvider.Default.Handle);
#else
      var dlg = new FolderBrowserDialog()
      {
        Description = title,
        SelectedPath = startPath,
      };
      return dlg.ShowDialog(Win32WindowProvider.Default) == DialogResult.OK
        ? dlg.SelectedPath
        : null;
#endif
#endif
    }

    public class Win32WindowProvider
#if !cli && !nativeDialogs
      : IWin32Window
#endif 
    {
      private static Win32WindowProvider? defaultInstance;

      public Win32WindowProvider(IntPtr handle)
      {
        Handle = handle;
      }

      public static Win32WindowProvider Default
      {
        get
        {
          if (defaultInstance == null)
            defaultInstance = new Win32WindowProvider(ConsoleNative.GetConsoleWindow());
          return defaultInstance;
        }
      }

      public IntPtr Handle { get; private set; }
    }

    public static class ConsoleNative
    {
      [DllImport("kernel32.dll")]
      internal static extern IntPtr GetConsoleWindow();
    }
  }
}
