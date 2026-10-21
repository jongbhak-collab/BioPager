// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace BioPager {
 internal static class DesktopFocus {
  [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
  [DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr window);
  [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

  static bool Eligible(IDesktopManager manager,IntPtr window,bool allowMinimized) {
   if(window==IntPtr.Zero||!Native.IsWindow(window)||!Native.IsWindowVisible(window)||!IsWindowEnabled(window))return false;
   if(!allowMinimized&&Native.IsIconic(window))return false;
   uint pid;Native.GetWindowThreadProcessId(window,out pid);
   if(pid==0||pid==(uint)Process.GetCurrentProcess().Id)return false;
   if((Native.GetWindowLong(window,-20)&(0x80|0x08000000))!=0)return false;
   StringBuilder name=new StringBuilder(128);Native.GetClassName(window,name,name.Capacity);
   string cls=name.ToString();
   if(cls=="Progman"||cls=="WorkerW"||cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd")return false;
   int cloaked;if(Native.DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0)return false;
   bool onCurrent;return manager.IsWindowOnCurrentVirtualDesktop(window,out onCurrent)==0&&onCurrent;
  }
  internal static bool ActivateFront(IDesktopManager manager,IntPtr requested) {
   IntPtr chosen=IntPtr.Zero;
   if(requested!=IntPtr.Zero&&Native.IsWindow(requested)) {
    // Prefer an enabled modal dialog belonging to an explicitly chosen app.
    IntPtr popup=GetLastActivePopup(requested);
    if(Eligible(manager,popup,true))chosen=popup;
    else if(Eligible(manager,requested,true))chosen=requested;
   } else {
    // EnumWindows yields top-level windows in front-to-back Z order.
    Native.EnumWindows(delegate(IntPtr window,IntPtr unused) {
     try{if(Eligible(manager,window,false)){chosen=window;return false;}}
     catch(COMException){}
     return true;
    },IntPtr.Zero);
   }
   if(chosen==IntPtr.Zero)return false; // Retry briefly while shell windows become visible.
   if(Native.IsIconic(chosen))Native.ShowWindowAsync(chosen,9);
   if(Native.GetForegroundWindow()==chosen)return true;
   Native.SetForegroundWindow(chosen);
   if(Native.GetForegroundWindow()==chosen)return true;
   // Transfer the user's pager click to the foreground app without injecting keys.
   uint pid;uint foregroundThread=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);
   uint ownThread=GetCurrentThreadId();bool attached=false;
   try {
    if(foregroundThread!=0&&foregroundThread!=ownThread)attached=AttachThreadInput(ownThread,foregroundThread,true);
    Native.SetForegroundWindow(chosen);
   } finally {if(attached)AttachThreadInput(ownThread,foregroundThread,false);}
   return Native.GetForegroundWindow()==chosen;
  }
 }
}
