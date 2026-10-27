// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// Setup-only helper: request a graceful exit, never terminate a process.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
[assembly: AssemblyTitle("BioPager Setup Helper")]
[assembly: AssemblyProduct("BioPager Setup Helper")]
[assembly: AssemblyCompany("Jong Bhak")]
[assembly: AssemblyVersion("1.1.1.0")]
namespace BioPagerSetup {
 internal static class Helper {
  delegate bool EnumProc(IntPtr window,IntPtr state);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr state);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
  [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint length);
  static readonly int session=Process.GetCurrentProcess().SessionId;
  static readonly SecurityIdentifier user=WindowsIdentity.GetCurrent().User;
  static string directory;
  static bool IsTarget(Process process) {
   try {
    if(process.HasExited||process.SessionId!=session)return false;
    IntPtr handle=OpenProcess(0x1000,false,(uint)process.Id);
    if(handle==IntPtr.Zero)return false;
    try {
     IntPtr token;
     if(!OpenProcessToken(handle,8,out token))return false;
     try {using(WindowsIdentity identity=new WindowsIdentity(token))if(!user.Equals(identity.User))return false;}
     finally{CloseHandle(token);}
     StringBuilder path=new StringBuilder(32768);uint size=(uint)path.Capacity;
     if(!QueryFullProcessImageName(handle,0,path,ref size))return false;
     string filename=path.ToString();
     if(directory!=null&&!String.Equals(Path.GetDirectoryName(Path.GetFullPath(filename)),directory,StringComparison.OrdinalIgnoreCase))return false;
     FileVersionInfo version=FileVersionInfo.GetVersionInfo(filename);
     // These are compatibility identity checks, not signature verification.
     return version.ProductName=="BioPager"&&version.CompanyName=="Jong Bhak";
    }finally{CloseHandle(handle);}
   }catch(Exception){return false;}
  }
  static bool StillRunning(Process process) {try{return !process.HasExited;}catch{return false;}}
  static int ClosePager() {
   List<Process> targets=new List<Process>();HashSet<int> candidates=new HashSet<int>();
   // Legacy portable executables may have been renamed. Discover their main
   // window as well as the normal executable name, then verify owner/metadata.
   EnumWindows(delegate(IntPtr window,IntPtr unused){
    StringBuilder title=new StringBuilder(64);GetWindowText(window,title,title.Capacity);
    if(title.ToString()=="BioPager"){uint pid;GetWindowThreadProcessId(window,out pid);candidates.Add((int)pid);}return true;
   },IntPtr.Zero);
   foreach(Process process in Process.GetProcessesByName("BioPager")){candidates.Add(process.Id);process.Dispose();}
   foreach(int pid in candidates){
    Process process=null;
    try{process=Process.GetProcessById(pid);if(IsTarget(process)){targets.Add(process);process=null;}}
    catch(ArgumentException){}finally{if(process!=null)process.Dispose();}
   }
   try {
    EnumWindows(delegate(IntPtr window,IntPtr unused){
     uint pid;GetWindowThreadProcessId(window,out pid);
     foreach(Process process in targets)if(process.Id==(int)pid&&StillRunning(process)&&IsTarget(process)){
      StringBuilder title=new StringBuilder(64);GetWindowText(window,title,title.Capacity);
      if(title.ToString()=="BioPager")PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);
      break;
     }return true;
    },IntPtr.Zero);
    Stopwatch clock=Stopwatch.StartNew();
    while(clock.ElapsedMilliseconds<10000){
     bool running=false;foreach(Process process in targets)if(StillRunning(process))running=true;
     if(!running)return 0;Thread.Sleep(100);
    }
    return 20; // Keep the old installation intact when a graceful exit fails.
   }finally{foreach(Process process in targets)process.Dispose();}
  }
  static int Main(string[] args) {
   try {
    if(args.Length==1&&args[0]=="--close")return ClosePager();
    if(args.Length==2&&args[0]=="--close-dir"){
     directory=Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);return ClosePager();
    }
    return 2;
   }catch(Exception){return 21;}
  }
 }
}
