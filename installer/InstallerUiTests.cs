// Test-only inspection of the installer windows on an isolated CI desktop.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
static class InstallerUiTests {
 delegate bool EnumProc(IntPtr h,IntPtr data);
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int L,T,R,B;}
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,EnumProc callback,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 static string Text(IntPtr h){StringBuilder text=new StringBuilder(8192);GetWindowText(h,text,text.Capacity);return text.ToString();}
 static string VisibleText(IntPtr h){StringBuilder text=new StringBuilder(Text(h));EnumChildWindows(h,delegate(IntPtr child,IntPtr ignored){if(IsWindowVisible(child))text.AppendLine(Text(child));return true;},IntPtr.Zero);return text.ToString();}
 static IntPtr FindWizard(DateTime started) {
  IntPtr found=IntPtr.Zero;
  EnumWindows(delegate(IntPtr h,IntPtr ignored){
   if(!IsWindowVisible(h)||!Text(h).Contains("BioPager"))return true;
   StringBuilder cls=new StringBuilder(80);GetClassName(h,cls,cls.Capacity);
   if(cls.ToString()!="TWizardForm")return true;
   uint pid;GetWindowThreadProcessId(h,out pid);
   try {using(Process p=Process.GetProcessById((int)pid)){
    if(p.StartTime.ToUniversalTime()<started||p.SessionId!=Process.GetCurrentProcess().SessionId)return true;
    if(!Path.GetFileName(p.MainModule.FileName).StartsWith("BioPager-Setup-",StringComparison.OrdinalIgnoreCase))return true;
    found=h;return false;
   }}catch{return true;}
  },IntPtr.Zero);return found;
 }
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static void Snapshot(IntPtr h,string path){
  SetForegroundWindow(h);Thread.Sleep(250);Rect r;Check(GetWindowRect(h,out r)&&r.R>r.L&&r.B>r.T,"installer has a visible capture rectangle");
  using(Bitmap image=new Bitmap(r.R-r.L,r.B-r.T))using(Graphics g=Graphics.FromImage(image)){
   g.CopyFromScreen(new Point(r.L,r.T),Point.Empty,image.Size);image.Save(path,ImageFormat.Png);
  }
 }
 static void Next(IntPtr h){
  IntPtr button=IntPtr.Zero;
  EnumChildWindows(h,delegate(IntPtr child,IntPtr ignored){if(IsWindowVisible(child)&&Text(child).Replace("&","").StartsWith("Next")){button=child;return false;}return true;},IntPtr.Zero);
  Check(button!=IntPtr.Zero,"installer Next button is available");PostMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);Thread.Sleep(400);
 }
 static int Main(string[] args){
  Process setup=null;IntPtr wizard=IntPtr.Zero;
  try {
   if(args.Length!=2)throw new ArgumentException("setup.exe evidence-folder");
   SetProcessDPIAware();Directory.CreateDirectory(args[1]);
   DateTime started=DateTime.UtcNow.AddSeconds(-1);
   setup=Process.Start(new ProcessStartInfo(args[0],"/SP- /NORESTART"){UseShellExecute=false});
   for(int i=0;i<120&&wizard==IntPtr.Zero;i++){Thread.Sleep(100);wizard=FindWizard(started);}
   Check(wizard!=IntPtr.Zero,"normal interactive installer opens its wizard");Thread.Sleep(400);
   string welcome=VisibleText(wizard);File.WriteAllText(Path.Combine(args[1],"welcome-text.txt"),welcome);
   Check(welcome.Contains("No administrator password")&&welcome.Contains("keeping your preferences"),"welcome explains installation scope and retained preferences");
   Snapshot(wizard,Path.Combine(args[1],"setup-welcome.png"));
   Next(wizard);string usage=VisibleText(wizard);File.WriteAllText(Path.Combine(args[1],"usage-text.txt"),usage);
   Check(usage.Contains("Using BioPager")&&usage.Contains("SWITCH AND FOCUS")&&usage.Contains("MOVE WINDOWS"),"setup includes switch focus and drag instructions");
   Check(usage.Contains("CHOOSE YOUR VIEW")&&usage.Contains("FIND IT AGAIN"),"setup explains view selection and tray recovery");
   Snapshot(wizard,Path.Combine(args[1],"setup-usage.png"));
   Next(wizard);string tasks=VisibleText(wizard);File.WriteAllText(Path.Combine(args[1],"tasks-text.txt"),tasks);
   Check(tasks.Contains("Additional Tasks")||tasks.Contains("additional tasks"),"optional tasks page follows the guide");
   Snapshot(wizard,Path.Combine(args[1],"setup-options.png"));
   return 0;
  }catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}
  finally {
   // Stop only the wizard process started for this screenshot test, before any
   // Install click. The separate integration suite exercises full installation.
   if(wizard!=IntPtr.Zero){uint pid;GetWindowThreadProcessId(wizard,out pid);try{using(Process p=Process.GetProcessById((int)pid)){if(!p.HasExited){p.Kill();p.WaitForExit(3000);}}}catch{}}
   if(setup!=null){try{if(!setup.WaitForExit(3000))setup.Kill();}catch{}setup.Dispose();}
  }
 }
}
