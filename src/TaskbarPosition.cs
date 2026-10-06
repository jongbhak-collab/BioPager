// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// Interoperates with an independently installed ExplorerPatcher; no EP code is bundled.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BioPager {
 static class TaskbarPosition {
  const uint TraySettingMessage = 0x400 + 0x1CA;
  [DllImport("user32.dll", SetLastError=true)]
  static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
   IntPtr lParam, uint flags, uint timeout, out UIntPtr result);

  static bool HasActiveExplorerPatcher(IntPtr tray) {
   uint pid; Native.GetWindowThreadProcessId(tray, out pid);
   using(Process explorer=Process.GetProcessById((int)pid)) {
    foreach(ProcessModule module in explorer.Modules) {
     string name=Path.GetFileName(module.FileName);
     if(name.StartsWith("ep_taskbar.", StringComparison.OrdinalIgnoreCase))return true;
     if(name.Equals("dxgi.dll",StringComparison.OrdinalIgnoreCase)) {
      FileVersionInfo info=FileVersionInfo.GetVersionInfo(module.FileName);
      if((info.ProductName??"").IndexOf("ExplorerPatcher",StringComparison.OrdinalIgnoreCase)>=0 ||
         (info.FileDescription??"").IndexOf("ExplorerPatcher",StringComparison.OrdinalIgnoreCase)>=0)return true;
     }
    }
   }
   return false;
  }

  static int ReadEdge() {
   Native.APPBARDATA data=new Native.APPBARDATA();
   data.size=(uint)Marshal.SizeOf(typeof(Native.APPBARDATA));
   if(Native.SHAppBarMessage(5,ref data)==UIntPtr.Zero)return -1; // ABM_GETTASKBARPOS
   return (int)data.edge;
  }

  internal static async Task SetAsync(IWin32Window owner, bool top) {
   try {
    IntPtr tray=Native.FindWindow("Shell_TrayWnd",null);
    if(tray==IntPtr.Zero)throw new InvalidOperationException("The Windows taskbar is unavailable. Try again after Explorer has started.");
    if(!HasActiveExplorerPatcher(tray)) {
     MessageBox.Show(owner,"Moving the taskbar requires ExplorerPatcher with its Windows 10 (ExplorerPatcher) taskbar enabled.\n\nInstall a release compatible with your Windows build, enable that taskbar in ExplorerPatcher Properties, then choose Top or Bottom here again.\n\nBioPager does not install ExplorerPatcher or replace your taskbar automatically.","Taskbar position",MessageBoxButtons.OK,MessageBoxIcon.Information);
     return;
    }
    using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\ExplorerPatcher")) {
     object style=key==null?null:key.GetValue("OldTaskbar");
     if(style!=null && Convert.ToInt32(style)==0)throw new InvalidOperationException("Enable Windows 10 (ExplorerPatcher) taskbar in ExplorerPatcher Properties first. The stock Windows 11 taskbar cannot be moved by this option.");
    }
    int edge=top?1:3;
    if(ReadEdge()==edge)return;
    UIntPtr result;
    int before=ReadEdge();
    if(before<0 || SendMessageTimeout(tray,TraySettingMessage,new UIntPtr(5),IntPtr.Zero,2,1000,out result)==IntPtr.Zero || result.ToUInt64()!=(ulong)before)
     throw new InvalidOperationException("This taskbar does not expose the compatible ExplorerPatcher positioning interface. Use ExplorerPatcher Properties to change its location.");
    if(SendMessageTimeout(tray,TraySettingMessage,new UIntPtr(6),new IntPtr(edge),2,1000,out result)==IntPtr.Zero)
     throw new InvalidOperationException("Explorer did not respond to the taskbar position request. Try again later.");
    for(int i=0;i<20;i++) {
     await Task.Delay(100);
     if(ReadEdge()==edge)return;
    }
    throw new InvalidOperationException("Windows did not confirm the requested taskbar position. Open ExplorerPatcher Properties and check its taskbar style and primary taskbar location. No Explorer restart was performed.");
   } catch(Exception ex) {
    MessageBox.Show(owner,ex.Message,"Taskbar position",MessageBoxButtons.OK,MessageBoxIcon.Information);
   }
  }
 }
}
