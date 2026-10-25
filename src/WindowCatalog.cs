// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BioPager {
 // The catalog owns its cached icons. WindowInfo borrows them until the next
 // collection; callers must not dispose them or retain them across collections.
 internal sealed class WindowCatalog : IDisposable {
  const int AppWindow=0x00040000,ToolWindow=0x00000080,NoActivate=0x08000000,ChildWindow=0x40000000;
  const int MaxCachedWindows=512,MaxDiagnosticLines=1000;
  sealed class Entry {
   internal uint Process;
   internal string Class,Name,ImagePath;
   internal Guid Desktop;
   internal Icon Icon;
   internal bool NativeIcon;
   internal DateTime NextIcon,NextMetadata;
  }
  readonly Dictionary<IntPtr,Entry> entries=new Dictionary<IntPtr,Entry>();
  bool disposed;
  internal string Diagnostics { get; private set; }

  [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr window);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref uint size);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetDriveType(string root);

  internal WindowCatalog() { Diagnostics="No window scan has completed."; }

  // These policy functions are independent of Windows so every exclusion and
  // uncertain desktop assignment has a focused regression test.
  internal static string ExclusionReason(bool visible,int style,int extendedStyle,bool hasOwner,bool ownerVisible,bool ownerSameProcess,int cloak,string className) {
   if(!visible)return "hidden";
   if((style&ChildWindow)!=0)return "child window";
   if(IsShellClass(className))return "shell or helper window";
   // Shell-cloaked windows belong to other virtual desktops and stay eligible.
   if((cloak&1)!=0)return "application-cloaked";
   if((extendedStyle&AppWindow)!=0)return null;
   if((extendedStyle&ToolWindow)!=0)return "tool window";
   if((extendedStyle&NoActivate)!=0)return "non-activating helper";
   // Some applications have an invisible owner. The visible application must
   // remain listed; only a visible representative owner suppresses its dialogs.
   if(hasOwner&&ownerVisible&&ownerSameProcess)return "dialog represented by visible owner";
   return null;
  }
  static bool IsShellClass(string name) {
   return name=="Progman"||name=="WorkerW"||name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd"||
    name=="tooltips_class32"||name=="#32768"||name=="SysShadow"||name=="MSCTFIME UI"||name=="IME";
  }
  internal static string DisplayTitle(string title,string processName,string className) {
   if(!String.IsNullOrWhiteSpace(title))return title;
   if(!String.IsNullOrWhiteSpace(processName))return processName;
   if(!String.IsNullOrWhiteSpace(className))return className;
   return "Application";
  }
  static bool Known(Guid desktop,ICollection<Guid> known) { return desktop!=Guid.Empty&&known.Contains(desktop); }
  internal static Guid SelectDesktop(Guid direct,Guid owner,Guid cached,ICollection<Guid> known,Guid current,bool currentKnown,bool onCurrent,int cloak,out bool inferred) {
   inferred=false;
   if(Known(direct,known))return direct;
   if(Known(owner,known)){inferred=true;return owner;}
   // A successful positive membership query is stronger than a stale cache.
   if(currentKnown&&onCurrent&&cloak==0&&Known(current,known)){inferred=true;return current;}
   if(Known(cached,known)) {
    bool contradicted=currentKnown&&((onCurrent&&cached!=current)||(!onCurrent&&cached==current));
    if(!contradicted){inferred=true;return cached;}
   }
   // Unknown is retained in the UI's Other apps group, never falsely pinned.
   return Guid.Empty;
  }

  internal List<WindowInfo> Collect(IDesktopManager manager,List<Guid> desktops,Guid current,uint ownPid) {
   if(disposed)throw new ObjectDisposedException("WindowCatalog");
   List<WindowInfo> result=new List<WindowInfo>();
   HashSet<IntPtr> seen=new HashSet<IntPtr>();
   StringBuilder diagnostics=new StringBuilder("BioPager window scan (UTC): "+DateTime.UtcNow.ToString("u")+Environment.NewLine);
   diagnostics.AppendLine("IN/OUT  HWND  PID  class  owner  styles  cloak  desktop  title  reason");
   Stopwatch iconBudget=Stopwatch.StartNew();
   int diagnosticLines=0,fileIconReads=0;
   // Free closed HWNDs before admitting new ones to the bounded cache.
   foreach(IntPtr handle in new List<IntPtr>(entries.Keys))if(!Native.IsWindow(handle))Remove(handle);
   Native.EnumWindows(delegate(IntPtr handle,IntPtr unused) {
    uint pid=0;string cls="",title="";IntPtr owner=IntPtr.Zero;
    int style=0,extendedStyle=0,cloak=0;Guid desktop=Guid.Empty;
    string reason=null;
    try {
     Native.GetWindowThreadProcessId(handle,out pid);
     if(pid==0||pid==ownPid)return true;
     StringBuilder value=new StringBuilder(512);Native.GetWindowText(handle,value,value.Capacity);title=value.ToString();
     value=new StringBuilder(256);Native.GetClassName(handle,value,value.Capacity);cls=value.ToString();
     style=Native.GetWindowLong(handle,-16);extendedStyle=Native.GetWindowLong(handle,-20);
     owner=Native.GetWindow(handle,4);
     uint ownerPid=0;if(owner!=IntPtr.Zero)Native.GetWindowThreadProcessId(owner,out ownerPid);
     if(Native.DwmGetWindowAttribute(handle,14,out cloak,4)!=0)cloak=0;
     bool ownerVisible=RepresentsApplication(owner);
     reason=ExclusionReason(Native.IsWindowVisible(handle),style,extendedStyle,owner!=IntPtr.Zero,ownerVisible,ownerPid==pid,cloak,cls);
     if(reason!=null)return true;

     Entry entry;bool cached=entries.TryGetValue(handle,out entry);
     if(cached&&(entry.Process!=pid||entry.Class!=cls)){Remove(handle);cached=false;entry=null;}
     if(entry==null) {
      entry=new Entry{Process=pid,Class=cls};
      // A pathological number of windows must not consume unbounded GDI icons.
      // Uncached candidates still appear with their title and live thumbnail.
      if(entries.Count<MaxCachedWindows){entries.Add(handle,entry);cached=true;}
     }
     seen.Add(handle);
     try{ReadMetadata(entry);}catch(Exception){} // Optional metadata never decides inclusion.
     title=DisplayTitle(title,entry.Name,cls);

     Guid direct=Guid.Empty,ownerDesktop=Guid.Empty;
     bool currentKnown=false,onCurrent=false,inferred;
     int desktopResult=GetDesktop(manager,handle,out direct);
     if(!Known(direct,desktops)) {
      IntPtr root=GetAncestor(handle,3);uint rootPid=0;
      if(root!=IntPtr.Zero&&root!=handle) {
       Native.GetWindowThreadProcessId(root,out rootPid);
       if(rootPid==pid)GetDesktop(manager,root,out ownerDesktop);
      }
      if(!Known(ownerDesktop,desktops)) {
       try{currentKnown=manager.IsWindowOnCurrentVirtualDesktop(handle,out onCurrent)==0;}catch(COMException){}
      }
     }
     desktop=SelectDesktop(direct,ownerDesktop,entry.Desktop,desktops,current,currentKnown,onCurrent,cloak,out inferred);
     if(desktop!=Guid.Empty)entry.Desktop=desktop;

     bool minimized=Native.IsIconic(handle);
     Rectangle bounds=WindowBounds(handle,minimized,extendedStyle);
     // Window destruction/reuse between enumeration and metadata queries is
     // normal. Do not accidentally represent the replacement using old data.
     uint afterPid;Native.GetWindowThreadProcessId(handle,out afterPid);
     if(!Native.IsWindow(handle)||afterPid!=pid){reason="closed or reused during scan";return true;}
     if(cached) {
      try{RefreshIcon(handle,entry,iconBudget,ref fileIconReads);}catch(Exception){}
     }
     result.Add(new WindowInfo{Handle=handle,Desktop=desktop,Bounds=bounds,Title=title,Icon=entry.Icon,
      Minimized=minimized,Pinned=false,Process=pid,Class=cls,Cloak=cloak,DesktopInferred=inferred});
     reason=desktop==Guid.Empty?"included; desktop unavailable (HRESULT 0x"+desktopResult.ToString("X8")+")":inferred?"included; recovered desktop":"included";
    }catch(Exception ex) {
     // One exiting or inaccessible app must never suppress the remaining apps.
     reason="scan error: "+ex.GetType().Name;
    }finally {
     if(pid!=0&&pid!=ownPid&&diagnosticLines++<MaxDiagnosticLines) {
      bool included=reason!=null&&reason.StartsWith("included",StringComparison.Ordinal);
      diagnostics.Append(included?"IN  ":"OUT ").Append("0x").Append(handle.ToInt64().ToString("X"))
       .Append(" pid=").Append(pid).Append(" class=").Append(Clean(cls)).Append(" owner=0x").Append(owner.ToInt64().ToString("X"))
       .Append(" style=0x").Append(style.ToString("X8")).Append(" ex=0x").Append(extendedStyle.ToString("X8"))
       .Append(" cloak=").Append(cloak).Append(" desktop=").Append(desktop)
       .Append(" title=\"").Append(Clean(title)).Append("\" ").AppendLine(reason??"unknown");
     }
    }
    return true;
   },IntPtr.Zero);
   foreach(IntPtr handle in new List<IntPtr>(entries.Keys))if(!seen.Contains(handle))Remove(handle);
   if(diagnosticLines>MaxDiagnosticLines)diagnostics.AppendLine("Additional diagnostic rows omitted.");
   diagnostics.AppendLine("Included applications: "+result.Count);
   Diagnostics=diagnostics.ToString();
   OrderFrontToBack(result);return result;
  }

  // Do not depend on EnumWindows callback ordering. Snapshot the actual
  // top-level Z chain; the pager reverses this for back-to-front painting.
  internal static void OrderFrontToBack(List<WindowInfo> windows) {
   Dictionary<IntPtr,int> rank=new Dictionary<IntPtr,int>();IntPtr h=GetTopWindow(IntPtr.Zero);
   for(int i=0;h!=IntPtr.Zero&&i<4096;i++){if(rank.ContainsKey(h))break;rank.Add(h,i);h=Native.GetWindow(h,2);}
   Dictionary<IntPtr,int> original=new Dictionary<IntPtr,int>();for(int i=0;i<windows.Count;i++)original[windows[i].Handle]=i;
   windows.Sort(delegate(WindowInfo a,WindowInfo b){int ar,br;if(!rank.TryGetValue(a.Handle,out ar))ar=4096+original[a.Handle];if(!rank.TryGetValue(b.Handle,out br))br=4096+original[b.Handle];return ar.CompareTo(br);});
  }

  static int GetDesktop(IDesktopManager manager,IntPtr handle,out Guid desktop) {
   desktop=Guid.Empty;
   try{return manager.GetWindowDesktopId(handle,out desktop);}catch(COMException ex){return ex.ErrorCode;}
  }
  static bool RepresentsApplication(IntPtr handle) {
   if(handle==IntPtr.Zero||!Native.IsWindowVisible(handle))return false;
   int style=Native.GetWindowLong(handle,-16),extendedStyle=Native.GetWindowLong(handle,-20),cloak;
   if((style&ChildWindow)!=0)return false;
   if((extendedStyle&AppWindow)==0&&(extendedStyle&(ToolWindow|NoActivate))!=0)return false;
   if(Native.DwmGetWindowAttribute(handle,14,out cloak,4)==0&&(cloak&1)!=0)return false;
   StringBuilder cls=new StringBuilder(256);Native.GetClassName(handle,cls,cls.Capacity);
   if(IsShellClass(cls.ToString()))return false;
   Native.RECT bounds;return Native.GetWindowRect(handle,out bounds)&&bounds.R>bounds.L&&bounds.B>bounds.T;
  }
  static Rectangle WindowBounds(IntPtr handle,bool minimized,int extendedStyle) {
   Native.RECT bounds;bool available=Native.GetWindowRect(handle,out bounds);
   if(minimized||!available||bounds.R<=bounds.L||bounds.B<=bounds.T) {
    Native.PLACEMENT placement=new Native.PLACEMENT();placement.length=Marshal.SizeOf(typeof(Native.PLACEMENT));
    if(Native.GetWindowPlacement(handle,ref placement)) {
     bounds=placement.normal;available=true;
     // WINDOWPLACEMENT uses workspace coordinates for ordinary top-level
     // windows; tool windows already use screen coordinates.
     if((extendedStyle&ToolWindow)==0) {
      Screen monitor=Screen.FromHandle(handle);
      int dx=monitor.WorkingArea.Left-monitor.Bounds.Left,dy=monitor.WorkingArea.Top-monitor.Bounds.Top;
      bounds.L+=dx;bounds.R+=dx;bounds.T+=dy;bounds.B+=dy;
     }
    }
   }
   if(available&&bounds.R>bounds.L&&bounds.B>bounds.T)return bounds.Box;
   Rectangle area=Screen.FromHandle(handle).WorkingArea;
   return new Rectangle(area.Left,area.Top,Math.Min(480,Math.Max(1,area.Width)),Math.Min(320,Math.Max(1,area.Height)));
  }
  static void ReadMetadata(Entry entry) {
   if(DateTime.UtcNow<entry.NextMetadata)return;
   entry.NextMetadata=DateTime.UtcNow.AddSeconds(30);
   IntPtr process=OpenProcess(0x1000,false,entry.Process); // QUERY_LIMITED_INFORMATION
   if(process!=IntPtr.Zero) {
    try {
     StringBuilder path=new StringBuilder(32768);uint length=(uint)path.Capacity;
     if(QueryFullProcessImageName(process,0,path,ref length)) {
      entry.ImagePath=path.ToString();entry.Name=Path.GetFileNameWithoutExtension(entry.ImagePath);
      entry.NextMetadata=DateTime.MaxValue;
     }
    }catch(ArgumentException){}finally{CloseHandle(process);}
   }
   if(String.IsNullOrEmpty(entry.Name)) {
    try{using(Process item=Process.GetProcessById((int)entry.Process))entry.Name=item.ProcessName;}catch(Exception){}
   }
  }
  static Icon CopyIcon(IntPtr handle) {
   if(handle==IntPtr.Zero)return null;
   try{return (Icon)Icon.FromHandle(handle).Clone();}catch(Exception){return null;}
  }
  static Icon ReadNativeIcon(IntPtr window,Stopwatch budget) {
   Icon icon=null;
   // A small total budget bounds the first scan when several apps are hung.
   foreach(int kind in new[]{1,0,2}) {
    if(budget.ElapsedMilliseconds>=100)break;
    IntPtr handle;
    if(Native.SendMessageTimeout(window,0x7F,new IntPtr(kind),IntPtr.Zero,0x23,12,out handle)!=IntPtr.Zero)
     icon=CopyIcon(handle);
    if(icon!=null)return icon;
   }
   foreach(int index in new[]{-14,-34}) {
    IntPtr handle=IntPtr.Size==8?Native.ClassLong64(window,index):new IntPtr(unchecked((int)Native.ClassLong32(window,index)));
    icon=CopyIcon(handle);if(icon!=null)return icon;
   }
   return null;
  }
  static bool LocalExecutable(string path) {
   if(String.IsNullOrEmpty(path)||path.StartsWith(@"\\",StringComparison.Ordinal))return false;
   try { string root=Path.GetPathRoot(path);return !String.IsNullOrEmpty(root)&&GetDriveType(root)==3; }
   catch(ArgumentException){return false;}
  }
  static void RefreshIcon(IntPtr handle,Entry entry,Stopwatch budget,ref int fileReads) {
   if(DateTime.UtcNow<entry.NextIcon)return;
   bool deferred=budget.ElapsedMilliseconds>=100;
   Icon fresh=ReadNativeIcon(handle,budget);
   bool native=fresh!=null;
   // Executable icons are a last resort; PWA/window icons take precedence and
   // are retried so a temporary startup failure does not become a grey tile.
   if(fresh==null&&entry.Icon==null&&fileReads<1&&!deferred&&LocalExecutable(entry.ImagePath)) {
    fileReads++;
    try{fresh=Icon.ExtractAssociatedIcon(entry.ImagePath);}catch(Exception){}
   }
   if(fresh!=null) {
    Icon previous=entry.Icon;entry.Icon=fresh;entry.NativeIcon=native;
    if(previous!=null)previous.Dispose();
   }
   entry.NextIcon=DateTime.UtcNow.AddMilliseconds(deferred?750:entry.Icon==null?2000:entry.NativeIcon?15000:4000);
  }
  static string Clean(string text) {
   if(text==null)return "";
   text=text.Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
   return text.Length>180?text.Substring(0,180):text;
  }
  void Remove(IntPtr handle) {
   Entry entry;if(!entries.TryGetValue(handle,out entry))return;
   entries.Remove(handle);if(entry.Icon!=null)entry.Icon.Dispose();
  }
  public void Dispose() {
   if(disposed)return;disposed=true;
   foreach(IntPtr handle in new List<IntPtr>(entries.Keys))Remove(handle);
  }
 }
}
