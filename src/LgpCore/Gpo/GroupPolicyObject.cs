using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace LgpCore.Gpo
{
  //see GPEdit.h https://github.com/tpn/winsdk-10/blob/master/Include/10.0.10240.0/um/GPEdit.h
  //see doc https://learn.microsoft.com/de-de/windows/win32/api/gpedit/nn-gpedit-igrouppolicyobject
  //registry based items are stored in HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Group Policy Objects\{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}Machine\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\LAPS
  //but are then reflected in the 'normal' registry like: HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\LAPS
  //restart GPEdit to see the changes in policies
  //the values shown in the GPEdit.msc are defined e.g. in "C:\Windows\PolicyDefinitions\LAPS.admx"
  //this is not for Audit or security based stuff located below "Windows Settings"! They are probably directly registry based or in files like the audit.csv (see AdvancedAuditPolicyWrapper.cs).
  //the registry based settings are typically located below GPEdit.msc "administrative templates"

  public static class GroupPolicyObjectGuids
  {
    public const string InterfaceGuid = "EA502723-A23D-11d1-A7D3-0000F87571E3";
    public const string ClassGuid =     "EA502722-A23D-11d1-A7D3-0000F87571E3";
  }


  /// <summary>
  /// Represents a Group Policy Object.
  /// </summary>
#if UseAOT
  //see https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation
  [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
#else
  [ComImport]
#endif
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid(GroupPolicyObjectGuids.InterfaceGuid)]
  public partial interface IGroupPolicyObject
  {
    /// <summary>
    /// Opens the specified Group Policy Object.
    /// </summary>
    /// <param name="domainName">
    /// The name of the domain where the GPO is located.
    /// </param>
    /// <param name="displayName">
    /// The display name of the GPO.
    /// </param>
    /// <param name="flags">
    /// The flags to use when opening the GPO.
    /// </param>
    /// <returns></returns>
    uint New(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
#endif
      string domainName, 
      [MarshalAs(UnmanagedType.LPWStr)] 
      string displayName, 
      GpoOpen flags);

    /// <summary>
    /// Opens the specified Group Policy Object.
    /// </summary>
    /// <param name="path">
    /// The path to the GPO.
    /// </param>
    /// <param name="flags">
    /// The flags to use when opening the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint OpenDSGPO(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
#endif
      string path, 
      GpoOpen flags);

    /// <summary>
    /// Opens the local machine Group Policy Object.
    /// </summary>
    /// <param name="flags">
    /// The flags to use when opening the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint OpenLocalMachineGPO(GpoOpen flags);

    /// <summary>
    /// Opens the Group Policy Object on a remote machine.
    /// </summary>
    /// <param name="computerName">
    /// The name of the remote computer.
    /// </param>
    /// <param name="flags">
    /// The flags to use when opening the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint OpenRemoteMachineGPO([MarshalAs(UnmanagedType.LPWStr)] string computerName, GpoOpen flags);

    /// <summary>
    /// Saves the specified Group Policy Object.
    /// </summary>
    /// <param name="machine">
    /// If true, the machine settings are saved.
    /// </param>
    /// <param name="add">
    /// If true, the GPO is added to the list of GPOs.
    /// </param>
    /// <param name="extension">
    /// The GUID of the extension.
    /// </param>
    /// <param name="app">
    /// The GUID of the application.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint Save(
      [MarshalAs(UnmanagedType.Bool)] bool machine, 
      [MarshalAs(UnmanagedType.Bool)] bool add,
#if !UseAOT
      [MarshalAs(UnmanagedType.LPStruct)] 
#endif
      Guid extension,
#if !UseAOT
      [MarshalAs(UnmanagedType.LPStruct)] 
#endif
      Guid app);

    /// <summary>
    /// Deletes the Group Policy Object.
    /// </summary>
    /// <returns></returns>
    uint Delete();

    /// <summary>
    /// Closes the Group Policy Object.
    /// </summary>
    /// <param name="name">
    /// The name of the GPO.
    /// </param>
    /// <param name="maxLength">
    /// The maximum length of the name.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetName(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder name, 
#else
      [MarshalUsing(CountElementName = "maxLength")]
      char[] name,
#endif
      int maxLength);

    /// <summary>
    /// Gets the display name of the Group Policy Object.
    /// </summary>
    /// <param name="name">
    /// The display name of the GPO.
    /// </param>
    /// <param name="maxLength">
    /// The maximum length of the display name.
    /// </param>
    /// <returns></returns>
    uint GetDisplayName(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder name, 
#else
      [MarshalUsing(CountElementName = "maxLength")]
      char[] name,
#endif
      int maxLength);

    /// <summary>
    /// Sets the display name of the Group Policy Object.
    /// </summary>
    /// <param name="name">
    /// The display name of the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name);

    /// <summary>
    /// Gets the path to the Group Policy Object.
    /// </summary>
    /// <param name="path">
    /// The path to the GPO.
    /// </param>
    /// <param name="maxPath">
    /// The maximum length of the path.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetPath(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder path, 
#else
      [MarshalUsing(CountElementName = "maxPath")]
      char[] path,
#endif
      int maxPath);

    /// <summary>
    /// Gets the path to the Group Policy Object.
    /// </summary>
    /// <param name="section">
    /// The section of the GPO.
    /// </param>
    /// <param name="path">
    /// The path to the GPO.
    /// </param>
    /// <param name="maxPath">
    /// The maximum length of the path.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetDSPath(
      GpoSection section,
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder path, 
#else
      [MarshalUsing(CountElementName = "maxPath")]
      char[] path,
#endif
      int maxPath);

    /// <summary>
    /// Gets the path to the Group Policy Object.
    /// </summary>
    /// <param name="section">
    /// The section of the GPO.
    /// </param>
    /// <param name="path">
    /// The path to the GPO.
    /// </param>
    /// <param name="maxPath">
    /// The maximum length of the path.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetFileSysPath(
      GpoSection section,
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder path, 
#else
      [MarshalUsing(CountElementName = "maxPath")]
      char[] path,
#endif
      int maxPath);

    /// <summary>
    /// Gets the registry key for the Group Policy Object.
    /// </summary>
    /// <param name="section">
    /// The section of the GPO.
    /// </param>
    /// <param name="key">
    /// The registry key for the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetRegistryKey(GpoSection section, out IntPtr key);

    /// <summary>
    /// Gets the options for the Group Policy Object.
    /// </summary>
    /// <param name="options">
    /// The options for the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetOptions(out GpoOption options);

    /// <summary>
    /// Sets the options for the Group Policy Object.
    /// </summary>
    /// <param name="options">
    /// The options for the GPO.
    /// </param>
    /// <param name="mask">
    /// The mask to use when setting the options.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint SetOptions(GpoOption options, uint mask);

    /// <summary>
    /// Gets the type of the Group Policy Object.
    /// </summary>
    /// <param name="gpoType">
    /// The type of the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetType(out GpoType gpoType);

    /// <summary>
    /// Gets the machine name.
    /// </summary>
    /// <param name="name">
    /// The name of the machine.
    /// </param>
    /// <param name="maxLength">
    /// The maximum length of the name.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetMachineName(
#if !UseAOT
      [MarshalAs(UnmanagedType.LPWStr)] 
      StringBuilder name,
#else
      [MarshalUsing(CountElementName = "maxLength")]
      char[] name,
#endif

      int maxLength);

    /// <summary>
    /// Gets the property sheet pages for the Group Policy Object.
    /// </summary>
    /// <param name="pages">
    /// The property sheet pages for the GPO.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is S_OK.
    /// </returns>
    uint GetPropertySheetPages(out IntPtr pages);
  }

#if UseAOT
  //we don't need a class here, because we use another way to create the object (see GpoHelper.InitGpo)
  //[GeneratedComClass] //this seems to be needed if you want to offer the com-object to other applications (we just want to use existing objects)
  ////[ComImport]
  //[Guid(GroupPolicyObjectGuids.ClassGuid)]
  //public partial class GroupPolicyObject
  //{ }
#else
	/// <summary>
	/// Group Policy Class definition from COM. You can find the Guid in Gpedit.h
	/// </summary>
	[ComImport]
  [Guid(GroupPolicyObjectGuids.ClassGuid)]
  public partial class GroupPolicyObject
  { }
#endif

}