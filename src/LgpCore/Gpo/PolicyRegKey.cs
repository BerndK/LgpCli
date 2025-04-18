using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using LgpCore.AdmParser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Win32;

namespace LgpCore.Gpo
{
  //This class is used to switch between the policy's reg-key and the item one's (like ValueItem or Element)
  //always use the Property RegKey, in case use SwitchToLocalRegKey and call the dispose
  public class PolicyRegKey : IDisposable
  {
    private RegistryKey rootKey;
    private RegistryKey? regKey;
    private bool regKeyIsWritable;
    private string sRegKey;
    internal Policy policy;

    public PolicyRegKey(RegistryKey rootKey, Policy policy, bool writable)
    {
      this.rootKey = rootKey;
      sRegKey = policy.RegKey;
      this.policy = policy;

      regKeyIsWritable = writable;
      ReInit();
    }

    public void ReInit()
    {
      if (Level != 0)
        throw new InvalidOperationException($"Level should be 0, but is {Level}");
      if (SRegKey != policy.RegKey)
        throw new InvalidOperationException($"sRegKey should be {policy.RegKey}, but is {SRegKey}");

      regKey = regKeyIsWritable
        ? rootKey.CreateSubKey(sRegKey)
        : rootKey.OpenSubKey(sRegKey, false);
    }

    public IDisposable SwitchToLocalRegKey(string? localRegKey, bool writable)
    {
      if ((string.IsNullOrWhiteSpace(localRegKey) || string.Equals(sRegKey, localRegKey, StringComparison.OrdinalIgnoreCase)) && regKeyIsWritable == writable)
      {
        return Disposable.Empty;
      }

      var oldRegKey = RegKey;
      var oldregKeyIsWritable = regKeyIsWritable;
      var oldsRegKey = sRegKey;
      if (writable)
        regKey = rootKey.CreateSubKey(localRegKey ?? oldsRegKey);
      else
        regKey = rootKey.OpenSubKey(localRegKey ?? oldsRegKey, false);
      regKeyIsWritable = writable;
      sRegKey = localRegKey ?? oldsRegKey;
      Level++;
      return Disposable.Create(() =>
      {
        regKey?.Dispose();
        regKey = oldRegKey;
        sRegKey = oldsRegKey;
        regKeyIsWritable = oldregKeyIsWritable;
        Level--;
      });
    }

    public RegistryKey? RegKey => regKey;
    public string SRegKey => sRegKey;
    public bool IsWritable => regKeyIsWritable;
    public int Level { get; private set; } = 0;
    public void Dispose()
    {
      regKey?.Dispose();
    }
  }
}
