using System.Runtime.InteropServices;
using Link360;
namespace Link360Desktop;

// IMFAttributes ABI/GUIDs from the installed Windows SDK mfobjects.h/mfidl.h.
// OpenCV's MSMF backend uses this same video-only enumeration, not DShow order.
internal static class MediaFoundationDevices {
 [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFStartup(uint version,uint flags);
 [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFShutdown();
 [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFCreateAttributes(out IntPtr attributes,uint count);
 [DllImport("mf.dll",ExactSpelling=true)]static extern int MFEnumDeviceSources(IntPtr attributes,out IntPtr devices,out uint count);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetGuid(IntPtr self,ref Guid key,ref Guid value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetString(IntPtr self,ref Guid key,out IntPtr value,out uint length);
 static T Method<T>(IntPtr instance,int slot)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size));
 static string Read(IntPtr instance,Guid key){IntPtr value=IntPtr.Zero;try{Marshal.ThrowExceptionForHR(Method<GetString>(instance,13)(instance,ref key,out value,out uint length));if(length>8192)throw new IOException("Invalid camera attribute length");return Marshal.PtrToStringUni(value,(int)length)??"";}finally{if(value!=IntPtr.Zero)Marshal.FreeCoTaskMem(value);}}
 public static List<VideoDevice> List(){
  Marshal.ThrowExceptionForHR(MFStartup(0x20070,0));IntPtr attributes=IntPtr.Zero,array=IntPtr.Zero;uint count=0;
  try{Marshal.ThrowExceptionForHR(MFCreateAttributes(out attributes,1));var key=new Guid("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");var kind=new Guid("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");Marshal.ThrowExceptionForHR(Method<SetGuid>(attributes,24)(attributes,ref key,ref kind));Marshal.ThrowExceptionForHR(MFEnumDeviceSources(attributes,out array,out count));if(count>64)throw new IOException("Unexpected camera inventory size");var devices=new List<VideoDevice>();for(int i=0;i<count;i++){var item=Marshal.ReadIntPtr(array,i*IntPtr.Size);devices.Add(new(i,Read(item,new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01")),Read(item,new("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f"))));}return devices;}
  finally{if(array!=IntPtr.Zero){for(int i=0;i<count;i++)Marshal.Release(Marshal.ReadIntPtr(array,i*IntPtr.Size));Marshal.FreeCoTaskMem(array);}if(attributes!=IntPtr.Zero)Marshal.Release(attributes);MFShutdown();}
 }
 internal static string PhysicalPath(string path){if(path.StartsWith("@device:pnp:",StringComparison.OrdinalIgnoreCase))path=path[12..];int category=path.LastIndexOf("#{",StringComparison.Ordinal);return category>0?path[..category]:path;}
 public static VideoDevice Match(VideoDevice direct){string path=PhysicalPath(direct.Path);var matches=List().Where(d=>d.Name.Equals(direct.Name,StringComparison.OrdinalIgnoreCase)&&PhysicalPath(d.Path).Equals(path,StringComparison.OrdinalIgnoreCase)).ToArray();return matches.Length==1?matches[0]:throw new IOException("Cannot identify the same camera in Media Foundation; refusing another device.");}
}
