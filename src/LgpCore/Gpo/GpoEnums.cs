using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LgpCore.Gpo
{
  public enum GpoSection : uint
  {
    /// <summary>
    /// The root section of the Group Policy Object. GPO_SECTION_ROOT 0
    /// </summary>
    Root = 0,

    /// <summary>
    /// The user section of the Group Policy Object. GPO_SECTION_USER 1
    /// </summary>
    User = 1,

    /// <summary>
    /// The machine section of the Group Policy Object. GPO_SECTION_MACHINE 2
    /// </summary>
    Machine = 2
  }

  [Flags]
  public enum GpoOpen : uint
  {
    /// <summary>
    /// Load the registry files  GPO_OPEN_LOAD_REGISTRY = 0x00000001
    /// </summary>
    LoadRegistry = 1,

    /// <summary>
    /// Open the GPO as read only  GPO_OPEN_READ_ONLY     = 0x00000002
    /// </summary>
    ReadOnly = 2,
  }

  [Flags]
  public enum GpoOption : uint
  {
    /// <summary>
    /// 
    /// </summary>
    NonVolatile = 0,

    /// <summary>
    /// 
    /// </summary>
    DisableUser = 1,

    /// <summary>
    /// 
    /// </summary>
    DisableMachine = 2,
  }

  public enum GpoType : uint
  {
    Local = 0, // Default GPO on the local machine
    Remote, // GPO on a remote machine
    DS, // GPO in the Active Directory
    LocalUser, // User-specific GPO on the local machine
    LocalGroup // Group-specific GPO on the local machine
  }

  public enum GpoHint : uint
  {
    Unknown = 0, // No link information available
    Machine, // GPO linked to a machine (local or remote)
    Site, // GPO linked to a site
    Domain, // GPO linked to a domain
    OrganizationalUnit, // GPO linked to a organizational unit
  }

}
