// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// Uses Windows' native taskbar setting; no ExplorerPatcher or Windhawk dependency.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BioPager {
 static class TaskbarPosition {
  const string SettingsKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
  const string LocationValue="TaskbarLocation";
  const uint TraySettingMessage=0x5CA;
  [DllImport("user32.dll",SetLastError=true)]
  static extern IntPtr SendMessageTimeout(IntPtr window,uint message,UIntPtr wParam,
   IntPtr lParam,uint flags,uint timeout,out UIntPtr result);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageTimeoutW",SetLastError=true)]
  static extern IntPtr NotifySetting(IntPtr window,uint message,UIntPtr wParam,
   string lParam,uint flags,uint timeout,out UIntPtr result);

  static int ReadEdge() {
   Native.APPBARDATA data=new Native.APPBARDATA();
   data.size=(uint)Marshal.SizeOf(typeof(Native.APPBARDATA));
   return Native.SHAppBarMessage(5,ref data)==UIntPtr.Zero?-1:(int)data.edge;
  }
  static bool Request(IntPtr tray,int edge) {
   UIntPtr result;
   return SendMessageTimeout(tray,TraySettingMessage,new UIntPtr(6),new IntPtr(edge),2,1000,out result)!=IntPtr.Zero;
  }
  static void Notify(IntPtr tray) {
   UIntPtr result;
   NotifySetting(tray,0x1A,UIntPtr.Zero,"TraySettings",2,1000,out result);
  }
  static async Task<bool> WaitForEdge(int edge) {
   for(int i=0;i<20;i++) {
    await Task.Delay(100);
    if(ReadEdge()==edge)return true;
   }
   return false;
  }

  internal static async Task SetAsync(IWin32Window owner,bool top) {
   if(MessageBox.Show(owner,
    "Some Windows 11 releases may not support changing the taskbar location.\n\nIf this option does not work on your Windows release, please install and use ExplorerPatcher independently to change the taskbar location. BioPager does not install or manage ExplorerPatcher.\n\nClick OK to try changing the location with BioPager, or Cancel to leave it unchanged.",
    "Taskbar location compatibility warning",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,
    MessageBoxDefaultButton.Button2)!=DialogResult.OK)return;
   try {
    IntPtr tray=Native.FindWindow("Shell_TrayWnd",null);
    int before=ReadEdge(),edge=top?1:3;
    if(tray==IntPtr.Zero||before<0)throw new InvalidOperationException("The Windows taskbar is unavailable. Try again after Explorer has started.");
    if(before==edge)return;
    Native.APPBARDATA state=new Native.APPBARDATA();
    state.size=(uint)Marshal.SizeOf(typeof(Native.APPBARDATA));
    if(top&&(Native.SHAppBarMessage(4,ref state).ToUInt64()&1)!=0)
     throw new InvalidOperationException("Turn off Automatically hide the taskbar in Windows taskbar settings before selecting Top. BioPager leaves your auto-hide preference unchanged.");
    // Reject an unrecognized private-message protocol before changing settings.
    UIntPtr reported;
    if(SendMessageTimeout(tray,TraySettingMessage,new UIntPtr(5),IntPtr.Zero,2,1000,out reported)==IntPtr.Zero||reported.ToUInt64()!=(ulong)before)
     throw new InvalidOperationException("This Windows taskbar does not expose a compatible positioning interface. Check Settings > Personalization > Taskbar > Taskbar behaviors for Taskbar position.");
    using(RegistryKey key=Registry.CurrentUser.OpenSubKey(SettingsKey,true)) {
     if(key==null)throw new InvalidOperationException("Windows taskbar settings could not be opened.");
     object previous=key.GetValue(LocationValue,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
     RegistryValueKind kind=previous==null?RegistryValueKind.DWord:key.GetValueKind(LocationValue);
     bool changed=false,committed=false;
     Exception failure=null;
     try {
      key.SetValue(LocationValue,edge,RegistryValueKind.DWord);changed=true;
      if(Request(tray,edge)) {
       Notify(tray);
       committed=await WaitForEdge(edge);
      }
     } catch(Exception ex) {failure=ex;}
      if(changed&&!committed) {
       // Avoid overwriting a setting changed concurrently by Windows or the user.
       object current=key.GetValue(LocationValue);
       if(current is int&&(int)current==edge) {
        if(previous==null)key.DeleteValue(LocationValue,false);
        else key.SetValue(LocationValue,previous,kind);
        tray=Native.FindWindow("Shell_TrayWnd",null);
        if(tray!=IntPtr.Zero){Request(tray,before);Notify(tray);}
        if(!await WaitForEdge(before))
         throw new InvalidOperationException("The requested position was not confirmed. The saved setting was restored, but the taskbar did not confirm its original position. Use Windows taskbar settings to restore the location.");
       } else {
        throw new InvalidOperationException("The taskbar setting changed during the operation. BioPager stopped to preserve that newer change. Check the location in Windows taskbar settings.");
       }
      }
     if(failure!=null)throw new InvalidOperationException("Taskbar positioning failed; the previous setting was restored. "+failure.Message,failure);
     if(!committed)throw new InvalidOperationException("Windows did not support the requested position. The previous setting was restored.\n\nCheck Settings > Personalization > Taskbar > Taskbar behaviors for Taskbar position. If that option is missing, your Windows build or feature rollout does not yet support native positioning. BioPager cannot add that shell feature with a simple setting change.");
    }
   } catch(Exception ex) {
    MessageBox.Show(owner,ex.Message+"\n\nIf your Windows 11 release does not support this option, please install and use ExplorerPatcher independently to change the taskbar location.","Windows taskbar location",MessageBoxButtons.OK,MessageBoxIcon.Information);
   }
  }
 }
}
