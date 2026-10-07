// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace BioPager {
 // A session-only override: do not write the user's saved animation preference.
 internal sealed class DesktopAnimation : IDisposable {
  [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]
  static extern bool Read(uint action,uint parameter,out int value,uint flags);
  [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]
  static extern bool Write(uint action,uint parameter,IntPtr value,uint flags);
  readonly bool restore;
  bool disposed;
  internal DesktopAnimation() {
   int enabled;
   if(!Read(0x1042,0,out enabled,0))throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not read Windows animation effects.");
   restore=enabled!=0;
   if(restore&&!Write(0x1043,0,IntPtr.Zero,2))throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not disable Windows animation effects for an instant desktop switch.");
  }
  public void Dispose() {
   if(disposed)return;disposed=true;
   if(!restore)return;
   int enabled;
   // Preserve a newer user setting if effects have already been re-enabled.
   if(Read(0x1042,0,out enabled,0)&&enabled==0)
    if(!Write(0x1043,0,new IntPtr(1),2))
     System.Diagnostics.Debug.WriteLine("BioPager could not restore Windows animation effects.");
  }
 }
}
