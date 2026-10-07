using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
namespace BioPager {
 internal static class Tests {
  [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr h,int index,int value);
  static int fakeCount,creates;
  static bool refuse;
  static Guid fakeSource=Guid.NewGuid(),fakeNext=Guid.NewGuid();
  static int FakeCount(){return fakeCount;}
  static int FakeCreate(){creates++;return fakeCount++;}
  static Guid FakeId(int i){return i==0?fakeSource:i==1?fakeNext:new Guid(i,0,0,new byte[8]);}
  static int FakeRemove(int i,int t){if(refuse)return -1;fakeCount--;return 1;}
  static void Bind(string field,string method){FieldInfo f=typeof(MoveWorker).GetField(field,BindingFlags.Static|BindingFlags.NonPublic);f.SetValue(null,Delegate.CreateDelegate(f.FieldType,typeof(Tests).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)));}
  static object Call(string method,params object[] args){return typeof(MoveWorker).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
  static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
  static void Unit(){
   Check(WindowsCompatibility.Select(22000,1)=="legacy21","21H2 selects integrated managed backend");
   Check(WindowsCompatibility.Select(22621,2214)=="2023-02-22-windows11","early 22H2 selects matching helper");
   Check(WindowsCompatibility.Select(22621,2215)=="2023-11-10-windows11","22H2 interface change at revision 2215");
   Check(WindowsCompatibility.Select(22631,3084)=="2023-11-10-windows11","early 23H2 selects matching helper");
   Check(WindowsCompatibility.Select(22631,3085)=="2024-01-25-windows11","23H2 interface change at revision 3085");
   Check(WindowsCompatibility.Select(26100,2604)==null,"unverified early 24H2 uses shortcuts");
   Check(WindowsCompatibility.Select(26100,2605)=="modern"&&WindowsCompatibility.Select(26200,9000)=="modern","24H2 and 25H2 select modern backend");
   Check(WindowsCompatibility.Select(26300,1)==null&&WindowsCompatibility.Select(19045,1)==null,"unknown builds never load guessed COM interfaces");
   Check(!DesktopFallback.CanFallback(28)&&!DesktopFallback.CanFallback(22),"unconfirmed mutations and bad payloads are not retried via shortcuts");
   Bind("count","FakeCount");Bind("createDesktop","FakeCreate");Bind("id","FakeId");Bind("removeDesktop","FakeRemove");
   fakeCount=1;creates=0;
   Check((int)Call("EnsureMinimum",6)==0&&fakeCount==6&&creates==5,"first-use setup adds five to a one-desktop profile");
   Check((int)Call("EnsureMinimum",6)==0&&creates==5,"repeated setup does not recreate desktops");
   fakeCount=10;Check((int)Call("EnsureMinimum",6)==0&&fakeCount==10,"existing larger layouts are preserved");
   fakeCount=1;Check((int)Call("Remove",fakeSource,fakeNext)==27&&fakeCount==1,"last desktop cannot be removed");
   fakeCount=3;Check((int)Call("Remove",fakeSource,FakeId(2))==12&&fakeCount==3,"stale or non-next target rejected before removal");
   refuse=true;Check((int)Call("Remove",fakeSource,fakeNext)==29&&fakeCount==3,"Windows refusal does not report success");refuse=false;
   string folder=Path.Combine(Path.GetTempPath(),"BioPagerProfileTest-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   try{
    ProfileSetup profile=new ProfileSetup(folder,false);Check(profile.Pending,"fresh profile marks setup pending");
    File.WriteAllText(Path.Combine(folder,"settings.txt"),"saved");
    Check(new ProfileSetup(folder,true).Pending,"failed first setup remains pending after settings save");
    profile.Complete();Check(!new ProfileSetup(folder,true).Pending,"completed setup does not run again");
    string legacy=Path.Combine(folder,"legacy");Check(!new ProfileSetup(legacy,true).Pending,"upgrading existing users skips default desktop creation");
   }finally{Directory.Delete(folder,true);}
   typeof(MoveWorker).GetField("ready",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,-1);
  }
  static async Task FallbackTests(){
   Guid a=Guid.NewGuid(),b=Guid.NewGuid(),c=Guid.NewGuid(),active=a;
   List<Guid> layout=new List<Guid>{a,b,c};int keys=0;
   DesktopFallback fallback=new DesktopFallback(delegate{return new List<Guid>(layout);},delegate{return active;},delegate(ushort key){
    keys++;int index=layout.IndexOf(active);
    if(key==0x27)active=layout[index+1];else if(key==0x25)active=layout[index-1];
    else if(key==0x44){active=Guid.NewGuid();layout.Add(active);}return true;
   });
   Check(await fallback.Switch(c)==0&&keys==2&&active==c,"fallback switches one acknowledged desktop at a time");
   Check(await fallback.Switch(Guid.NewGuid())==12&&keys==2,"fallback rejects missing target without sending keys");
   Check(await fallback.Create()==0&&layout.Count==4&&active==c,"fallback creation restores original desktop");
   Check(await fallback.Ensure(6)==0&&layout.Count==6&&active==c,"fallback fresh profile reaches six desks");
   DesktopFallback blocked=new DesktopFallback(delegate{return new List<Guid>(layout);},delegate{return active;},delegate(ushort key){return false;});
   Check(await blocked.Switch(a)==31&&await blocked.Create()==31,"held modifiers or blocked input cannot report success");
   DesktopFallback changed=new DesktopFallback(delegate{return new List<Guid>(layout);},delegate{return active;},delegate(ushort key){layout.Reverse();return true;});
   Check(await changed.Switch(a)!=0,"concurrent layout changes stop fallback switching");
   fallback.Cancel();int previousKeys=keys;
   Check(await fallback.Switch(a)==90&&await fallback.Create()==90&&keys==previousKeys,"closing pager cancels fallback without sending more input");
   foreach(string backend in new[]{"modern","2023-02-22-windows11","2023-11-10-windows11","2024-01-25-windows11"}){
    string extracted=NativePayload.Extract(backend);Check(File.Exists(extracted),"embedded helper verified: "+backend);NativePayload.Release();Check(!File.Exists(extracted),"helper cleaned up: "+backend);
   }
  }
  static List<Guid> Ids(){
   byte[] data=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops","VirtualDesktopIDs",null) as byte[];
   List<Guid> result=new List<Guid>();if(data!=null)for(int i=0;i<data.Length;i+=16){byte[] g=new byte[16];Array.Copy(data,i,g,0,16);result.Add(new Guid(g));}return result;
  }
  static async Task<int> Send(WorkerClient worker,string command){int result=await worker.Request(command);Console.WriteLine(command+" => "+result);return result;}
  static async Task Integration(Form host){
   List<Guid> original=Ids();List<Guid> added=new List<Guid>();Guid first=original[0];
   using(WorkerClient worker=new WorkerClient(Application.ExecutablePath, "--worker",6000)){
    try{
     Check(await Send(worker,"ping")==0,"relocated single executable initializes native payload");
     Guid originalActive=DesktopFallback.ReadCurrent();
     DesktopFallback keyboard=new DesktopFallback(DesktopFallback.ReadIds,DesktopFallback.ReadCurrent,delegate(ushort key){return Native.Shortcut(key,true);});
     try{
      int keyboardResult=await keyboard.Create();
      foreach(Guid g in Ids())if(!original.Contains(g))added.Add(g);
      Check(keyboardResult==0&&Ids().Count==original.Count+1&&DesktopFallback.ReadCurrent()==originalActive,"actual Windows shortcut creates desktop and restores original");
      Guid keyboardDesk=added[0];
      Check(await keyboard.Switch(keyboardDesk)==0&&await keyboard.Switch(originalActive)==0,"actual keyboard fallback switches to target and back");
      Check(await Send(worker,"remove "+keyboardDesk+" "+first)==0,"fallback test desktop removed safely");
      added.Clear();
     }finally{if(DesktopFallback.ReadCurrent()!=originalActive)Task.Run(delegate{return worker.Request("switch "+originalActive);}).GetAwaiter().GetResult();}
     Check(await Send(worker,"create")==0,"native new desktop creation");
     added.Add(Ids()[original.Count]);
     Check(await Send(worker,"create")==0,"second temporary desktop creation");
     added.Add(Ids()[original.Count+1]);
     Guid own;IDesktopManager manager=(IDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
     try{
      manager.GetWindowDesktopId(host.Handle,out own);
      Guid fixtureTarget=added[0];Check(manager.MoveWindowToDesktop(host.Handle,ref fixtureTarget)==0,"fixture window moves onto temporary source desktop");
      Check(await Send(worker,"remove "+added[0]+" "+added[1])==0,"source desktop removal confirms native success");
      Guid after;manager.GetWindowDesktopId(host.Handle,out after);
      Check(after==added[1],"removed desktop's open app survives on next desktop");
      Check(manager.MoveWindowToDesktop(host.Handle,ref own)==0,"test window restored to original desktop");
      Check(await Send(worker,"remove "+added[1]+" "+first)==0,"last desktop removal wraps to first desktop");
      Check(Ids().Count==original.Count&&Ids().TrueForAll(original.Contains),"original desktop count and identities restored");
     }finally{Marshal.ReleaseComObject(manager);}
    }finally{
     foreach(Guid g in added)if(Ids().Contains(g)){
      // Test cleanup uses the native API only on the two IDs we created.
      List<Guid> ids=Ids();int index=ids.IndexOf(g);Guid next=ids[(index+1)%ids.Count];Task.Run(delegate {return worker.Request("remove "+g+" "+next);}).GetAwaiter().GetResult();
     }
    }
   }
  }
  static bool Above(IntPtr front,IntPtr back){
   bool seen=false,above=false;Native.EnumWindows(delegate(IntPtr h,IntPtr p){if(h==front)seen=true;if(h==back){above=seen;return false;}return true;},IntPtr.Zero);return above;
  }
  static async Task Ui(Pager pager){
   await Task.Delay(500);
   Check(pager.TopMost&&(Native.GetWindowLong(pager.Handle,-20)&8)!=0,"pager is topmost by default");
   typeof(Pager).GetMethod("SetEmbedded",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pager,new object[]{true});
   await Task.Delay(500);
   Native.RECT taskbar;Native.GetWindowRect(Native.FindWindow("Shell_TrayWnd",null),out taskbar);
   Check(pager.Visible&&pager.Bounds.IntersectsWith(taskbar.Box),"integration creates visible compact pager within taskbar bounds");
   Check(Native.GetParent(pager.Handle)==IntPtr.Zero,"taskbar pager stays top-level above taskbar compositor");
   // Rival belongs to a different process, like Task Manager's always-on-top window.
   var info=new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--rival "+pager.Left+" "+pager.Top+" "+pager.Width+" "+pager.Height);
   info.UseShellExecute=false;
   using(var rival=System.Diagnostics.Process.Start(info)){
    try{
     await Task.Delay(1500);rival.Refresh();
     Check(rival.MainWindowHandle!=IntPtr.Zero&&Above(pager.Handle,rival.MainWindowHandle),"pager returns above a competing always-on-top program");
    }finally{if(!rival.HasExited){rival.CloseMainWindow();if(!rival.WaitForExit(1000))rival.Kill();}}
   }
   uint msg=(uint)typeof(Pager).GetField("appbarMessage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pager);
   typeof(Pager).GetMethod("WndProc",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pager,new object[]{Message.Create(pager.Handle,(int)msg,new IntPtr(2),new IntPtr(1))});
   Check((Native.GetWindowLong(pager.Handle,-20)&8)!=0,"fullscreen appbar notification keeps pager topmost");
   typeof(Pager).GetMethod("SetEmbedded",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pager,new object[]{false});
   Check(pager.Visible&&pager.TopMost,"turning integration off restores floating pager");
  }
  [STAThread] static int Main(string[] args){
   if(args.Length>0&&args[0]=="--worker"){MoveWorker.Serve();return 0;}
   if(args.Length==5&&args[0]=="--rival"){
    using(Form rival=new Form()){rival.Text="BioPager topmost rival";rival.TopMost=true;rival.StartPosition=FormStartPosition.Manual;rival.Bounds=new Rectangle(Int32.Parse(args[1]),Int32.Parse(args[2]),Int32.Parse(args[3]),Int32.Parse(args[4]));Application.Run(rival);return 0;}
   }
   try{
    Unit();FallbackTests().GetAwaiter().GetResult();
    if(args.Length>0&&args[0]=="--unit")return 0;
    Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length>0&&args[0]=="--preview"){
     string profile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-profile","settings.txt");
     Pager pager=new Pager(profile);pager.Text="BioPager 0.4.3 preview";
     pager.Shown+=delegate {SetWindowLong(pager.Handle,-20,(Native.GetWindowLong(pager.Handle,-20)&~0x80)|0x40000);Native.SetWindowPos(pager.Handle,new IntPtr(-1),0,0,0,0,0x33);};
     Application.Run(pager);return 0;
    }
    Exception failure=null;using(Form host=new Form()){
     host.Text="BioPager desktop migration test";host.ShowInTaskbar=false;host.Size=new Size(220,80);
     host.Shown+=async delegate {try{await Integration(host);}catch(Exception ex){failure=ex;}finally{host.Close();}};
     Application.Run(host);
    }
    if(failure!=null)throw failure;
    string uiFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-"+Guid.NewGuid().ToString("N"));
    using(Pager pager=new Pager(Path.Combine(uiFolder,"settings.txt"))){
     pager.Shown+=async delegate {try{await Ui(pager);}catch(Exception ex){failure=ex;}finally{pager.Close();}};
     Application.Run(pager);
    }
    Directory.Delete(uiFolder,true);
    if(failure!=null)throw failure;
    return 0;
   }catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}
  }
 }
}
