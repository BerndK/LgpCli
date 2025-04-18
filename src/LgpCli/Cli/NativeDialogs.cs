﻿using System.Runtime.InteropServices;

namespace Cli
{
  internal static class NativeDialogs
  {
    [Flags]
    public enum OpenFileFlags
    {
      ALLOWMULTISELECT = 0x00000200,
      //The File Name list box allows multiple selections. If you also set the OFN_EXPLORER flag, the dialog box uses the Explorer-style user interface; otherwise, it uses the old-style user interface.
      //If the user selects more than one file, the lpstrFile buffer returns the path to the current directory followed by the file names of the selected files. The nFileOffset member is the offset, in bytes or characters, to the first file name, and the nFileExtension member is not used. For Explorer-style dialog boxes, the directory and file name strings are NULL separated, with an extra NULL character after the last file name. This format enables the Explorer-style dialog boxes to return long file names that include spaces. For old-style dialog boxes, the directory and file name strings are separated by spaces and the function uses short file names for file names with spaces. You can use the FindFirstFile function to convert between long and short file names.
      //If you specify a custom template for an old-style dialog box, the definition of the File Name list box must contain the LBS_EXTENDEDSEL value.

      CREATEPROMPT = 0x00002000,
      //If the user specifies a file that does not exist, this flag causes the dialog box to prompt the user for permission to create the file. If the user chooses to create the file, the dialog box closes and the function returns the specified name; otherwise, the dialog box remains open. If you use this flag with the OFN_ALLOWMULTISELECT flag, the dialog box allows the user to specify only one nonexistent file.
      
      DONTADDTORECENT = 0x02000000,
      //Prevents the system from adding a link to the selected file in the file system directory that contains the user's most recently used documents. To retrieve the location of this directory, call the SHGetSpecialFolderLocation function with the CSIDL_RECENT flag.
      
      ENABLEHOOK = 0x00000020,
      //Enables the hook function specified in the lpfnHook member.
      
      ENABLEINCLUDENOTIFY = 0x00400000,
      //Causes the dialog box to send CDN_INCLUDEITEM notification messages to your OFNHookProc hook procedure when the user opens a folder. The dialog box sends a notification for each item in the newly opened folder. These messages enable you to control which items the dialog box displays in the folder's item list.
      
      ENABLESIZING = 0x00800000,
      //Enables the Explorer-style dialog box to be resized using either the mouse or the keyboard. By default, the Explorer-style Open and Save As dialog boxes allow the dialog box to be resized regardless of whether this flag is set. This flag is necessary only if you provide a hook procedure or custom template. The old-style dialog box does not permit resizing.
      
      ENABLETEMPLATE = 0x00000040,
      //The lpTemplateName member is a pointer to the name of a dialog template resource in the module identified by the hInstance member. If the OFN_EXPLORER flag is set, the system uses the specified template to create a dialog box that is a child of the default Explorer-style dialog box. If the OFN_EXPLORER flag is not set, the system uses the template to create an old-style dialog box that replaces the default dialog box.
      
      ENABLETEMPLATEHANDLE = 0x00000080,
      //The hInstance member identifies a data block that contains a preloaded dialog box template. The system ignores lpTemplateName if this flag is specified. If the OFN_EXPLORER flag is set, the system uses the specified template to create a dialog box that is a child of the default Explorer-style dialog box. If the OFN_EXPLORER flag is not set, the system uses the template to create an old-style dialog box that replaces the default dialog box.
      
      EXPLORER = 0x00080000,
      //Indicates that any customizations made to the Open or Save As dialog box use the Explorer-style customization methods. For more information, see Explorer-Style Hook Procedures and Explorer-Style Custom Templates.
      //By default, the Open and Save As dialog boxes use the Explorer-style user interface regardless of whether this flag is set. This flag is necessary only if you provide a hook procedure or custom template, or set the OFN_ALLOWMULTISELECT flag.
      //If you want the old-style user interface, omit the OFN_EXPLORER flag and provide a replacement old-style template or hook procedure. If you want the old style but do not need a custom template or hook procedure, simply provide a hook procedure that always returns FALSE.

      EXTENSIONDIFFERENT = 0x00000400,
      //The user typed a file name extension that differs from the extension specified by lpstrDefExt. The function does not use this flag if lpstrDefExt is NULL.
      
      FILEMUSTEXIST = 0x00001000,
      //The user can type only names of existing files in the File Name entry field. If this flag is specified and the user enters an invalid name, the dialog box procedure displays a warning in a message box. If this flag is specified, the OFN_PATHMUSTEXIST flag is also used. This flag can be used in an Open dialog box. It cannot be used with a Save As dialog box.
      
      FORCESHOWHIDDEN = 0x10000000,
      //Forces the showing of system and hidden files, thus overriding the user setting to show or not show hidden files. However, a file that is marked both system and hidden is not shown.
      
      HIDEREADONLY = 0x00000004,
      //Hides the Read Only check box.
      
      LONGNAMES = 0x00200000,
      //For old-style dialog boxes, this flag causes the dialog box to use long file names. If this flag is not specified, or if the OFN_ALLOWMULTISELECT flag is also set, old-style dialog boxes use short file names (8.3 format) for file names with spaces. Explorer-style dialog boxes ignore this flag and always display long file names.
      
      NOCHANGEDIR = 0x00000008,
      //Restores the current directory to its original value if the user changed the directory while searching for files.
      //This flag is ineffective for GetOpenFileName.

      NODEREFERENCELINKS = 0x00100000,
      //Directs the dialog box to return the path and file name of the selected shortcut (.LNK) file. If this value is not specified, the dialog box returns the path and file name of the file referenced by the shortcut.
      
      NOLONGNAMES = 0x00040000,
      //For old-style dialog boxes, this flag causes the dialog box to use short file names (8.3 format). Explorer-style dialog boxes ignore this flag and always display long file names.
      
      NONETWORKBUTTON = 0x00020000,
      //Hides and disables the Network button.
      
      NOREADONLYRETURN = 0x00008000,
      //The returned file does not have the Read Only check box selected and is not in a write-protected directory.
      
      NOTESTFILECREATE = 0x00010000,
      //The file is not created before the dialog box is closed. This flag should be specified if the application saves the file on a create-nonmodify network share. When an application specifies this flag, the library does not check for write protection, a full disk, an open drive door, or network protection. Applications using this flag must perform file operations carefully, because a file cannot be reopened once it is closed.
      
      NOVALIDATE = 0x00000100,
      //The common dialog boxes allow invalid characters in the returned file name. Typically, the calling application uses a hook procedure that checks the file name by using the FILEOKSTRING message. If the text box in the edit control is empty or contains nothing but spaces, the lists of files and directories are updated. If the text box in the edit control contains anything else, nFileOffset and nFileExtension are set to values generated by parsing the text. No default extension is added to the text, nor is text copied to the buffer specified by lpstrFileTitle. If the value specified by nFileOffset is less than zero, the file name is invalid. Otherwise, the file name is valid, and nFileExtension and nFileOffset can be used as if the OFN_NOVALIDATE flag had not been specified.
      
      OVERWRITEPROMPT = 0x00000002,
      //Causes the Save As dialog box to generate a message box if the selected file already exists. The user must confirm whether to overwrite the file.
      
      PATHMUSTEXIST = 0x00000800,
      //The user can type only valid paths and file names. If this flag is used and the user types an invalid path and file name in the File Name entry field, the dialog box function displays a warning in a message box.
      
      READONLY = 0x00000001,
      //Causes the Read Only check box to be selected initially when the dialog box is created. This flag indicates the state of the Read Only check box when the dialog box is closed.
      
      SHAREAWARE = 0x00004000,
      //Specifies that if a call to the OpenFile function fails because of a network sharing violation, the error is ignored and the dialog box returns the selected file name. If this flag is not set, the dialog box notifies your hook procedure when a network sharing violation occurs for the file name specified by the user. If you set the OFN_EXPLORER flag, the dialog box sends the CDN_SHAREVIOLATION message to the hook procedure. If you do not set OFN_EXPLORER, the dialog box sends the SHAREVISTRING registered message to the hook procedure.

      SHOWHELP = 0x00000010,
      //Causes the dialog box to display the Help button. The hwndOwner member must specify the window to receive the HELPMSGSTRING registered messages that the dialog box sends when the user clicks the Help button. An Explorer-style dialog box sends a CDN_HELP notification message to your hook procedure when the user clicks the Help button.
    }


//see https://learn.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamea
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct OpenFileName
    {
      public int lStructSize;
      public IntPtr hwndOwner;
      public IntPtr hInstance;
      public string? lpstrFilter;
      public string lpstrCustomFilter;
      public int nMaxCustFilter;
      public int nFilterIndex;
      public string lpstrFile;
      public int nMaxFile;
      public string lpstrFileTitle;
      public int nMaxFileTitle;
      public string? lpstrInitialDir;
      public string lpstrTitle;
      public OpenFileFlags Flags;
      public short nFileOffset;
      public short nFileExtension;
      public string lpstrDefExt;
      public IntPtr lCustData;
      public IntPtr lpfnHook;
      public string lpTemplateName;
      public IntPtr pvReserved;
      public int dwReserved;
      public int flagsEx;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    [DllImport("Comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetSaveFileName(ref OpenFileName lpofn);

    public static string? OpenFileDlg(string title, string? filter, string? initialDir = null)
    {
      var ofn = new OpenFileName();
      ofn.lStructSize = Marshal.SizeOf(ofn);
      ofn.lpstrFilter = filter; //"All files(*.*)\0\0";
      ofn.lpstrFile = new string(new char[300]);
      ofn.nMaxFile = ofn.lpstrFile.Length;
      ofn.lpstrFileTitle = new string(new char[64]);
      ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
      ofn.lpstrTitle = title;
      ofn.lpstrInitialDir = initialDir;
      ofn.Flags = OpenFileFlags.FILEMUSTEXIST;
      if (GetOpenFileName(ref ofn))
        return ofn.lpstrFile;
      else
        return null;
    }

    public static string? SaveFileDlg(string title, string? filter, bool overwritePrompt, string? filename = null, string? initialDir = null)
    {
      var filenameBuf = new char[300];
      if (filename != null)
        filename.CopyTo(0, filenameBuf, 0, filename.Length);
      var ofn = new OpenFileName();
      ofn.lStructSize = Marshal.SizeOf(ofn);
      ofn.lpstrFilter = filter; //"All files(*.*)\0\0";
      ofn.lpstrFile = new string(filenameBuf);
      ofn.nMaxFile = filenameBuf.Length;
      ofn.lpstrFileTitle = new string(new char[64]);
      ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
      ofn.lpstrTitle = title;
      ofn.lpstrInitialDir = initialDir;
      ofn.Flags = OpenFileFlags.PATHMUSTEXIST;
      if (overwritePrompt)
        ofn.Flags |= OpenFileFlags.OVERWRITEPROMPT;

      if (GetSaveFileName(ref ofn))
        return ofn.lpstrFile;
      else
        return null;
    }
  }
}