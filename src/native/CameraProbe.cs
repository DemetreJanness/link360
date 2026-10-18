#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Link360 {
 [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ICreateDevEnum {
  [PreserveSig] int CreateClassEnumerator(ref Guid category, out IEnumMoniker enumerator, int flags);
 }
 [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IPropertyBag {
  [PreserveSig] int Read([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] out object value, IntPtr errorLog);
  [PreserveSig] int Write([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] ref object value);
 }
 [ComImport, Guid("C6E13370-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IAMCameraControl {
  [PreserveSig] int GetRange(int property, out int min, out int max, out int step, out int def, out int caps);
  [PreserveSig] int Set(int property, int value, int flags);
  [PreserveSig] int Get(int property, out int value, out int flags);
 }
 public class ControlReading {
  public string Name;
  public int RangeHResult, GetHResult, Min, Max, Step, Default, Capabilities, Value, Flags;
 }
 public class CameraReading {
  public string Name, Error;
  public string ErrorKind;
  public string Operation;
  public int? OriginalValue, RequestedValue, ReadbackValue;
  public bool? NativeTrackingEnabled;
  public int? CommandedPan,CommandedTilt,ResolvedTargetPan,ResolvedTargetTilt;
  public long SnapshotUtcMilliseconds;
  public int MotionCommands,MotionElapsedMs,MotionDurationMs;
  public List<ControlReading> Controls = new List<ControlReading>();
 }
 public static partial class CameraProbe {
  static void Release(object o) { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
  public static List<CameraReading> Read() { return Inspect(false); }
  public static List<CameraReading> ReadRelativeCapabilities(){RequireOneCamera();return Inspect(false,null,0,3,(controls,reading)=>{
   // Windows SDK KSPROPERTY_CAMERACONTROL_PAN_RELATIVE=10, TILT_RELATIVE=11.
   // Read-only capability probe; no relative or velocity commands are sent.
   foreach(var pair in new[]{Tuple.Create(10,"RelativePan"),Tuple.Create(11,"RelativeTilt")}){var c=new ControlReading{Name=pair.Item2};c.RangeHResult=controls.GetRange(pair.Item1,out c.Min,out c.Max,out c.Step,out c.Default,out c.Capabilities);c.GetHResult=controls.Get(pair.Item1,out c.Value,out c.Flags);reading.Controls.Add(c);}
   try{var kernel=(IKsControl)controls;foreach(var pair in new[]{Tuple.Create(0,"RelativeProbeKnownPanControl"),Tuple.Create(9,"RelativeProbeCombinedAbsoluteSupport"),Tuple.Create(10,"RelativePanKernelSupport"),Tuple.Create(11,"RelativeTiltKernelSupport"),Tuple.Create(17,"RelativePanTiltKernelSupport")}){var property=new KsNodeProperty{Set=new Guid("C6E13370-30AC-11D0-A18C-00A0C9118956"),Id=(uint)pair.Item1,Flags=0x200};var bytes=new byte[4];int hr=kernel.KsProperty(ref property,24,bytes,4,out uint returned);reading.Controls.Add(new ControlReading{Name=pair.Item2,RangeHResult=hr,GetHResult=hr,Capabilities=hr>=0&&returned==4?BitConverter.ToInt32(bytes,0):0});}}catch(InvalidCastException){reading.Controls.Add(new ControlReading{Name="RelativeKernelInterface",RangeHResult=unchecked((int)0x80004002),GetHResult=unchecked((int)0x80004002)});}
  });}
  static void RequireOneCamera() {
   var cameras=Read();
   if (cameras.Count!=1) throw new Exception("Expected exactly one Insta360 Link camera before writing controls");
   if (cameras[0].Error!=null) throw new Exception(cameras[0].Error);
  }
  public static List<CameraReading> TestZoom() { RequireOneCamera(); return Inspect(true); }
  public static List<CameraReading> TestPanTilt() {
   RequireOneCamera();
   var pan=Inspect(true,null,0,0);
   if (pan[0].Error!=null) return pan;
   pan.AddRange(Inspect(true,null,0,1));
   return pan;
  }
  public static List<CameraReading> SetControl(string name, int value) {
   string[] allowed = {"Pan","Tilt","Roll","Zoom","Focus"};
   int[] ids = {0,1,2,3,6};
   int index = Array.FindIndex(allowed, n => String.Equals(n,name,StringComparison.OrdinalIgnoreCase));
   if (index<0) throw new ArgumentException("Unsupported control");
   RequireOneCamera();
   return Inspect(false,ids[index],value);
  }
  static List<CameraReading> Inspect(bool testZoom, int? requestedProperty=null, int requestedValue=0, int testProperty=3, Action<IAMCameraControl,CameraReading> motion=null) {
   var result = new List<CameraReading>();
   object dev = null;
   IEnumMoniker enumerator = null;
   try {
    dev = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86")));
    var category = new Guid("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    int hr = ((ICreateDevEnum)dev).CreateClassEnumerator(ref category, out enumerator, 0);
    if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    if (hr == 1) return result;
    var monikers = new IMoniker[1];
    while (enumerator.Next(1, monikers, IntPtr.Zero) == 0) {
     object bag = null, filter = null;
     try {
      var bagId = typeof(IPropertyBag).GUID;
      monikers[0].BindToStorage(null, null, ref bagId, out bag);
      object name;
      Marshal.ThrowExceptionForHR(((IPropertyBag)bag).Read("FriendlyName", out name, IntPtr.Zero));
      if (!String.Equals(Convert.ToString(name), "Insta360 Link", StringComparison.OrdinalIgnoreCase)) continue;
      var reading = new CameraReading { Name = Convert.ToString(name) };
      result.Add(reading);
      try {
       var filterId = new Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770");
       monikers[0].BindToObject(null, null, ref filterId, out filter);
       var controls = (IAMCameraControl)filter;
       if(motion!=null) motion(controls,reading);
       if (requestedProperty.HasValue) {
        int min,max,step,def,caps,original,flags;
        int p = requestedProperty.Value;
        Marshal.ThrowExceptionForHR(controls.GetRange(p,out min,out max,out step,out def,out caps));
        Marshal.ThrowExceptionForHR(controls.Get(p,out original,out flags));
        if ((caps & 2)==0 || step<=0 || requestedValue<min || requestedValue>max || ((long)requestedValue-min)%step!=0)
         throw new ArgumentOutOfRangeException("value", "Value must match the camera's manual range and step");
        reading.Operation="SetControl";
        reading.OriginalValue=original;
        reading.RequestedValue=requestedValue;
        Marshal.ThrowExceptionForHR(controls.Set(p,requestedValue,2));
        int observed, observedFlags;
        Marshal.ThrowExceptionForHR(controls.Get(p,out observed,out observedFlags));
        reading.ReadbackValue=observed;
        if (observed!=requestedValue) throw new Exception("Control readback differs from requested value");
       }
       if (testZoom) {
        int min,max,step,def,caps,original,flags;
        Marshal.ThrowExceptionForHR(controls.GetRange(testProperty,out min,out max,out step,out def,out caps));
        Marshal.ThrowExceptionForHR(controls.Get(testProperty,out original,out flags));
        if ((caps & 2)==0 || step<=0) throw new Exception("Manual zoom unavailable");
        int target = original + step <= max ? original + step : original - step;
        if (target < min || target == original) throw new Exception("No safe adjacent zoom value");
        reading.Operation="Test" + new string[]{"Pan","Tilt","Roll","Zoom"}[testProperty] + "AndRestore";
        reading.OriginalValue=original;
        reading.RequestedValue=target;
        try {
         Marshal.ThrowExceptionForHR(controls.Set(testProperty,target,2));
         int observed, observedFlags;
         Marshal.ThrowExceptionForHR(controls.Get(testProperty,out observed,out observedFlags));
         reading.ReadbackValue=observed;
         if (observed != target) throw new Exception("Zoom readback differs from requested value");
        } finally {
         Marshal.ThrowExceptionForHR(controls.Set(testProperty,original,flags));
         int restored, restoredFlags;
         Marshal.ThrowExceptionForHR(controls.Get(testProperty,out restored,out restoredFlags));
         if (restored != original || restoredFlags != flags) throw new Exception("Zoom restoration could not be verified");
        }
       }
       string[] names = { "Pan", "Tilt", "Roll", "Zoom", "Exposure", "Iris", "Focus" };
       for (int p=0; p<names.Length; p++) {
        var c = new ControlReading { Name=names[p] };
        c.RangeHResult = controls.GetRange(p, out c.Min, out c.Max, out c.Step, out c.Default, out c.Capabilities);
        c.GetHResult = controls.Get(p, out c.Value, out c.Flags);
        reading.Controls.Add(c);
       }
      } catch (Exception e) { reading.Error = e.Message; }
     } finally { Release(filter); Release(bag); Release(monikers[0]); }
    }
   } finally { Release(enumerator); Release(dev); }
   return result;
  }
 }
}
