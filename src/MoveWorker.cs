// Copyright (c) 2026 Jong Bhak. Licensed under BioLicense 1.0; see LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;

namespace BioPager {
 // Persistent isolated helper: COM and native exports are initialized once.
 // Calls still run outside the pager process; no Explorer injection or elevation.
 internal static class MoveWorker {
  static IntPtr nativeModule;
  [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
  [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Count();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SwitchDesktop(int index);
  static IDesktopManager manager;
  static Count count,currentNumber;
  static DesktopId id;
  static WindowQuery pinned,pinnedApp;
  static MoveWindow move;
  static SwitchDesktop changeDesktop;
  static Count createDesktop;
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DeleteDesktop(int source,int fallback);
  static DeleteDesktop removeDesktop;
  static int ready=-1;
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate Guid DesktopId(int index);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int WindowQuery(IntPtr window);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int MoveWindow(IntPtr window,int index);
  static T Export<T>(IntPtr module,string name) where T:class {
   IntPtr address=GetProcAddress(module,name);if(address==IntPtr.Zero)throw new EntryPointNotFoundException(name);
   return Marshal.GetDelegateForFunctionPointer(address,typeof(T)) as T;
  }
  static List<Guid> DesktopIds(){
   List<Guid> ids=new List<Guid>();
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops")){
    byte[] b=k==null?null:k.GetValue("VirtualDesktopIDs") as byte[];
    if(b!=null&&b.Length%16==0)for(int i=0;i<b.Length;i+=16){byte[] g=new byte[16];Array.Copy(b,i,g,0,16);ids.Add(new Guid(g));}
   }return ids;
  }
  static string backend;
  static int LegacyCount(){return Legacy21.Desktop.Count;}
  static Guid LegacyId(int index){return Legacy21.DesktopManager.GetDesktop(index).GetId();}
  static int LegacyCurrent(){return Legacy21.Desktop.FromDesktop(Legacy21.Desktop.Current);}
  static int LegacyPinned(IntPtr window){return Legacy21.Desktop.IsWindowPinned(window)?1:0;}
  static int LegacyAppPinned(IntPtr window){return Legacy21.Desktop.IsApplicationPinned(window)?1:0;}
  static int LegacyMove(IntPtr window,int index){Legacy21.Desktop.FromIndex(index).MoveWindow(window);return 0;}
  static int LegacySwitch(int index){Legacy21.Desktop.FromIndex(index).MakeVisible();return 0;}
  static int LegacyCreate(){return Legacy21.Desktop.FromDesktop(Legacy21.Desktop.Create());}
  static int LegacyRemove(int source,int target){Legacy21.Desktop.FromIndex(source).Remove(Legacy21.Desktop.FromIndex(target));return 0;}
  internal static int Run(string[] args){
   SetErrorMode(0x0001|0x0002); // Native failures return an exit code, not a modal OS dialog.
   try{
    long raw;uint pid;Guid source,target;
    if(args.Length!=5||!Int64.TryParse(args[1],out raw)||!UInt32.TryParse(args[2],out pid)||!Guid.TryParse(args[3],out source)||!Guid.TryParse(args[4],out target))return 10;
    int init=EnsureReady();if(init!=0)return init;
    IntPtr window=new IntPtr(raw);uint actualPid;
    if(!Native.IsWindow(window))return 11;
    Native.GetWindowThreadProcessId(window,out actualPid);if(actualPid!=pid)return 11;
    Guid actual;if(manager.GetWindowDesktopId(window,out actual)!=0||actual!=source)return 12;
    if(source==target)return 0;
    int pin=pinned(window),appPin=pinnedApp(window);if(pin==1||appPin==1)return 24;if(pin<0||appPin<0)return 23;
    List<Guid> ids=DesktopIds();int from=ids.IndexOf(source),to=ids.IndexOf(target);
    if(from<0||to<0||count()!=ids.Count)return 12;
    // Verify native enumeration agrees with Explorer before moving anything.
    if(id(from)!=source||id(to)!=target)return 12;
    if(!Native.IsWindow(window))return 11;
    Native.GetWindowThreadProcessId(window,out actualPid);if(actualPid!=pid)return 11;
    if(manager.GetWindowDesktopId(window,out actual)!=0||actual!=source)return 12;
    int result=move(window,to);
    for(int n=0;n<30;n++){
     if(manager.GetWindowDesktopId(window,out actual)==0&&actual==target)return 0;
     Thread.Sleep(10);
    }
    return result<0?25:26;
   }catch {return 40;}
  }
  static int EnsureReady(){
   if(ready!=-1)return ready;
   try{
    backend=WindowsCompatibility.Backend();
    if(!Environment.Is64BitProcess||backend==null)return ready=20;
    int code=Initialize();ready=code;return code;
   }catch{return ready=40;}
  }
  static int Initialize(){
    manager=(IDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
    if(backend=="legacy21"){
     count=LegacyCount;id=LegacyId;currentNumber=LegacyCurrent;pinned=LegacyPinned;pinnedApp=LegacyAppPinned;
     move=LegacyMove;changeDesktop=LegacySwitch;createDesktop=LegacyCreate;removeDesktop=LegacyRemove;
     return VerifyEnumeration();
    }
    string path;
    try{path=NativePayload.Extract(backend);}catch(InvalidDataException){return 22;}catch{return 21;}
    IntPtr module=LoadLibraryEx(path,IntPtr.Zero,0x1100);if(module==IntPtr.Zero)return 23;nativeModule=module;
    count=Export<Count>(module,"GetDesktopCount");
    id=Export<DesktopId>(module,"GetDesktopIdByNumber");
    pinned=Export<WindowQuery>(module,"IsPinnedWindow");pinnedApp=Export<WindowQuery>(module,"IsPinnedApp");
    move=Export<MoveWindow>(module,"MoveWindowToDesktopNumber");
    currentNumber=Export<Count>(module,"GetCurrentDesktopNumber");
    changeDesktop=Export<SwitchDesktop>(module,"GoToDesktopNumber");
    createDesktop=Export<Count>(module,"CreateDesktop");
    removeDesktop=Export<DeleteDesktop>(module,"RemoveDesktop");
    return VerifyEnumeration();
  }
  static int VerifyEnumeration(){
   List<Guid> observed=DesktopIds();int total=count();
   if(total<1||total!=observed.Count)return 23;
   for(int i=0;i<total;i++)if(id(i)!=observed[i])return 23;
   int active=currentNumber();return active<0||active>=total?23:0;
  }
  static int Switch(Guid target){
   int init=EnsureReady();if(init!=0)return init;
   List<Guid> ids=DesktopIds();int to=ids.IndexOf(target);
   if(to<0||count()!=ids.Count||id(to)!=target)return 12;
   if(currentNumber()==to)return 0;
   // VDA calls the native SwitchDesktop method, not SwitchDesktopWithAnimation.
   int result=changeDesktop(to);
   for(int n=0;n<30;n++){
    int actual=currentNumber();if(actual>=0&&id(actual)==target)return 0;
    Thread.Sleep(5);
   }
   return result<0?23:26;
  }
  static List<Guid> NativeIds(){
   int before=count();if(before<1)throw new InvalidOperationException();
   List<Guid> ids=new List<Guid>();
   for(int i=0;i<before;i++){Guid value=id(i);if(value==Guid.Empty||ids.Contains(value))throw new InvalidOperationException();ids.Add(value);}
   if(count()!=before)throw new InvalidOperationException();
   return ids;
  }
  static int DesktopChange(Func<int> operation){
   int init=EnsureReady();if(init!=0)return init;
   using(Mutex mutex=new Mutex(false,@"Local\BioPager.DesktopChanges")){
    bool held=false;
    try{
     try{held=mutex.WaitOne(1000);}catch(AbandonedMutexException){held=true;}
     return held?operation():92;
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }
  static int Create(){
   int before=count();if(before<1)return 23;
   int created=createDesktop();
   for(int i=0;i<50;i++){
    if(count()>before&&created>=0&&id(created)!=Guid.Empty)return 0;
    Thread.Sleep(10);
   }
   return 28;
  }
  static int EnsureMinimum(int minimum){
   if(minimum<1||minimum>32)return 10;
   // Add only missing desktops; never shrink an existing desktop layout.
   for(int i=0;i<minimum;i++){
    int total=count();if(total<1)return 23;if(total>=minimum)return 0;
    int result=Create();if(result!=0)return result;
   }
   return count()>=minimum?0:28;
  }
  static int Remove(Guid source,Guid target){
   List<Guid> before=NativeIds();
   if(before.Count<=1)return 27;
   int from=before.IndexOf(source),to=before.IndexOf(target);
   if(from<0||to<0||source==target||before[(from+1)%before.Count]!=target)return 12;
   // Recheck identities immediately before the index-based native operation.
   if(count()!=before.Count||id(from)!=source||id(to)!=target)return 12;
   int result=removeDesktop(from,to);
   for(int i=0;i<50;i++){
    List<Guid> after=NativeIds();
    if(!after.Contains(source)&&after.Contains(target))return 0;
    if(result<0)return 29;
    Thread.Sleep(10);
   }
   return 30;
  }
  internal static void Serve(){
   SetErrorMode(0x0001|0x0002);
   try{
    using(StreamReader input=new StreamReader(Console.OpenStandardInput(),new System.Text.UTF8Encoding(false)))
    using(StreamWriter output=new StreamWriter(Console.OpenStandardOutput(),new System.Text.UTF8Encoding(false))){
    string line;
    while((line=input.ReadLine())!=null){
     int code=10;
     try{
      string[] args=line.Split(' ');Guid target,source;int minimum;
      if(line=="ping")code=EnsureReady();
      else if(line=="create")code=DesktopChange(Create);
      else if(args.Length==2&&args[0]=="ensure"&&Int32.TryParse(args[1],out minimum))code=DesktopChange(delegate{return EnsureMinimum(minimum);});
      else if(args.Length==3&&args[0]=="remove"&&Guid.TryParse(args[1],out source)&&Guid.TryParse(args[2],out target))code=DesktopChange(delegate{return Remove(source,target);});
      else if(args.Length==2&&args[0]=="switch"&&Guid.TryParse(args[1],out target))code=Switch(target);
      else if(args.Length==5&&args[0]=="move")code=Run(args);
     }catch{code=40;}
     output.WriteLine(code);output.Flush();
    }
    }
   }finally{try{if(manager!=null)Marshal.ReleaseComObject(manager);}finally{if(nativeModule!=IntPtr.Zero)FreeLibrary(nativeModule);NativePayload.Release();}}
  }
  internal static string Describe(int result){
   switch(result){
    case 11:return "The selected window closed or changed. Try dragging it again.";
    case 12:return "The desktop layout changed. Refresh and drag the window again.";
    case 20:return "This Windows build has no matching desktop API. Switching and new desktops use Windows shortcuts; app dragging and removal require Task View on this build.";
    case 21:return "The embedded helper could not be prepared. Check free disk space and write access to your local app-data folder, then restart BioPager.";
    case 22:return "The embedded helper failed verification. Replace BioPager.exe with a fresh copy of this release.";
    case 24:return "This app is shown on all desktops. Turn that option off in Win+Tab before moving it.";
    case 25:return "Windows refused to move this window. It may be protected or elevated. Try Win+Tab.";
    case 26:return "Windows did not confirm the move. Check the app in Win+Tab.";
    case 27:return "The last desktop must be kept.";
    case 28:return "Windows did not confirm the new desktop. Check Task View before trying again.";
    case 29:return "Windows refused to remove this desktop; its apps remain open. Try Task View.";
    case 30:return "Desktop removal was not confirmed. Check Task View before trying again.";
    case 31:return "Release Ctrl, Alt, Shift and Windows keys, then try again. Windows may also block keyboard input to an elevated app.";
    case 92:return "Another desktop change is in progress. Try again shortly.";
    case 91:return "The desktop helper timed out. Check Task View before retrying.";
    default:return "The move could not be confirmed on this Windows build. Use Win+Tab for this window. Code: "+result;
   }
  }
 }
}
