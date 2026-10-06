// Copyright (c) 2026 Jong Bhak. Licensed under BioLicense 1.0; see LICENSE.
// BioPager 0.4.8. Created by Jong Bhak. Inspired by Vern by One Guy Coding.
// Build with the .NET Framework C# compiler shipped with Windows 10/11.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BioPager {
 [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IDesktopManager {
  [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr window, [MarshalAs(UnmanagedType.Bool)] out bool current);
  [PreserveSig] int GetWindowDesktopId(IntPtr window, out Guid desktop);
  [PreserveSig] int MoveWindowToDesktop(IntPtr window, ref Guid desktop);
 }
 static class Native {
  internal delegate bool EnumProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int L,T,R,B; public Rectangle Box { get { return Rectangle.FromLTRB(L,T,R,B); } } }
  [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] internal struct PLACEMENT { public int length,flags,show; public POINT min,max; public RECT normal; }
  [StructLayout(LayoutKind.Sequential)] internal struct KEYBD { public ushort key,scan; public uint flags,time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] internal struct MOUSE { public int x,y; public uint data,flags,time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] internal struct UNION { [FieldOffset(0)] public KEYBD keyboard; [FieldOffset(0)] public MOUSE mouse; }
  [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public UNION data; }
  [StructLayout(LayoutKind.Sequential)] internal struct APPBARDATA { public uint size; public IntPtr window; public uint callback,edge; public RECT rect; public IntPtr parameter; }
  [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message,ref APPBARDATA data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string className,string title);
  [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetParent(IntPtr child,IntPtr parent);
  [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr child);
  [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window,out RECT rect);
  [DllImport("user32.dll")] internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
  [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
  [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr h, ref PLACEMENT p);
  [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr h,uint cmd);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(IntPtr h,int n);
  [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll",EntryPoint="GetClassLongPtrW")] internal static extern IntPtr ClassLong64(IntPtr h,int index);
  [DllImport("user32.dll",EntryPoint="GetClassLongW")] internal static extern uint ClassLong32(IntPtr h,int index);
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr h,int cmd);
  [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint count,INPUT[] input,int size);
  [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
  [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr h,int a,out int value,int size);
  internal static bool Shortcut(ushort key,bool ctrl) {
   // Do not release modifiers the user is physically holding.
   foreach(int k in new int[]{0x10,0x11,0x12,0x5B,0x5C}) if((GetAsyncKeyState(k)&0x8000)!=0) return false;
   List<INPUT> input=new List<INPUT>();
   input.Add(Key(0x5B,false)); if(ctrl) input.Add(Key(0x11,false));
   input.Add(Key(key,false)); input.Add(Key(key,true));
   if(ctrl) input.Add(Key(0x11,true)); input.Add(Key(0x5B,true));
   INPUT[] a=input.ToArray();
   return SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)))==a.Length;
  }
  static INPUT Key(ushort k,bool up) { INPUT i=new INPUT(); i.type=1; i.data.keyboard.key=k; i.data.keyboard.flags=(up?2u:0u)|((k==0x25||k==0x27||k==0x5B)?1u:0u); return i; }
 }
 sealed class WindowInfo { internal IntPtr Handle; internal Guid Desktop; internal Rectangle Bounds; internal string Title; internal Icon Icon; internal bool Minimized,Pinned; }
 sealed class CachedIcon { internal Icon Icon; internal uint Process; internal string Class; }
 sealed class Hit { internal Rectangle Box; internal int Desktop; internal IntPtr Window; }
 sealed class Pager : Form {
  const string RegRoot=@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";
  readonly IDesktopManager manager;
  readonly uint ownProcessId=(uint)Process.GetCurrentProcess().Id;
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  readonly System.Windows.Forms.Timer fastTimer=new System.Windows.Forms.Timer();
  readonly WorkerClient worker=new WorkerClient(Application.ExecutablePath);
  readonly DesktopFallback fallback=new DesktopFallback(DesktopFallback.ReadIds,DesktopFallback.ReadCurrent,delegate(ushort key){return Native.Shortcut(key,true);});
  readonly Dictionary<Guid,string> names=new Dictionary<Guid,string>();
  readonly ToolTip tooltip=new ToolTip();
  readonly NotifyIcon tray=new NotifyIcon();
  readonly Icon brandIcon;
  readonly Dictionary<IntPtr,CachedIcon> icons=new Dictionary<IntPtr,CachedIcon>();
  readonly List<Guid> desktops=new List<Guid>();
  readonly List<WindowInfo> windows=new List<WindowInfo>();
  readonly List<Hit> hits=new List<Hit>();
  readonly string settings;
  readonly bool loadLegacySettings;
  readonly ProfileSetup profileSetup;
  bool desktopOperation;
  Guid menuDesktop;
  ToolStripMenuItem newDesktopItem,removeDesktopItem;
  DateTime nextRaise=DateTime.MinValue;
  Guid current=Guid.Empty;
  bool busy,dragging,customPosition,overTaskbar;
  bool showTitles=true,iconGrid=false,docked,appbarRegistered,positioning,appDragging;
  uint appbarMessage,taskbarCreated;
  IntPtr dragWindow=IntPtr.Zero;
  Guid dragSource=Guid.Empty,dropDesktop=Guid.Empty;
  uint dragProcess;
  Point pressPoint,dragPoint;
  int downClicks;
  bool gestureActive;
  Guid pressDesktop;
  ToolStripMenuItem titleItem,gridItem,dockItem;
  ToolStripMenuItem embedItem;
  IntPtr taskbarParent;
  bool embedded,changingEmbedding,switchPumping;
  int embedOffset=200,embedDragOffset;
  Guid pendingSwitch=Guid.Empty,ackDesktop=Guid.Empty;
  IntPtr pendingFocus;
  DateTime ackUntil=DateTime.MinValue;
  float floatingScale=1;
  Color backgroundColor=Color.FromArgb(55,9,12);
  readonly System.Windows.Forms.Timer cancelTimer=new System.Windows.Forms.Timer();
  Point dragStart,formStart;
  int tileWidth=132,tileHeight=74,lastHover=-1;
  string status="",hoverText="";
  DateTime statusUntil=DateTime.MinValue;
  Rectangle screenBounds;
  int ScaleValue(int value) { return (int)Math.Round(value*DeviceScale); }
  float DeviceScale=1;
  Control InputSurface {get{return this;}}
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ExStyle|=0x08000000|0x80; return p; } }
  internal Pager():this(null) {}
  internal Pager(string settingsFile) {
   loadLegacySettings=settingsFile==null;
   settings=settingsFile??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BioPager","settings.txt");
   bool existingSettings=File.Exists(settings)||(loadLegacySettings&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VernStylePager","settings.txt")));
   try{profileSetup=new ProfileSetup(Path.GetDirectoryName(settings),existingSettings);}catch(Exception ex){Debug.WriteLine(ex);}
   manager=(IDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
   using(Stream iconStream=typeof(Pager).Assembly.GetManifestResourceStream("BioPager.App.ico"))brandIcon=new Icon(iconStream);
   Icon=brandIcon;
   Text="BioPager"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
   StartPosition=FormStartPosition.Manual; BackColor=Color.FromArgb(28,17,20); DoubleBuffered=true;
   using(Graphics g=CreateGraphics()) DeviceScale=g.DpiX/96f;floatingScale=DeviceScale;
   Font=new Font("Segoe UI",9f); LoadSettings();ApplyBackgroundColor();
   appbarMessage=Native.RegisterWindowMessage("BioPager.Appbar.02");
   taskbarCreated=Native.RegisterWindowMessage("TaskbarCreated");
   ContextMenuStrip menu=new ContextMenuStrip();
   newDesktopItem=new ToolStripMenuItem("New desktop");newDesktopItem.Click+=async delegate {await CreateDesktop();};menu.Items.Add(newDesktopItem);
   removeDesktopItem=new ToolStripMenuItem("Remove desktop (move apps to next)");removeDesktopItem.Click+=async delegate {await RemoveDesktop(menuDesktop);};menu.Items.Add(removeDesktopItem);
   menu.Opening+=delegate {
    RefreshState();menuDesktop=current;
    if(menu.SourceControl==this){Hit hit=HitAt(PointToClient(System.Windows.Forms.Cursor.Position));if(hit!=null&&hit.Desktop>=0&&hit.Desktop<desktops.Count)menuDesktop=desktops[hit.Desktop];}
    int from=desktops.IndexOf(menuDesktop);int to=from>=0?(from+1)%Math.Max(1,desktops.Count):-1;
    newDesktopItem.Enabled=!desktopOperation&&!busy;
    removeDesktopItem.Enabled=desktops.Count>1&&from>=0&&!desktopOperation&&!busy;
    removeDesktopItem.Text=from>=0&&desktops.Count>1?"Remove "+DesktopName(from)+" (move apps to "+DesktopName(to)+")":"Remove desktop (keep at least one)";
   };
   menu.Items.Add("Open Windows Task View",null,delegate { Native.Shortcut(0x09,false); });
   menu.Items.Add(new ToolStripSeparator());
   titleItem=new ToolStripMenuItem("Show desktop titles");titleItem.Checked=showTitles;titleItem.CheckOnClick=true;
   titleItem.CheckedChanged+=delegate {showTitles=titleItem.Checked;LayoutBar();Invalidate();SaveSettings();};menu.Items.Add(titleItem);
   gridItem=new ToolStripMenuItem("Arrange app icons in a grid (easier to drag)");gridItem.Checked=iconGrid;gridItem.CheckOnClick=true;
   gridItem.CheckedChanged+=delegate {iconGrid=gridItem.Checked;Invalidate();SaveSettings();};menu.Items.Add(gridItem);
   menu.Items.Add("Background color...",null,delegate {ChooseBackgroundColor();});
   menu.Items.Add("Restore default background",null,delegate {backgroundColor=Color.FromArgb(55,9,12);ApplyBackgroundColor();SaveSettings();});
   embedItem=new ToolStripMenuItem("Integrate with taskbar");embedItem.Checked=embedded;embedItem.CheckOnClick=true;
   embedItem.CheckedChanged+=delegate {SetEmbedded(embedItem.Checked);};menu.Items.Add(embedItem);
   menu.Items.Add("BioPager position within taskbar...",null,delegate {ConfigureEmbedOffset();});
   ToolStripMenuItem taskbarLocation=new ToolStripMenuItem("Windows taskbar location");
   ToolStripMenuItem topTaskbar=new ToolStripMenuItem("Top");
   ToolStripMenuItem bottomTaskbar=new ToolStripMenuItem("Bottom");
   topTaskbar.Click+=async delegate {taskbarLocation.Enabled=false;try{await TaskbarPosition.SetAsync(this,true);LayoutBar();}finally{taskbarLocation.Enabled=true;}};
   bottomTaskbar.Click+=async delegate {taskbarLocation.Enabled=false;try{await TaskbarPosition.SetAsync(this,false);LayoutBar();}finally{taskbarLocation.Enabled=true;}};
   taskbarLocation.DropDownItems.Add(topTaskbar);taskbarLocation.DropDownItems.Add(bottomTaskbar);
   taskbarLocation.DropDownItems.Add("Windows taskbar settings...",null,delegate {Process.Start("ms-settings:taskbar");});
   menu.Items.Add(taskbarLocation);
   dockItem=new ToolStripMenuItem("Dock beside taskbar (reserve screen space)");dockItem.Checked=docked;dockItem.CheckOnClick=true;
   dockItem.CheckedChanged+=delegate {SetDocked(dockItem.Checked);};menu.Items.Add(dockItem);
   menu.Items.Add("Float beside taskbar",null,delegate { SetEmbedded(false);SetDocked(false);customPosition=false;overTaskbar=false;LayoutBar();SaveSettings(); });
   menu.Items.Add("Place over taskbar (floating overlay)",null,delegate { SetEmbedded(false);SetDocked(false);customPosition=false;overTaskbar=true;LayoutBar();SaveSettings(); });
   menu.Items.Add("Smaller tiles",null,delegate { tileWidth=Math.Max(80,tileWidth-16);tileHeight=Math.Max(48,tileHeight-8);LayoutBar();SaveSettings(); });
   menu.Items.Add("Larger tiles",null,delegate { tileWidth=Math.Min(240,tileWidth+16);tileHeight=Math.Min(132,tileHeight+8);LayoutBar();SaveSettings(); });
   menu.Items.Add("Reset position and size",null,delegate {SetEmbedded(false);SetDocked(false);tileWidth=132;tileHeight=74;embedOffset=200;customPosition=false;overTaskbar=false;LayoutBar();SaveSettings();});
   menu.Items.Add(new ToolStripSeparator());
   menu.Items.Add("Help",null,delegate { ShowHelp(); });
   menu.Items.Add("Exit",null,delegate { Close(); });
   ContextMenuStrip=menu; tray.ContextMenuStrip=menu;tray.Icon=brandIcon;tray.Text="BioPager";tray.Visible=true;
   tray.DoubleClick+=delegate {SetEmbedded(false);customPosition=false;LayoutBar();Show();};
   timer.Interval=750; timer.Tick+=delegate { RefreshState(); };
   fastTimer.Interval=60;fastTimer.Tick+=delegate {FastTick();};
   Shown+=async delegate {RefreshState();if(embedded)SetEmbedded(true);else if(docked)RegisterAppbar();LayoutBar();timer.Start();fastTimer.Start();worker.Warm();await InitializeDesktops();};
   MouseDown+=OnDown; MouseMove+=OnMove; MouseUp+=OnUp;
   MouseCaptureChanged+=delegate {if(!Capture&&(dragging||dragWindow!=IntPtr.Zero)){CancelDrag();}};
   cancelTimer.Interval=40;cancelTimer.Tick+=delegate {if((Native.GetAsyncKeyState(0x1B)&0x8000)!=0)CancelDrag();};
   MouseLeave+=delegate {tooltip.SetToolTip(InputSurface,"");hoverText="";lastHover=-1;Invalidate();};
  }
  void ShowHelp() {
   // Decode only when Help opens; the pager's refresh path does no logo work.
   using(Form dialog=new Form())
   using(Font helpFont=new Font("Segoe UI",9f))
   using(Stream stream=typeof(Pager).Assembly.GetManifestResourceStream("BioPager.Logo.jpg"))
   using(Image logo=Image.FromStream(stream)) {
    dialog.Text="BioPager Help";dialog.Font=helpFont;
    dialog.AutoScaleDimensions=new SizeF(96f,96f);dialog.AutoScaleMode=AutoScaleMode.Dpi;
    dialog.ClientSize=new Size(640,680);dialog.MinimumSize=new Size(380,420);
    dialog.StartPosition=FormStartPosition.CenterScreen;dialog.ShowInTaskbar=false;
    dialog.MinimizeBox=false;dialog.MaximizeBox=false;dialog.TopMost=true;
    TableLayoutPanel layout=new TableLayoutPanel();layout.Dock=DockStyle.Fill;
    layout.Padding=new Padding(12);layout.ColumnCount=1;layout.RowCount=3;
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,32));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,68));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
    PictureBox picture=new PictureBox();picture.Dock=DockStyle.Fill;
    picture.BackColor=Color.Black;picture.SizeMode=PictureBoxSizeMode.Zoom;
    picture.Image=logo;picture.AccessibleName="BioPager tiger-whale logo";
    TextBox help=new TextBox();help.Dock=DockStyle.Fill;help.Multiline=true;
    help.ReadOnly=true;help.ScrollBars=ScrollBars.Vertical;help.BorderStyle=BorderStyle.None;
    help.BackColor=SystemColors.Control;help.ForeColor=SystemColors.ControlText;
    help.Text="BioPager 0.4.8\nCreated by Jong Bhak\nBioLicense: free for all, including companies and AIs.\n\nInspired by Vern, created by One Guy Coding.\nIndependent implementation; no Vern code or assets used.\nDesktop API: VirtualDesktopAccessor by Jari Pennanen (MIT).\n\nWindows taskbar location: right-click > Windows taskbar location > Top or Bottom. Uses native Windows taskbar positioning where available. No ExplorerPatcher installation needed. Windows may apply the location to all taskbars.\n\nBackground color: right-click > Background color...\n\nClick a tile: direct desktop switch without the pager's transition animation. Double-click an app: focus it. Drag an app to another tile: move that window silently. Escape cancels dragging.\n\nShow desktop titles: hide/show headers. Icon grid: reach overlapping apps.\n\nIntegrate with taskbar: show a compact always-on-top pager within the taskbar area. Drag the left grip horizontally to position it, or use Taskbar position. It can cover taskbar buttons; use Taskbar position to choose a clear area. Turn off integration to return to the floating pager. Double-click the tray icon to recover a floating pager.\n\nDock beside taskbar: a separate reserved appbar, now following the top/bottom taskbar position.\n\nVersion-specific desktop APIs support Windows 11 21H2 through 25H2. Unrecognized builds use Windows shortcuts for switching and creating desktops; app dragging and removal require Win+Tab there. Windows keyboard shortcuts may still animate; global animation settings are not changed.".Replace("\n",Environment.NewLine);
    Button close=new Button();close.Text="Close";close.AutoSize=true;
    close.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;close.DialogResult=DialogResult.OK;
    layout.Controls.Add(picture,0,0);layout.Controls.Add(help,0,1);layout.Controls.Add(close,0,2);
    dialog.Controls.Add(layout);dialog.AcceptButton=close;dialog.CancelButton=close;
    try {dialog.ShowDialog();} finally {picture.Image=null;}
   }
  }
  static byte[] ReadBytes(string path,string name) { using(RegistryKey k=Registry.CurrentUser.OpenSubKey(path)) return k==null?null:k.GetValue(name) as byte[]; }
  Guid CurrentDesktop() {
   if(ackDesktop!=Guid.Empty&&DateTime.UtcNow<ackUntil)return ackDesktop;
   byte[] b=ReadBytes(RegRoot,"CurrentVirtualDesktop");
   if(b==null||b.Length!=16) b=ReadBytes(@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\"+Process.GetCurrentProcess().SessionId+@"\VirtualDesktops","CurrentVirtualDesktop");
   return b!=null&&b.Length==16?new Guid(b):Guid.Empty;
  }
  string DesktopName(int i) {
   if(i<0||i>=desktops.Count)return "Desktop";
   string cached;if(names.TryGetValue(desktops[i],out cached))return cached;
   try {
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(RegRoot+@"\Desktops\{"+desktops[i]+"}")) {
    string name=k==null?null:k.GetValue("Name") as string;return String.IsNullOrWhiteSpace(name)?"Desktop "+(i+1):name;
   }
   }catch{return "Desktop "+(i+1);}
  }
  void RefreshState() {
   // Freeze the drag source and target GUIDs until the gesture ends.
   if(dragging||dragWindow!=IntPtr.Zero)return;
   try {
    byte[] bytes=ReadBytes(RegRoot,"VirtualDesktopIDs");desktops.Clear();
    if(bytes!=null&&bytes.Length%16==0)for(int n=0;n<bytes.Length;n+=16){byte[] g=new byte[16];Array.Copy(bytes,n,g,0,16);desktops.Add(new Guid(g));}
    current=CurrentDesktop();
    if(desktops.Count==0&&current!=Guid.Empty)desktops.Add(current);
    // On a fresh Windows profile no desktop registry values may exist yet.
    if(desktops.Count==0) { Guid own; if(manager.GetWindowDesktopId(Handle,out own)==0&&own!=Guid.Empty){desktops.Add(own);current=own;} }
    if(current==Guid.Empty&&desktops.Count==1)current=desktops[0];
    names.Clear();for(int i=0;i<desktops.Count;i++)names[desktops[i]]=DesktopName(i);
    FollowDesktop();
    windows.Clear();HashSet<IntPtr> seen=new HashSet<IntPtr>();
    Native.EnumWindows(delegate(IntPtr h,IntPtr unused) {
     try {
      if(h==Handle||!Native.IsWindowVisible(h)||Native.GetWindow(h,4)!=IntPtr.Zero)return true;
      uint pid;Native.GetWindowThreadProcessId(h,out pid);
      if(pid==0||pid==ownProcessId)return true;
      int style=Native.GetWindowLong(h,-20);if((style&0x80)!=0)return true;
      StringBuilder title=new StringBuilder(512);Native.GetWindowText(h,title,title.Capacity);if(title.Length==0)return true;
      StringBuilder cls=new StringBuilder(128);Native.GetClassName(h,cls,cls.Capacity);
      if(cls.ToString()=="Progman"||cls.ToString()=="WorkerW"||cls.ToString()=="Shell_TrayWnd"||cls.ToString()=="Shell_SecondaryTrayWnd")return true;
      int cloak; if(Native.DwmGetWindowAttribute(h,14,out cloak,4)==0&&(cloak&1)!=0)return true;
      Guid desktop;if(manager.GetWindowDesktopId(h,out desktop)!=0)return true;
      // An empty ID means unassigned/unknown, not "pinned on every desktop".
      // Exclude shell/helper windows instead of duplicating a grey placeholder.
      if(desktop==Guid.Empty||!desktops.Contains(desktop))return true;
      Native.RECT r;if(!Native.GetWindowRect(h,out r))return true;
      bool minimized=Native.IsIconic(h);
      if(minimized){Native.PLACEMENT p=new Native.PLACEMENT();p.length=Marshal.SizeOf(typeof(Native.PLACEMENT));if(Native.GetWindowPlacement(h,ref p)) {r=p.normal;Screen sc=Screen.FromHandle(h);r.L+=sc.WorkingArea.Left-sc.Bounds.Left;r.R+=sc.WorkingArea.Left-sc.Bounds.Left;r.T+=sc.WorkingArea.Top-sc.Bounds.Top;r.B+=sc.WorkingArea.Top-sc.Bounds.Top;}}
      if(r.R<=r.L||r.B<=r.T)return true;
      seen.Add(h);CachedIcon cache;
      if(icons.TryGetValue(h,out cache)&&(cache.Process!=pid||cache.Class!=cls.ToString())){if(cache.Icon!=null)cache.Icon.Dispose();icons.Remove(h);cache=null;}
      if(!icons.TryGetValue(h,out cache)){cache=new CachedIcon();cache.Process=pid;cache.Class=cls.ToString();cache.Icon=ReadIcon(h);icons[h]=cache;}
      windows.Add(new WindowInfo{Handle=h,Desktop=desktop,Bounds=r.Box,Title=title.ToString(),Icon=cache.Icon,Minimized=minimized,Pinned=false});
     }catch(COMException){}catch(ArgumentException){}
     return true;
    },IntPtr.Zero);
    foreach(IntPtr h in new List<IntPtr>(icons.Keys))if(!seen.Contains(h)){if(icons[h].Icon!=null)icons[h].Icon.Dispose();icons.Remove(h);}
    windows.Reverse();LayoutBar();Invalidate();
   }catch(Exception ex){SetStatus("Desktop detection unavailable. Right-click > Task View.");Debug.WriteLine(ex);}
  }
  Icon ReadIcon(IntPtr h) {
   IntPtr icon;Native.SendMessageTimeout(h,0x7F,new IntPtr(1),IntPtr.Zero,2,30,out icon);
   if(icon==IntPtr.Zero)Native.SendMessageTimeout(h,0x7F,IntPtr.Zero,IntPtr.Zero,2,30,out icon);
   if(icon==IntPtr.Zero)icon=IntPtr.Size==8?Native.ClassLong64(h,-14):new IntPtr(unchecked((int)Native.ClassLong32(h,-14)));
   try { return icon==IntPtr.Zero?null:(Icon)Icon.FromHandle(icon).Clone(); }catch { return null; }
  }
  void LayoutBar() {
   if(dragging||positioning)return;
   screenBounds=SystemInformation.VirtualScreen;
   if(embedded){LayoutEmbedded();return;}
   Screen monitor=customPosition?Screen.FromPoint(Location):Screen.PrimaryScreen;
   int tw=ScaleValue(tileWidth),th=ScaleValue(tileHeight),grip=ScaleValue(20);
   int columns=Math.Max(1,(monitor.WorkingArea.Width-grip)/tw);
   columns=Math.Min(columns,Math.Max(1,desktops.Count));int rows=(Math.Max(1,desktops.Count)+columns-1)/columns;
   if(docked&&appbarRegistered){PositionAppbar(rows*th);return;}
   Size=new Size(grip+columns*tw,rows*th);
   if(!customPosition) {
    Rectangle area=monitor.WorkingArea;
    int y=TaskbarIsTop()?area.Top+ScaleValue(3):area.Bottom-Height-ScaleValue(3);
    if(overTaskbar)y=TaskbarIsTop()?monitor.Bounds.Top:monitor.Bounds.Bottom-Height;
    Location=new Point(area.Left+(area.Width-Width)/2,Math.Max(monitor.Bounds.Top,y));
   } else {
    Rectangle b=monitor.Bounds; Location=new Point(Math.Max(b.Left,Math.Min(Left,b.Right-Width)),Math.Max(b.Top,Math.Min(Top,b.Bottom-Height)));
   }
   Native.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);
  }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);DrawPager(e.Graphics,ClientRectangle);
  }
  void DrawPager(Graphics g,Rectangle viewport){
   int Width=viewport.Width,Height=viewport.Height;Rectangle ClientRectangle=viewport;
   hits.Clear();int grip=ScaleValue(20),tw=embedded?Math.Max(8,(Width-grip)/Math.Max(1,desktops.Count)):ScaleValue(tileWidth),th=embedded?Height:ScaleValue(tileHeight);
   int cols=Math.Max(1,(Width-grip)/tw);
   Color textColor=ThemeTextColor();
   using(Brush gripBrush=new SolidBrush(Blend(backgroundColor,textColor,0.6)))for(int dot=0;dot<3;dot++)g.FillEllipse(gripBrush,ScaleValue(7),Height/2-ScaleValue(9)+dot*ScaleValue(7),ScaleValue(3),ScaleValue(3));
   for(int i=0;i<desktops.Count;i++) {
    Rectangle tile=new Rectangle(grip+(i%cols)*tw,(i/cols)*th,tw,th);
    bool selected=desktops[i]==current;
    using(Brush b=new SolidBrush(selected?Blend(backgroundColor,textColor,0.14):backgroundColor))g.FillRectangle(b,tile);
    hits.Add(new Hit{Box=tile,Desktop=i});
    int header=showTitles?ScaleValue(19):ScaleValue(3);
    Rectangle body=new Rectangle(tile.X+4,tile.Y+header,tw-8,th-header-ScaleValue(4));
    float scale=Math.Min((float)body.Width/Math.Max(1,screenBounds.Width),(float)body.Height/Math.Max(1,screenBounds.Height));
    float ox=body.X+(body.Width-screenBounds.Width*scale)/2,oy=body.Y+(body.Height-screenBounds.Height*scale)/2;
    if(!iconGrid)foreach(Screen monitor in Screen.AllScreens) {Rectangle b=monitor.Bounds;using(Pen pen=new Pen(Blend(backgroundColor,textColor,0.35)))g.DrawRectangle(pen,ox+(b.Left-screenBounds.Left)*scale,oy+(b.Top-screenBounds.Top)*scale,b.Width*scale,b.Height*scale);}
    int appCount=0,appIndex=0;foreach(WindowInfo app in windows)if(app.Pinned||app.Desktop==desktops[i])appCount++;
    int gridCols=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(appCount*(double)body.Width/Math.Max(1,body.Height))));
    int gridRows=Math.Max(1,(appCount+gridCols-1)/gridCols);
    foreach(WindowInfo w in windows) {
     if(!w.Pinned&&w.Desktop!=desktops[i])continue;
     Rectangle r=new Rectangle((int)(ox+(w.Bounds.Left-screenBounds.Left)*scale),(int)(oy+(w.Bounds.Top-screenBounds.Top)*scale),Math.Max(8,(int)(w.Bounds.Width*scale)),Math.Max(7,(int)(w.Bounds.Height*scale)));
     if(iconGrid){int cw=Math.Max(1,body.Width/gridCols),ch=Math.Max(1,body.Height/gridRows);r=new Rectangle(body.X+(appIndex%gridCols)*cw,body.Y+(appIndex/gridCols)*ch,Math.Max(2,cw-2),Math.Max(2,ch-2));}appIndex++;
     r=Rectangle.Intersect(r,body);if(r.Width<2||r.Height<2)continue;
     using(Brush b=new SolidBrush(w.Minimized?Color.FromArgb(126,127,135):Color.FromArgb(232,230,231)))g.FillRectangle(b,r);
     g.DrawRectangle(Pens.Gray,r.X,r.Y,r.Width-1,r.Height-1);
     int s=Math.Min(ScaleValue(24),Math.Min(r.Width-2,r.Height-2));
     if(s>=8){if(w.Icon!=null)g.DrawIcon(w.Icon,new Rectangle(r.X+(r.Width-s)/2,r.Y+(r.Height-s)/2,s,s));else g.FillRectangle(Brushes.SlateGray,r.X+(r.Width-s)/2,r.Y+(r.Height-s)/2,s,s);}
     if(w.Minimized)g.DrawLine(Pens.White,r.Left+2,r.Bottom-3,r.Right-2,r.Bottom-3);
     hits.Add(new Hit{Box=r,Desktop=i,Window=w.Handle});
    }
    if(showTitles)TextRenderer.DrawText(g,DesktopName(i),Font,new Rectangle(tile.X+5,tile.Y+1,tw-9,ScaleValue(17)),textColor,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    using(Pen p=new Pen(selected?(textColor==Color.Black?Color.FromArgb(150,65,0):Color.FromArgb(255,200,91)):Blend(backgroundColor,textColor,0.65),selected?2:1))g.DrawRectangle(p,tile.X+1,tile.Y+1,tw-2,th-2);
    if(appDragging&&dropDesktop==desktops[i]&&dropDesktop!=dragSource)using(Pen p=new Pen(Color.Aqua,3))g.DrawRectangle(p,tile.X+3,tile.Y+3,tw-6,th-6);
   }
   if(appDragging){CachedIcon ic;Rectangle ghost=new Rectangle(dragPoint.X+10,dragPoint.Y+10,ScaleValue(24),ScaleValue(24));g.FillRectangle(Brushes.DimGray,ghost);if(icons.TryGetValue(dragWindow,out ic)&&ic.Icon!=null)g.DrawIcon(ic.Icon,ghost);}
   if(desktops.Count==0)TextRenderer.DrawText(g,"Win+Tab",Font,new Rectangle(grip,0,Width-grip,Height),textColor,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
   if(DateTime.Now<statusUntil) {using(Brush b=new SolidBrush(Color.FromArgb(235,25,25,30)))g.FillRectangle(b,ClientRectangle);TextRenderer.DrawText(g,status,Font,ClientRectangle,Color.White,TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter);}
  }
  Hit HitAt(Point p) {for(int i=hits.Count-1;i>=0;i--)if(hits[i].Box.Contains(p))return hits[i];return null;}
  void OnDown(object sender,MouseEventArgs e) {
   if(e.Button==MouseButtons.Right){CancelDrag();return;}
   if(desktopOperation||e.Button!=MouseButtons.Left||(busy&&!switchPumping&&e.Clicks<2))return;
   gestureActive=true;downClicks=e.Clicks;pressPoint=e.Location;pressDesktop=DesktopAt(e.Location);
   if(e.X<ScaleValue(20)){
    if(docked){gestureActive=false;SetStatus("Right-click and turn off docking before moving the bar.");return;}
    dragging=true;dragStart=Cursor.Position;formStart=Location;embedDragOffset=embedOffset;Capture=true;cancelTimer.Start();return;
   }
   Hit h=HitAt(e.Location);
   if(h!=null&&h.Window!=IntPtr.Zero){
    dragWindow=h.Window;dragSource=desktops[h.Desktop];Native.GetWindowThreadProcessId(dragWindow,out dragProcess);
    Capture=true;cancelTimer.Start();
   }
  }
  void CancelDrag(){
   bool wasBarDrag=dragging;gestureActive=false;dragging=false;dragWindow=IntPtr.Zero;appDragging=false;dropDesktop=Guid.Empty;
   cancelTimer.Stop();Capture=false;Cursor=Cursors.Default;tooltip.Active=true;
   if(wasBarDrag){if(!embedded)customPosition=true;SaveSettings();}Invalidate();
  }
  Guid DesktopAt(Point point){Hit h=HitAt(point);return h!=null&&h.Desktop>=0&&h.Desktop<desktops.Count?desktops[h.Desktop]:Guid.Empty;}
  async void OnUp(object sender,MouseEventArgs e) {
   if(e.Button!=MouseButtons.Left||!gestureActive)return;
   if(dragging){CancelDrag();return;}
   if(appDragging){
    IntPtr window=dragWindow;Guid source=dragSource,target=DesktopAt(e.Location);uint pid=dragProcess;
    CancelDrag();if(target!=Guid.Empty&&target!=source)await MoveApp(window,pid,source,target);return;
   }
   bool doubleClick=downClicks>=2,validClick=DesktopAt(e.Location)==pressDesktop;CancelDrag();if(!validClick)return;
   Hit h=HitAt(e.Location);if(h!=null)await SwitchTo(h.Desktop,doubleClick?h.Window:IntPtr.Zero);
  }
  void OnMove(object sender,MouseEventArgs e) {
   if(dragging){if(embedded){embedOffset=Math.Max(0,embedDragOffset+(int)((Cursor.Position.X-dragStart.X)/DeviceScale));LayoutEmbedded();}else Location=new Point(formStart.X+Cursor.Position.X-dragStart.X,formStart.Y+Cursor.Position.Y-dragStart.Y);return;}
   if(dragWindow!=IntPtr.Zero){
    if(busy)return;
    Size threshold=SystemInformation.DragSize;
    if(!appDragging&&(Math.Abs(e.X-pressPoint.X)>threshold.Width/2||Math.Abs(e.Y-pressPoint.Y)>threshold.Height/2))appDragging=true;
    if(appDragging){dragPoint=e.Location;dropDesktop=DesktopAt(e.Location);Cursor=dropDesktop!=Guid.Empty&&dropDesktop!=dragSource?Cursors.SizeAll:Cursors.No;tooltip.Active=false;Invalidate();}
    return;
   }
   Hit h=HitAt(e.Location);Cursor=e.X<ScaleValue(20)?Cursors.SizeAll:Cursors.Hand;
   string text="Drag this grip to move. Right-click for options.";
   if(h!=null){text=DesktopName(h.Desktop)+" - click to switch";foreach(WindowInfo w in windows)if(w.Handle==h.Window){text=w.Title+(w.Minimized?" (minimized)":"")+"\nDrag to another tile to move; double-click to focus";break;}}
   if(text!=hoverText){hoverText=text;tooltip.SetToolTip(InputSurface,text);}
   int over=h==null?-1:h.Desktop;if(over!=lastHover){lastHover=over;Invalidate();}
  }
  async Task MoveApp(IntPtr window,uint pid,Guid source,Guid target){
   if(busy||!Native.IsWindow(window))return;
   busy=true;
   try {
    int result=await worker.Request("move "+window.ToInt64()+" "+pid+" "+source+" "+target);
    if(IsDisposed)return;
    Guid actual;bool verified=manager.GetWindowDesktopId(window,out actual)==0&&actual==target;
    if(verified){foreach(WindowInfo w in windows)if(w.Handle==window)w.Desktop=target;statusUntil=DateTime.MinValue;Invalidate();}
    else {RefreshState();string message=MoveWorker.Describe(result);SetStatus(message);tray.ShowBalloonTip(6000,"Window was not confirmed moved",message,ToolTipIcon.Warning);}
   }catch(Exception ex){if(!IsDisposed)SetStatus("Move failed: "+ex.Message);}
   finally{busy=false;}
  }
  bool dockDirty=true;
  int dockHeight;
  Rectangle dockScreen;
  Native.APPBARDATA AppbarData(){Native.APPBARDATA data=new Native.APPBARDATA();data.size=(uint)Marshal.SizeOf(typeof(Native.APPBARDATA));data.window=Handle;data.callback=appbarMessage;data.edge=TaskbarIsTop()?1u:3u;return data;}
  void RegisterAppbar(){
   if(appbarRegistered||!docked||IsDisposed)return;
   Native.APPBARDATA data=AppbarData();appbarRegistered=Native.SHAppBarMessage(0,ref data)!=UIntPtr.Zero;
   if(!appbarRegistered){docked=false;if(dockItem!=null)dockItem.Checked=false;SetStatus("Windows could not register the dock. Using a floating bar.");}
   dockDirty=true;
  }
  void RemoveAppbar(){
   if(!appbarRegistered)return;appbarRegistered=false;
   Native.APPBARDATA data=AppbarData();Native.SHAppBarMessage(1,ref data);dockDirty=true;
  }
  void SetDocked(bool value){
   if(value&&embedded)SetEmbedded(false);
   if(docked==value&&(!value||appbarRegistered))return;
   CancelDrag();docked=value;customPosition=false;
   if(dockItem!=null&&dockItem.Checked!=value)dockItem.Checked=value;
   if(docked)RegisterAppbar();else RemoveAppbar();
   LayoutBar();Invalidate();SaveSettings();
  }
  bool TaskbarIsTop(){
   IntPtr h=Native.FindWindow("Shell_TrayWnd",null);Native.RECT r;
   if(h==IntPtr.Zero||!Native.GetWindowRect(h,out r))return false;
   Rectangle b=Screen.FromHandle(h).Bounds;return r.R-r.L>r.B-r.T&&r.T<b.Top+b.Height/2;
  }
  void FollowDesktop(){
   if(current==Guid.Empty)return;
   Guid own;if(manager.GetWindowDesktopId(Handle,out own)==0&&own!=current){Guid dest=current;manager.MoveWindowToDesktop(Handle,ref dest);}
  }
  void FastTick(){
   if(IsDisposed||changingEmbedding)return;
   try{
    if(embedded){
     if(!dragging)LayoutEmbedded();
    }
    KeepOnTop();
    if(dragging||dragWindow!=IntPtr.Zero)return;
    Guid actual=CurrentDesktop();if(actual!=Guid.Empty&&actual!=current){current=actual;FollowDesktop();Invalidate();}
   }catch(Exception ex){Debug.WriteLine(ex);}
  }
  void KeepOnTop(){
   if(!Visible||DateTime.UtcNow<nextRaise||ContextMenuStrip.Visible||OwnedForms.Length>0)return;
   uint foregroundProcess;IntPtr foreground=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(foreground,out foregroundProcess);
   if(foregroundProcess==ownProcessId&&foreground!=Handle)return;
   nextRaise=DateTime.UtcNow.AddMilliseconds(250);
   // NOACTIVATE: restore topmost ordering without stealing keyboard focus.
   Native.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);
  }
  void SetEmbedded(bool value){
   if(changingEmbedding)return;
   changingEmbedding=true;CancelDrag();
   try{
    embedded=value;
    if(value){
     if(docked){docked=false;if(dockItem!=null)dockItem.Checked=false;RemoveAppbar();}
     LayoutEmbedded();
    }else{taskbarParent=IntPtr.Zero;DeviceScale=floatingScale;customPosition=false;LayoutBar();}
    FollowDesktop();Show();KeepOnTop();
   }finally{if(embedItem!=null)embedItem.Checked=embedded;changingEmbedding=false;Invalidate();SaveSettings();}
  }
  void LayoutEmbedded(){
   // Use our top-level window above the taskbar's compositor content. A child
   // HWND under Shell_TrayWnd cannot reliably overlay the Windows 11 XAML surface.
   IntPtr parent=Native.FindWindow("Shell_TrayWnd",null);Native.RECT r;
   if(parent==IntPtr.Zero||!Native.GetWindowRect(parent,out r)||r.R-r.L<80||r.B-r.T<1){
    taskbarParent=IntPtr.Zero;return; // Retry when Explorer recreates the taskbar.
   }
   taskbarParent=parent;
   uint dpi=Native.GetDpiForWindow(parent);DeviceScale=dpi>0?dpi/96f:floatingScale;
   Rectangle monitor=Screen.FromHandle(parent).Bounds;
   bool horizontal=r.R-r.L>=r.B-r.T;
   int thickness=horizontal?r.B-r.T:r.R-r.L;
   thickness=Math.Min(Math.Max(thickness,ScaleValue(32)),ScaleValue(100));
   // Auto-hide taskbars can live just outside monitor bounds: keep the pager visible.
   int count=Math.Max(1,desktops.Count),grip=ScaleValue(20);
   int available=monitor.Width;
   int minLength=Math.Min(available,grip+count*ScaleValue(20));
   int offset=Math.Max(0,Math.Min(ScaleValue(embedOffset),available-minLength));
   int length=Math.Min(grip+count*ScaleValue(tileWidth),available-offset);
   Rectangle desired;
   if(horizontal){
    int y=r.T<monitor.Top+monitor.Height/2?monitor.Top:monitor.Bottom-thickness;
    desired=new Rectangle(monitor.Left+offset,y,length,thickness);
   }else{
    int x=r.L<monitor.Left+monitor.Width/2?monitor.Left:monitor.Right-thickness;
    // Pager tiles remain a readable horizontal strip beside a vertical taskbar.
    desired=new Rectangle(x==monitor.Left?x:monitor.Right-length,monitor.Top+Math.Min(ScaleValue(embedOffset),monitor.Height-thickness),length,thickness);
   }
   if(Bounds!=desired){Bounds=desired;Invalidate();}
  }
  void ConfigureEmbedOffset(){
   using(Form dialog=new Form()){
    dialog.Text="Taskbar integration position";dialog.FormBorderStyle=FormBorderStyle.FixedDialog;dialog.StartPosition=FormStartPosition.CenterScreen;dialog.ClientSize=new Size(380,145);dialog.MaximizeBox=false;dialog.MinimizeBox=false;
    Label label=new Label();label.Text="Distance from the taskbar's left edge (logical pixels)";label.AutoSize=true;label.Location=new Point(15,16);dialog.Controls.Add(label);
    NumericUpDown offset=new NumericUpDown();offset.Minimum=0;offset.Maximum=20000;offset.Value=Math.Min(20000,embedOffset);offset.Location=new Point(15,45);offset.Width=150;dialog.Controls.Add(offset);
    Button apply=new Button();apply.Text="Apply";apply.Location=new Point(275,95);apply.DialogResult=DialogResult.OK;dialog.Controls.Add(apply);dialog.AcceptButton=apply;
    Label note=new Label();note.Text="You can also drag the pager's left grip horizontally.";note.AutoSize=true;note.Location=new Point(15,77);dialog.Controls.Add(note);
    if(dialog.ShowDialog(this)==DialogResult.OK){embedOffset=(int)offset.Value;LayoutEmbedded();SaveSettings();}
   }
  }
  static Color Blend(Color a,Color b,double amount){return Color.FromArgb((int)(a.R+(b.R-a.R)*amount),(int)(a.G+(b.G-a.G)*amount),(int)(a.B+(b.B-a.B)*amount));}
  static double LinearChannel(byte value){double s=value/255.0;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);}
  Color ThemeTextColor(){double luminance=0.2126*LinearChannel(backgroundColor.R)+0.7152*LinearChannel(backgroundColor.G)+0.0722*LinearChannel(backgroundColor.B);return luminance>0.179?Color.Black:Color.White;}
  void ApplyBackgroundColor(){BackColor=backgroundColor;Invalidate();}
  void ChooseBackgroundColor(){using(ColorDialog dialog=new ColorDialog()){dialog.Color=backgroundColor;dialog.FullOpen=true;dialog.AnyColor=true;if(dialog.ShowDialog(this)==DialogResult.OK){backgroundColor=Color.FromArgb(dialog.Color.R,dialog.Color.G,dialog.Color.B);ApplyBackgroundColor();SaveSettings();}}}
  void PositionAppbar(int height){
   Rectangle bounds=Screen.PrimaryScreen.Bounds;
   if(!dockDirty&&height==dockHeight&&bounds==dockScreen)return;
   positioning=true;
   try{
    Native.APPBARDATA data=AppbarData();data.rect.L=bounds.Left;data.rect.R=bounds.Right;data.rect.T=data.edge==1?bounds.Top:bounds.Bottom-height;data.rect.B=data.edge==1?bounds.Top+height:bounds.Bottom;
    Native.SHAppBarMessage(2,ref data);if(data.edge==1)data.rect.B=data.rect.T+height;else data.rect.T=data.rect.B-height;Native.SHAppBarMessage(3,ref data);
    SetBounds(data.rect.L,data.rect.T,data.rect.R-data.rect.L,data.rect.B-data.rect.T);
    Native.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);
    dockHeight=height;dockScreen=bounds;dockDirty=false;
   }finally{positioning=false;}
  }
  protected override void WndProc(ref Message m){
   if(appbarMessage!=0&&(uint)m.Msg==appbarMessage){
    if(m.WParam.ToInt32()==1&&!positioning)dockDirty=true;
    if(m.WParam.ToInt32()==2){Native.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);}
   }
   if(taskbarCreated!=0&&(uint)m.Msg==taskbarCreated){appbarRegistered=false;dockDirty=true;if(docked)RegisterAppbar();}
   if(m.Msg==0x7E||m.Msg==0x1A)dockDirty=true;
   if(appbarRegistered&&!positioning&&(m.Msg==0x47||m.Msg==0x06)){
    Native.APPBARDATA data=AppbarData();data.parameter=m.WParam;Native.SHAppBarMessage(m.Msg==0x47?9u:6u,ref data);
   }
   base.WndProc(ref m);
  }
  async Task SwitchTo(int index,IntPtr focus) {
   if(desktopOperation||index<0||index>=desktops.Count)return;
   pendingSwitch=desktops[index];pendingFocus=focus;
   if(switchPumping)return;
   if(busy){pendingSwitch=Guid.Empty;return;}
   switchPumping=true;busy=true;
   try{
    while(pendingSwitch!=Guid.Empty&&!IsDisposed){
     Guid target=pendingSwitch;IntPtr window=pendingFocus;pendingSwitch=Guid.Empty;
     if(CurrentDesktop()!=target){
      int result=await worker.Request("switch "+target);
      if(DesktopFallback.CanFallback(result))result=await fallback.Switch(target);
      if(IsDisposed)return;
      if(result!=0){SetStatus("Direct switch unavailable. "+MoveWorker.Describe(result));pendingSwitch=Guid.Empty;return;}
      ackDesktop=target;ackUntil=DateTime.UtcNow.AddMilliseconds(300);current=target;statusUntil=DateTime.MinValue;FollowDesktop();Invalidate();
     }
     if(window!=IntPtr.Zero&&Native.IsWindow(window)){if(Native.IsIconic(window))Native.ShowWindowAsync(window,9);Native.SetForegroundWindow(window);}
    }
   }catch(Exception ex){if(!IsDisposed)SetStatus("Switch unavailable: "+ex.Message);}finally{switchPumping=false;busy=false;}
  }
  async Task InitializeDesktops(){
   if(profileSetup==null||!profileSetup.Pending)return;
   desktopOperation=true;
   try{
    int result=await worker.Request("ping");
    if(DesktopFallback.CanFallback(result))result=await fallback.Ensure(6);
    else if(result==0)result=await worker.Request("ensure 6");
    if(IsDisposed)return;
    if(result==0){profileSetup.Complete();RefreshState();LayoutBar();}
    else SetStatus("Initial desktop setup: "+MoveWorker.Describe(result));
   }catch(Exception ex){if(!IsDisposed)SetStatus("Initial desktop setup: "+ex.Message);}
   finally{desktopOperation=false;}
  }
  async Task CreateDesktop(){
   if(desktopOperation||busy)return;desktopOperation=true;CancelDrag();
   try{
    int result=await worker.Request("ping");if(IsDisposed)return;
    if(DesktopFallback.CanFallback(result))result=await fallback.Create();
    else if(result==0)result=await worker.Request("create");
    RefreshState();LayoutBar();
    if(result!=0)SetStatus("New desktop: "+MoveWorker.Describe(result));
   }finally{desktopOperation=false;}
  }
  async Task RemoveDesktop(Guid source){
   if(desktopOperation||busy)return;
   RefreshState();int from=desktops.IndexOf(source);
   if(from<0||desktops.Count<=1)return;
   Guid target=desktops[(from+1)%desktops.Count];
   desktopOperation=true;CancelDrag();
   try{
    int result=await worker.Request("remove "+source+" "+target);
    if(IsDisposed)return;
    ackDesktop=Guid.Empty;ackUntil=DateTime.MinValue;pendingSwitch=Guid.Empty;
    RefreshState();LayoutBar();
    if(result!=0)SetStatus("Remove desktop: "+MoveWorker.Describe(result));
   }finally{desktopOperation=false;}
  }
  void SetStatus(string s){status=s;statusUntil=DateTime.Now.AddSeconds(4);Invalidate();}
  void LoadSettings() {try{string source=settings;if(!File.Exists(source)&&loadLegacySettings)source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VernStylePager","settings.txt");if(!File.Exists(source))return;string[] v=File.ReadAllLines(source);if(v.Length<6)return;tileWidth=Math.Max(80,Math.Min(240,Int32.Parse(v[0])));tileHeight=Math.Max(48,Math.Min(132,Int32.Parse(v[1])));customPosition=Boolean.Parse(v[2]);overTaskbar=Boolean.Parse(v[3]);Location=new Point(Int32.Parse(v[4]),Int32.Parse(v[5]));if(v.Length>=9){showTitles=Boolean.Parse(v[6]);iconGrid=Boolean.Parse(v[7]);docked=Boolean.Parse(v[8]);}if(v.Length>=11){embedded=Boolean.Parse(v[9]);embedOffset=Math.Max(0,Math.Min(20000,Int32.Parse(v[10])));if(embedded)docked=false;}if(v.Length>=12){int color;if(Int32.TryParse(v[11],out color)){Color loaded=Color.FromArgb(color);backgroundColor=Color.FromArgb(loaded.R,loaded.G,loaded.B);}}}catch{customPosition=false;}}
  void SaveSettings() {try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllLines(settings,new string[]{tileWidth.ToString(),tileHeight.ToString(),customPosition.ToString(),overTaskbar.ToString(),Left.ToString(),Top.ToString(),showTitles.ToString(),iconGrid.ToString(),docked.ToString(),embedded.ToString(),embedOffset.ToString(),backgroundColor.ToArgb().ToString()});}catch(Exception ex){Debug.WriteLine(ex);}}
  protected override void OnFormClosed(FormClosedEventArgs e) {CancelDrag();timer.Stop();timer.Dispose();fastTimer.Stop();fastTimer.Dispose();cancelTimer.Stop();cancelTimer.Dispose();fallback.Cancel();worker.Dispose();RemoveAppbar();SaveSettings();tray.Visible=false;tray.Dispose();brandIcon.Dispose();tooltip.Dispose();foreach(CachedIcon c in icons.Values)if(c.Icon!=null)c.Icon.Dispose();Marshal.ReleaseComObject(manager);base.OnFormClosed(e);}
 }
 static class Program {
  [STAThread] static void Main(string[] args) {
   if(args.Length>0&&args[0]=="--worker"){MoveWorker.Serve();return;}
   bool created;using(Mutex mutex=new Mutex(true,@"Local\VernStylePager_01",out created)) {
    if(!created){MessageBox.Show("BioPager is already running. Look for the bar or its tray icon.");return;}
    try {Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Pager());}
    catch(Exception ex){MessageBox.Show("BioPager could not start.\n\n"+ex.ToString(),"BioPager");}
   }
  }
 }
}
