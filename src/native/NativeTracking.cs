#nullable disable
using System;
using System.Runtime.InteropServices;
namespace Link360 {
 [StructLayout(LayoutKind.Sequential)] struct KsNodeProperty {public Guid Set;public uint Id,Flags,NodeId,Reserved;}
 [ComImport,Guid("720D4AC0-7533-11D0-A5D6-28DB04C10000"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IKsTopologyInfo {
  [PreserveSig] int NumCategories(out uint n);[PreserveSig] int Category(uint n,out Guid g);
  [PreserveSig] int NumConnections(out uint n);[PreserveSig] int Connection(uint n,IntPtr p);
  [PreserveSig] int NodeName(uint n,IntPtr p,uint size,out uint length);
  [PreserveSig] int NumNodes(out uint n);[PreserveSig] int NodeType(uint n,out Guid g);
  [PreserveSig] int CreateNodeInstance(uint n,ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)] out object node);
 }
 [ComImport,Guid("28F54685-06FD-11D2-B27A-00A0C9223196"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IKsControl {
  [PreserveSig] int KsProperty(ref KsNodeProperty property,uint propertyLength,[In,Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=3)] byte[] data,uint dataLength,out uint returned);
  [PreserveSig] int KsMethod(IntPtr method,uint methodLength,IntPtr data,uint dataLength,out uint returned);
  [PreserveSig] int KsEvent(IntPtr evt,uint eventLength,IntPtr data,uint dataLength,out uint returned);
 }
 public static partial class CameraProbe {
  // Original Link descriptor and selector observed in USBPcap; no Link 2 guesses.
  static readonly Guid TrackingExtension=new("faf1672d-b71b-4793-8c91-7b1c9b7f95f8");
  public static System.Collections.Generic.List<CameraReading> NativeTracking(bool? enabled,double? targetX=null,double? targetY=null){
   if(enabled==true)throw new InvalidOperationException("Native activation is disabled pending validated target-selection semantics; use palm activation");
   if(enabled==true&&(!targetX.HasValue||!targetY.HasValue||!double.IsFinite(targetX.Value+targetY.Value)||targetX<0||targetX>1||targetY<0||targetY>1))throw new ArgumentException("Native activation needs a fresh detected target inside the Link view");
   RequireOneCamera();return Inspect(false,null,0,3,(camera,reading)=>{
    var topology=(IKsTopologyInfo)camera;Marshal.ThrowExceptionForHR(topology.NumNodes(out uint count));if(count>64)throw new Exception("Unexpected camera topology");
    IKsControl control=null;object owned=null;uint matched=0;int matches=0;
    try{
     for(uint i=0;i<count;i++){
      Marshal.ThrowExceptionForHR(topology.NodeType(i,out Guid type));if(type!=new Guid("941C7AC0-C559-11D0-8A2B-00A0C9255AC1"))continue;
      object node=null;try{var iid=typeof(IKsControl).GUID;if(topology.CreateNodeInstance(i,ref iid,out node)<0)continue;var candidate=(IKsControl)node;var property=new KsNodeProperty{Set=TrackingExtension,Id=2,Flags=0x10000001,NodeId=i};var data=new byte[52];int hr=candidate.KsProperty(ref property,32,data,52,out uint size);if(hr<0||size!=52)continue;matches++;matched=i;control=candidate;owned=node;node=null;}finally{Release(node);}
     }
     if(matches!=1||control==null)throw new Exception("Expected one verified original-Link tracking extension");
     byte[] Read(){var property=new KsNodeProperty{Set=TrackingExtension,Id=2,Flags=0x10000001,NodeId=matched};var data=new byte[52];Marshal.ThrowExceptionForHR(control.KsProperty(ref property,32,data,52,out uint size));if(size!=52)throw new Exception("Unexpected tracking payload length");return data;}
     var original=Read();uint flag=BitConverter.ToUInt32(original,0);for(int attempt=0;flag==255&&attempt<30;attempt++){System.Threading.Thread.Sleep(100);original=Read();flag=BitConverter.ToUInt32(original,0);}reading.NativeTrackingEnabled=flag<=1?flag==1:null;
     if(enabled.HasValue){if((flag>1&&!(flag==255&&!enabled.Value))||System.Linq.Enumerable.Any(original.AsSpan(4,34).ToArray(),b=>b!=0))throw new Exception("Unrecognized native tracking configuration");
      // SET tail is the vendor's observed no-position-change sentinel. GET tail
      // contains live PTZ telemetry and must never be copied into a SET request.
      var desired=Convert.FromHexString("00000000000000000000000000000000000000000000000000000000000000000000000000001a0e00001a0e00001a0e00000000");
      Array.Copy(BitConverter.GetBytes(enabled.Value?1u:0u),desired,4);var property=new KsNodeProperty{Set=TrackingExtension,Id=2,Flags=0x10000002,NodeId=matched};Marshal.ThrowExceptionForHR(control.KsProperty(ref property,32,desired,52,out _));
      uint observed=255;for(int attempt=0;attempt<30;attempt++){System.Threading.Thread.Sleep(100);observed=BitConverter.ToUInt32(Read(),0);if(observed==(enabled.Value?1u:0u))break;}
      if(observed!=(enabled.Value?1u:0u))throw new Exception("Native tracking state did not settle within three seconds; refresh camera state");reading.NativeTrackingEnabled=enabled.Value;}
     reading.Operation=enabled.HasValue?"NativeTrackingSetVerified":"NativeTrackingRead";
    }finally{Release(owned);}
   });
  }
 }
}
