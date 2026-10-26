// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// DWM previews and a small, memory-only cache of unobscured foreground windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BioPager {
 internal sealed class WindowPreviews : IDisposable {
  internal const int MaximumCachedFrames=64;
  internal const int MaximumEntries=256;
  const long MaximumSamplePixels=16000000;
  const int GwlStyle=-16,GwlExStyle=-20;
  const int WsChild=0x40000000,WsExLayered=0x00080000,WsExTransparent=0x00000020;
  const uint TnpDestination=1,TnpOpacity=4,TnpVisible=8,TnpClientOnly=16;
  readonly Dictionary<IntPtr,Entry> entries=new Dictionary<IntPtr,Entry>();
  readonly uint ownProcessId;
  IntPtr destination;
  DateTime nextCapture=DateTime.MinValue;
  bool disposed;

  sealed class Entry {
   internal IntPtr Window,Thumbnail;
   internal readonly List<IntPtr> Pieces=new List<IntPtr>();
   internal uint Process;
   internal string Class;
   internal Bitmap Frame;
   internal Rectangle Target;
   internal DateTime LastUsed,Captured,RetryAfter;
   internal bool Seen,LiveSeen,Visible,Restricted;
  }
  [StructLayout(LayoutKind.Sequential)]
  internal struct ThumbnailProperties {
   internal uint Flags;
   internal Native.RECT Destination,Source;
   internal byte Opacity;
   [MarshalAs(UnmanagedType.Bool)] internal bool Visible;
   [MarshalAs(UnmanagedType.Bool)] internal bool ClientOnly;
  }
  [StructLayout(LayoutKind.Sequential)] struct NativeSize { internal int Width,Height; }
  [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr target,IntPtr source,out IntPtr thumbnail);
  [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
  [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out NativeSize size);
  [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Native.POINT point);
  [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr window);
  [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr window,out uint affinity);
  [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window,IntPtr region);
  [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);

  internal WindowPreviews(IntPtr destinationWindow) {
   destination=destinationWindow;
   using(Process process=Process.GetCurrentProcess())ownProcessId=(uint)process.Id;
  }
  internal static int ThumbnailPropertiesSize {get{return Marshal.SizeOf(typeof(ThumbnailProperties));}}
  internal int EntryCount {get{return entries.Count;}}
  internal int RegistrationCount {
   get{int count=0;foreach(Entry entry in entries.Values){if(entry.Thumbnail!=IntPtr.Zero)count++;count+=entry.Pieces.Count;}return count;}
  }
  internal int CachedFrameCount {
   get{int count=0;foreach(Entry entry in entries.Values)if(entry.Frame!=null)count++;return count;}
  }

  internal void ResetDestination(IntPtr destinationWindow) {
   if(disposed)return;
   foreach(Entry entry in entries.Values){Unregister(entry);entry.RetryAfter=DateTime.MinValue;}
   destination=destinationWindow;
  }
  internal void BeginFrame() {
   if(disposed)return;
   foreach(Entry entry in entries.Values){entry.Seen=false;entry.LiveSeen=false;}
  }

  // True means a DWM relationship was configured, not that fresh pixels were
  // observed. Windows may retain an older surface for a minimized/hidden app.
  internal bool Show(IntPtr window,uint process,string className,Rectangle target,bool minimized,int cloak) {
   return Show(window,process,className,target,minimized,cloak,null);
  }
  // Pieces are disjoint destination rectangles with explicit source crops. DWM
  // is composed above GDI, so its own destination must exclude every front app.
  internal bool Show(IntPtr window,uint process,string className,Rectangle target,bool minimized,int cloak,List<Rectangle> pieces) {
   return Show(window,process,className,target,minimized,cloak,pieces,Rectangle.Empty);
  }
  internal bool Show(IntPtr window,uint process,string className,Rectangle target,bool minimized,int cloak,List<Rectangle> pieces,Rectangle badge) {
   if(disposed)return false;
   Entry entry=FindOrCreate(window,process,className);
   if(entry==null)return false;
   entry.Seen=true;
   if(!AllowContent(entry)){Unregister(entry);return false;}
   if(pieces!=null&&pieces.Count==0){Unregister(entry);return false;}
   if((minimized||cloak!=0)&&entry.Frame!=null){Unregister(entry);return false;}
   if(destination==IntPtr.Zero||!Native.IsWindow(destination)||target.Width<2||target.Height<2||(cloak&1)!=0) {
    Unregister(entry);return false;
   }
   if((Native.GetWindowLong(window,GwlStyle)&WsChild)!=0){Unregister(entry);return false;}
   if(DateTime.UtcNow<entry.RetryAfter)return false;
   try {
    if(entry.Thumbnail==IntPtr.Zero) {
     IntPtr thumbnail;
     if(DwmRegisterThumbnail(destination,window,out thumbnail)!=0||thumbnail==IntPtr.Zero) {
      entry.RetryAfter=DateTime.UtcNow.AddSeconds(3);return false;
     }
     entry.Thumbnail=thumbnail;
    }
    NativeSize size;
    if(DwmQueryThumbnailSourceSize(entry.Thumbnail,out size)!=0||size.Width<=0||size.Height<=0) {
     Failed(entry);return false;
    }
    bool clientOnly=pieces==null;
    Native.RECT client;
    if(clientOnly&&!minimized&&Native.GetClientRect(window,out client)&&client.R>client.L&&client.B>client.T) {
     size.Width=client.R-client.L;size.Height=client.B-client.T;
    }
    Rectangle fitted=clientOnly?Fit(new Size(size.Width,size.Height),target):target;
    // Preserve the normal fitted client image in grid mode and full-window
    // geometry in spatial mode. Only subtract the badge after choosing that
    // mapping: changing the target would squeeze or shift the source image.
    if(!badge.IsEmpty) {
     List<Rectangle> uncovered=new List<Rectangle>();
     if(pieces==null)uncovered.AddRange(SpatialLayout.Uncovered(fitted,fitted,new[]{badge}));
     else foreach(Rectangle part in pieces)uncovered.AddRange(SpatialLayout.Uncovered(part,part,new[]{badge}));
     pieces=uncovered;
    }
    if(pieces!=null) {
     if(pieces.Count==0||pieces.Count>32){Unregister(entry);return false;}
     ThumbnailProperties hidden=new ThumbnailProperties();hidden.Flags=TnpVisible;hidden.Visible=false;
     if(DwmUpdateThumbnailProperties(entry.Thumbnail,ref hidden)!=0){Failed(entry);return false;}
     entry.Visible=false;
     while(entry.Pieces.Count>pieces.Count){int last=entry.Pieces.Count-1;DwmUnregisterThumbnail(entry.Pieces[last]);entry.Pieces.RemoveAt(last);}
     for(int i=0;i<pieces.Count;i++) {
      if(i>=entry.Pieces.Count){IntPtr part;if(DwmRegisterThumbnail(destination,window,out part)!=0||part==IntPtr.Zero){Failed(entry);return false;}entry.Pieces.Add(part);}
      Rectangle crop=SpatialLayout.SourceCrop(pieces[i],fitted,new Size(size.Width,size.Height));
      ThumbnailProperties fragment=new ThumbnailProperties();
      fragment.Flags=TnpDestination|2|TnpOpacity|TnpVisible|TnpClientOnly;
      fragment.Destination=ToNative(pieces[i]);fragment.Source=ToNative(crop);
      fragment.Opacity=255;fragment.Visible=true;fragment.ClientOnly=clientOnly;
      if(DwmUpdateThumbnailProperties(entry.Pieces[i],ref fragment)!=0){Failed(entry);return false;}
     }
     entry.LiveSeen=true;return true;
    }
    ClearPieces(entry);
    if(fitted.Width<2||fitted.Height<2){Unregister(entry);return false;}
    if(!entry.Visible||entry.Target!=fitted) {
     ThumbnailProperties properties=new ThumbnailProperties();
     properties.Flags=TnpDestination|TnpOpacity|TnpVisible|TnpClientOnly;
     properties.Destination=new Native.RECT{L=fitted.Left,T=fitted.Top,R=fitted.Right,B=fitted.Bottom};
     properties.Source=new Native.RECT();
     properties.Opacity=255;properties.Visible=true;properties.ClientOnly=true;
     if(DwmUpdateThumbnailProperties(entry.Thumbnail,ref properties)!=0){Failed(entry);return false;}
     entry.Target=fitted;entry.Visible=true;
    }
    entry.LiveSeen=true;return true;
   }catch {Failed(entry);return false;}
  }

  internal bool TryDrawCached(Graphics graphics,IntPtr window,uint process,string className,Rectangle target) {
   if(disposed||graphics==null)return false;
   Entry entry=FindOrCreate(window,process,className);
   if(entry==null)return false;
   entry.Seen=true;
   if(!AllowContent(entry)||entry.Frame==null||target.Width<1||target.Height<1)return false;
   Rectangle fitted=Fit(entry.Frame.Size,target);
   if(fitted.Width<1||fitted.Height<1)return false;
   GraphicsState state=graphics.Save();
   try {
    graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;
    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
    graphics.DrawImage(entry.Frame,fitted);
    return true;
   }catch {return false;}finally{graphics.Restore(state);}
  }

  internal void EndFrame() {
   if(disposed)return;
   List<IntPtr> remove=new List<IntPtr>();
   foreach(KeyValuePair<IntPtr,Entry> item in entries) {
    Entry entry=item.Value;
    if(!HasIdentity(entry.Window,entry.Process,entry.Class)){remove.Add(item.Key);continue;}
    AllowContent(entry);
    if(!entry.LiveSeen)Unregister(entry);
    if(!entry.Seen) {
     // Spatial mode and clipped windows can still need their last sampled
     // picture later. Keep a bounded cache even without a live relationship.
     if(entry.Frame==null)remove.Add(item.Key);
    }
   }
   foreach(IntPtr window in remove)Remove(window);
  }

  // Call from the refresh timer, never from OnPaint. This deliberately avoids
  // PrintWindow, which can block indefinitely inside the other application.
  // Screen sampling is conservative: it is skipped whenever a whole client
  // rectangle cannot be shown to be unobscured before AND after the copy.
  internal bool CaptureForeground(IntPtr ownWindow) {
   if(disposed||DateTime.UtcNow<nextCapture)return false;
   nextCapture=DateTime.UtcNow.AddSeconds(1);
   IntPtr window=Native.GetForegroundWindow();
   if(window==IntPtr.Zero||window==ownWindow)return false;
   uint process;Native.GetWindowThreadProcessId(window,out process);
   if(process==0||process==ownProcessId)return false;
   Entry entry;
   if(!entries.TryGetValue(window,out entry)||entry.Process!=process)return false;
   if(!HasIdentity(window,process,entry.Class)){Remove(window);return false;}
   if(IsShell(entry.Class)||!AllowContent(entry))return false;
   Rectangle bounds;
   if(!CanSample(entry,out bounds))return false;
   Bitmap sampled=null;
   try {
    using(Bitmap full=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb)) {
     using(Graphics capture=Graphics.FromImage(full))
      capture.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
     Rectangle after;
     if(!CanSample(entry,out after)||after!=bounds)return false;
     Rectangle small=Fit(full.Size,new Rectangle(0,0,320,180));
     if(small.Width<1||small.Height<1)return false;
     sampled=new Bitmap(small.Width,small.Height,PixelFormat.Format24bppRgb);
     using(Graphics draw=Graphics.FromImage(sampled)) {
      draw.InterpolationMode=InterpolationMode.HighQualityBilinear;
      draw.PixelOffsetMode=PixelOffsetMode.HighQuality;
      draw.DrawImage(full,new Rectangle(0,0,sampled.Width,sampled.Height));
     }
    }
    Rectangle finalBounds;
    if(!CanSample(entry,out finalBounds)||finalBounds!=bounds)return false;
    TrimCachedFrames(entry);
    Bitmap previous=entry.Frame;
    entry.Frame=sampled;sampled=null;
    entry.Captured=DateTime.UtcNow;entry.LastUsed=entry.Captured;
    if(previous!=null)previous.Dispose();
    return true;
   }catch {return false;}finally{if(sampled!=null)sampled.Dispose();}
  }

  bool CanSample(Entry entry,out Rectangle bounds) {
   bounds=Rectangle.Empty;
   if(Native.GetForegroundWindow()!=entry.Window||!HasIdentity(entry.Window,entry.Process,entry.Class)||
      !Native.IsWindowVisible(entry.Window)||Native.IsIconic(entry.Window)||!AllowContent(entry))return false;
   int cloak;
   if(Native.DwmGetWindowAttribute(entry.Window,14,out cloak,4)!=0||cloak!=0)return false;
   int extended=Native.GetWindowLong(entry.Window,GwlExStyle);
   // A rectangular screen crop cannot faithfully isolate a transparent or
   // shaped source window from pixels belonging to windows behind it.
   if((extended&(WsExLayered|WsExTransparent))!=0||HasCustomRegion(entry.Window))return false;
   if(!ClientBounds(entry.Window,out bounds)||bounds.Width<2||bounds.Height<2||
      (long)bounds.Width*bounds.Height>MaximumSamplePixels)return false;
   if(!CoveredByScreens(bounds)||!Unobscured(entry.Window,bounds))return false;
   return true;
  }
  static bool ClientBounds(IntPtr window,out Rectangle bounds) {
   bounds=Rectangle.Empty;Native.RECT client;
   if(!Native.GetClientRect(window,out client)||client.R<=client.L||client.B<=client.T)return false;
   Native.POINT start=new Native.POINT{X=client.L,Y=client.T};
   Native.POINT end=new Native.POINT{X=client.R,Y=client.B};
   if(!ClientToScreen(window,ref start)||!ClientToScreen(window,ref end)||end.X<=start.X||end.Y<=start.Y)return false;
   bounds=Rectangle.FromLTRB(start.X,start.Y,end.X,end.Y);return true;
  }
  static bool HasCustomRegion(IntPtr window) {
   IntPtr region=CreateRectRgn(0,0,0,0);
   if(region==IntPtr.Zero)return true;
   try{return GetWindowRgn(window,region)!=0;}finally{DeleteObject(region);}
  }
  static bool CoveredByScreens(Rectangle bounds) {
   List<Rectangle> remaining=new List<Rectangle>();remaining.Add(bounds);
   foreach(Screen screen in Screen.AllScreens) {
    List<Rectangle> next=new List<Rectangle>();
    foreach(Rectangle part in remaining) {
     Rectangle overlap=Rectangle.Intersect(part,screen.Bounds);
     if(overlap.IsEmpty){next.Add(part);continue;}
     if(overlap.Top>part.Top)next.Add(Rectangle.FromLTRB(part.Left,part.Top,part.Right,overlap.Top));
     if(overlap.Bottom<part.Bottom)next.Add(Rectangle.FromLTRB(part.Left,overlap.Bottom,part.Right,part.Bottom));
     if(overlap.Left>part.Left)next.Add(Rectangle.FromLTRB(part.Left,overlap.Top,overlap.Left,overlap.Bottom));
     if(overlap.Right<part.Right)next.Add(Rectangle.FromLTRB(overlap.Right,overlap.Top,part.Right,overlap.Bottom));
    }
    remaining=next;if(remaining.Count==0)return true;
   }
   return false;
  }
  static bool Unobscured(IntPtr source,Rectangle bounds) {
   HashSet<IntPtr> visited=new HashSet<IntPtr>();
   IntPtr window=GetTopWindow(IntPtr.Zero);
   for(int i=0;window!=IntPtr.Zero&&i<4096;i++) {
    if(window==source)return true;
    if(!visited.Add(window))return false;
    if(Native.IsWindowVisible(window)&&!Native.IsIconic(window)) {
     int cloak;
     bool cloaked=Native.DwmGetWindowAttribute(window,14,out cloak,4)==0&&cloak!=0;
     if(!cloaked) {
      Native.RECT other;
      if(!Native.GetWindowRect(window,out other))return false;
      if(other.R>other.L&&other.B>other.T&&bounds.IntersectsWith(other.Box))return false;
     }
    }
    window=Native.GetWindow(window,2); // GW_HWNDNEXT: walk toward the source.
   }
   return false;
  }

  Entry FindOrCreate(IntPtr window,uint process,string className) {
   if(window==IntPtr.Zero||process==0||String.IsNullOrEmpty(className))return null;
   Entry entry;
   if(entries.TryGetValue(window,out entry)) {
    if(entry.Process!=process||entry.Class!=className||!HasIdentity(window,process,className)) {
     Remove(window);entry=null;
    }
   }
   if(entry==null) {
    if(!HasIdentity(window,process,className))return null;
    if(entries.Count>=MaximumEntries) {
     Entry oldest=null;
     foreach(Entry candidate in entries.Values)
      if(!candidate.Seen&&(oldest==null||candidate.LastUsed<oldest.LastUsed))oldest=candidate;
     if(oldest==null)return null; // Caller still paints its icon/title fallback.
     Remove(oldest.Window);
    }
    entry=new Entry{Window=window,Process=process,Class=className};entries.Add(window,entry);
   }
   entry.LastUsed=DateTime.UtcNow;return entry;
  }
  static bool HasIdentity(IntPtr window,uint process,string className) {
   if(!Native.IsWindow(window))return false;
   uint actual;Native.GetWindowThreadProcessId(window,out actual);
   if(actual!=process)return false;
   StringBuilder name=new StringBuilder(256);
   return Native.GetClassName(window,name,name.Capacity)>0&&name.ToString()==className;
  }
  static bool IsShell(string className) {
   return className=="Progman"||className=="WorkerW"||className=="Shell_TrayWnd"||
          className=="Shell_SecondaryTrayWnd"||className=="NotifyIconOverflowWindow"||className=="#32768";
  }
  static bool AllowContent(Entry entry) {
   uint affinity;
   // Once Windows reports protection, an inconclusive later query must not
   // silently make this same window's content eligible for capture again.
   if(GetWindowDisplayAffinity(entry.Window,out affinity))entry.Restricted=affinity!=0;
   if(entry.Restricted) {
    if(entry.Frame!=null){entry.Frame.Dispose();entry.Frame=null;}
    Unregister(entry);return false;
   }
   return true;
  }
  void TrimCachedFrames(Entry replacement) {
   int count=CachedFrameCount-(replacement.Frame==null?0:1);
   while(count>=MaximumCachedFrames) {
    Entry oldest=null;
    foreach(Entry candidate in entries.Values)
     if(candidate!=replacement&&candidate.Frame!=null&&(oldest==null||candidate.Captured<oldest.Captured))oldest=candidate;
    if(oldest==null)break;
    oldest.Frame.Dispose();oldest.Frame=null;count--;
   }
  }
  static void Failed(Entry entry) {Unregister(entry);entry.RetryAfter=DateTime.UtcNow.AddSeconds(3);}
  static Native.RECT ToNative(Rectangle r){return new Native.RECT{L=r.Left,T=r.Top,R=r.Right,B=r.Bottom};}
  static void ClearPieces(Entry entry){foreach(IntPtr part in entry.Pieces)try{DwmUnregisterThumbnail(part);}catch{}entry.Pieces.Clear();}
  static void Unregister(Entry entry) {
   ClearPieces(entry);
   IntPtr thumbnail=entry.Thumbnail;entry.Thumbnail=IntPtr.Zero;entry.Visible=false;entry.Target=Rectangle.Empty;
   if(thumbnail!=IntPtr.Zero)try{DwmUnregisterThumbnail(thumbnail);}catch{}
  }
  void Remove(IntPtr window) {
   Entry entry;if(!entries.TryGetValue(window,out entry))return;
   entries.Remove(window);Unregister(entry);if(entry.Frame!=null){entry.Frame.Dispose();entry.Frame=null;}
  }
  internal static Rectangle Fit(Size source,Rectangle target) {
   if(source.Width<=0||source.Height<=0||target.Width<=0||target.Height<=0)return Rectangle.Empty;
   double scale=Math.Min((double)target.Width/source.Width,(double)target.Height/source.Height);
   int width=Math.Max(1,Math.Min(target.Width,(int)Math.Round(source.Width*scale)));
   int height=Math.Max(1,Math.Min(target.Height,(int)Math.Round(source.Height*scale)));
   return new Rectangle(target.Left+(target.Width-width)/2,target.Top+(target.Height-height)/2,width,height);
  }
  public void Dispose() {
   if(disposed)return;disposed=true;
   foreach(Entry entry in entries.Values){Unregister(entry);if(entry.Frame!=null)entry.Frame.Dispose();}
   entries.Clear();destination=IntPtr.Zero;
  }
 }
}
