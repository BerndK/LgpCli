using Infrastructure;
using LgpCore.Gpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
	/// <summary>
	/// This is to create a COM object and get an interface instance from it.
	/// This is useful in the context of source generated Com-Interop code.
	/// </summary>
	/// <example>
	/// 	public static class GroupPolicyObjectGuids
	///   {
	///			public const string InterfaceGuid = "EA502723-A23D-11d1-A7D3-0000F87571E3";
	///			public const string ClassGuid = "EA502722-A23D-11d1-A7D3-0000F87571E3";
	///   }
	///   
	///   [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
	///   //no [ComImport] here, as we want to have the source generator ant AOT support
	///   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	///   [Guid(GroupPolicyObjectGuids.InterfaceGuid)]
	///   public partial interface IGroupPolicyObject
	///   {
	///   	uint New(string domainName, string displayName,	GpoOpen flags);
	///  		...
	///   }
	/// </example>
	public static unsafe partial class ComWrappersHelper
	{
		public static IDisposable GetComInterfaceInstance<T>(out T instance, string classGuid, string interfaceGuid)
		{
			return GetComInterfaceInstance(out instance, new Guid(classGuid), new Guid(interfaceGuid));
		}

		public static IDisposable GetComInterfaceInstance<T>(out T instance, Guid classGuid, Guid interfaceGuid)
		{
			if (!typeof(T).IsInterface)
				throw new InvalidOperationException("Type must be an interface");

			var res = CoCreateInstance(
				&classGuid,
				null,
				/* CLSCTX_INPROC_SERVER */ 1,
				&interfaceGuid,
				out nint objPtr);
			if (res != 0 || objPtr == 0)
				throw new InvalidOperationException($"Failed to create COM object. Error code: {res}");

			var comWrappers = new StrategyBasedComWrappers();
			instance = (T) comWrappers.GetOrCreateObjectForComInstance(objPtr, CreateObjectFlags.None);
			return Disposable.Create(() => Marshal.Release(objPtr));
		}

		[LibraryImport("ole32")]
		private static partial int CoCreateInstance(Guid* rclsid, void* pUnkOuter, uint dwClsContext, Guid* riid, out nint ppv);

	}
}
