using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LgpCore.Infrastructure;
using System.Runtime.InteropServices.Marshalling;
using Infrastructure;

namespace LgpCore.Gpo
{
  public static unsafe partial class GpoHelper
  {
    // You can find the Guid in Gpedit.h
    public static readonly Guid REGISTRY_EXTENSION_GUID = new Guid("35378EAC-683F-11D2-A89A-00C04FBBCFA2");
    public static readonly Guid CLSID_GPESnapIn = new Guid("8FC0B734-A0E1-11d1-A7D3-0000F87571E3");

    public static IDisposable InitGpo(out IGroupPolicyObject gpo, string? remoteMachineName = null)
    {
      CheckStaThread();
#if UseAOT
      //this might work in non IOT scenarios
      //  Type comType = Type.GetTypeFromCLSID(new Guid("EA502722-A23D-11d1-A7D3-0000F87571E3"))
      //   ?? throw new InvalidOperationException("Not able to get COM GroupPolicyObject");
      //var gpo = (IGroupPolicyObject)Activator.CreateInstance(comType);

      var disposable = ComWrappersHelper.GetComInterfaceInstance<IGroupPolicyObject>(out gpo, GroupPolicyObjectGuids.ClassGuid, GroupPolicyObjectGuids.InterfaceGuid);
#else
      gpo = (IGroupPolicyObject) new GroupPolicyObject();
      var gpoForDisposal = gpo;
      var disposable = Disposable.Create(() => Marshal.ReleaseComObject(gpoForDisposal));
#endif
      try
      {
        if (string.IsNullOrEmpty(remoteMachineName))
        {
          gpo.OpenLocalMachineGPO(GpoOpen.LoadRegistry);
        }
        else
        {
          gpo.OpenRemoteMachineGPO(remoteMachineName, GpoOpen.LoadRegistry);
        }
      }
      catch
      {
        disposable.Dispose();
        throw;
      }

      return disposable;
    }

    public static RegistryKey GetRootRegistryKey(this IGroupPolicyObject gpo, GpoSection section)
    {
      gpo.GetRegistryKey(section, out var key);
      return RegistryKey.FromHandle(new SafeRegistryHandle(key, ownsHandle: true));
    }

    public static void CheckStaThread()
    {
      if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        throw new InvalidOperationException("Run this operation in an STA thread");
    }

    public static T RunInSta<T>(this Func<T> operation)
    {
      if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        return operation.Invoke();

      //T? result = default;
      //Exception? exception = null;

      //var staThread = new Thread(() =>
      //{
      //  try
      //  {
      //    result = operation.Invoke();
      //  }
      //  catch (Exception e)
      //  {
      //    exception = e;
      //  }
      //});

      //staThread.SetApartmentState(ApartmentState.STA);
      //staThread.Start();
      //staThread.Join();

      //if (exception != null)
      //  throw new AggregateException(exception);

      //return result!;
      return RunInStaTask(operation).Result;
    }

    private static Task<T> RunInStaTask<T>(Func<T> func)
    {
      var tcs = new TaskCompletionSource<T>();

      var thread = new Thread(() =>
      {
        try
        {
          tcs.SetResult(func());
        }
        catch (Exception ex)
        {
          tcs.SetException(ex);
        }
      });

      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();

      return tcs.Task;
    }
    private static Task RunInStaTask(Action action)
    {
      var tcs = new TaskCompletionSource<object?>();

      var thread = new Thread(() =>
      {
        try
        {
          action();
          tcs.SetResult(null);
        }
        catch (Exception ex)
        {
          tcs.SetException(ex);
        }
      });

      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();

      return tcs.Task;
    }


    public static void RunInSta(this Action action)
    {
      if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        action.Invoke();
      else
      {
        RunInStaTask(action).Wait();
      }
    }

    public static List<GpoSetting> EnumSettings(GpoSection section, string? path = null,
      string? remoteMachineName = null)
    {
      void EnumKeysRecurse(RegistryKey key, List<GpoSetting> results)
      {
        var valueNames = key.GetValueNames();

        foreach (var valueName in valueNames)
        {
          var gpoSetting = GetSetting(key, valueName, section);
          if (gpoSetting != null)
            results.Add(gpoSetting);
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
          using (var subKey = key.OpenSubKey(subKeyName))
          {
            if (subKey != null)
              EnumKeysRecurse(subKey, results);
          }
        }
      }

      using (InitGpo(out var gpo, remoteMachineName))
      {
        using (var key = gpo.GetRootRegistryKey(section))
        {
          var result = new List<GpoSetting>();

          if (string.IsNullOrEmpty(path))
          {
            EnumKeysRecurse(key, result);
          }
          else
          {
            using (var subKey = key.OpenSubKey(path))
            {
              if (subKey != null)
                EnumKeysRecurse(subKey, result);
            }
          }

          return result;
        }
      }
    }

    private static GpoSetting? GetSetting(RegistryKey key, string valueName, GpoSection section)
    {
      var value = key.GetValueSafeTyped(valueName);
      if (value == null)
        return null;
      var valueKind = key.GetValueKind(valueName);

      return new GpoSetting(section, key.ToString(), valueName, valueKind, value);
    }

    public static GpoSetting? GetPolicyValue(GpoSection section, string path, string valueName,
      string? remoteMachineName = null)
    {
      if (string.IsNullOrEmpty(valueName))
        throw new ArgumentNullException(nameof(valueName));

      using (InitGpo(out var gpo, remoteMachineName))
      {
        using (var key = gpo.GetRootRegistryKey(section))
        {
          if (string.IsNullOrEmpty(path))
          {
            return GetSetting(key, valueName, section);
          }
          else
          {
            using (var subKey = key.OpenSubKey(path))
            {
              if (subKey == null)
                return null;

              return GetSetting(subKey, valueName, section);
            }
          }
        }
      }
    }

    private static bool IsMachine(this GpoSection section)
    {
      switch (section)
      {
        case GpoSection.Root:
          throw new ArgumentOutOfRangeException($"{nameof(GpoSection)}.{section} is not supported.");
        case GpoSection.User:
          return false;
        case GpoSection.Machine:
          return true;
        default:
          throw new ArgumentOutOfRangeException(nameof(section), section, null);
      }
    }

    [Obsolete]
    public static bool SetPolicyValue(GpoSection section, string path, string valueName, RegistryValueKind valueKind,
      object? value, string? remoteMachineName = null)
    {
      bool SetValue(RegistryKey? key)
      {
        if (key == null)
          return false;
        if (value == null)
        {
          if (key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase))
          {
            key.DeleteValue(valueName);
            return true;
          }

          return false;
        }
        else
        {
          key.SetValue(valueName, value, valueKind);
          return true;
        }
      }

      if (string.IsNullOrEmpty(valueName))
        throw new ArgumentNullException(nameof(valueName));

      using (InitGpo(out var gpo, remoteMachineName))
      {
        using (var key = gpo.GetRootRegistryKey(section))
        {
          bool result = false;
          if (string.IsNullOrEmpty(path))
          {
            result = SetValue(key);
          }
          else
          {
            using (var subKey = value == null ? key.OpenSubKey(path, true) : key.CreateSubKey(path))
            {
              if (subKey != null)
                result = SetValue(subKey);
            }
          }

          if (result)
            gpo.Save(section.IsMachine(), true, REGISTRY_EXTENSION_GUID, CLSID_GPESnapIn);
          return result;
        }
      }
    }
  }

  public class GpoSetting
  {
    public GpoSetting(GpoSection section, string path, string name, RegistryValueKind valueKind, object value)
    {
      Section = section;
      Path = path;
      Name = name;
      ValueKind = valueKind;
      Value = value;
    }

    public GpoSection Section { get; set; }
    public string Path { get; set; }
    public string Name { get; set; }
    public RegistryValueKind ValueKind { get; set; }
    public object Value { get; set; }
  }
}

