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
  static int FakeCurrent(){return 0;}
  static int FakeCreate(){creates++;return fakeCount++;}
  static Guid FakeId(int i){return i==0?fakeSource:i==1?fakeNext:new Guid(i,0,0,new byte[8]);}
  static int FakeRemove(int i,int t){if(refuse)return -1;fakeCount--;return 1;}
  static void Bind(string field,string method){FieldInfo f=typeof(MoveWorker).GetField(field,BindingFlags.Static|BindingFlags.NonPublic);f.SetValue(null,Delegate.CreateDelegate(f.FieldType,typeof(Tests).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)));}
  static object Call(string method,params object[] args){return typeof(MoveWorker).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
  static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
  static void Unit(){
   PreviewTests.Unit();
   Check(WindowsCompatibility.Candidates().Length==5&&WindowsCompatibility.Candidates()[0]=="modern","adapter candidates are independent of Windows build numbers");
   Check(!WindowsCompatibility.IsBackend("unexpected")&&WindowsCompatibility.IsBackend("legacy21"),"only embedded known adapters may be probed");
   foreach(int code in new[]{12,22,23,28,40,91,93})Check(!DesktopFallback.CanFallback(code),"no automatic shortcut after uncertain result "+code);
   Check(Marshal.SizeOf(typeof(Native.INPUT))==40,"x64 SendInput uses the complete 40-byte INPUT union");
   Bind("count","FakeCount");Bind("createDesktop","FakeCreate");Bind("id","FakeId");Bind("removeDesktop","FakeRemove");
   Bind("currentNumber","FakeCurrent");fakeCount=1;
   Check((int)Call("VerifyEnumerationAgainst",new List<Guid>())==0,"missing fresh-account registry IDs permit consistent native OS enumeration");
   Check((int)Call("VerifyEnumerationAgainst",new List<Guid>{Guid.NewGuid()})==23,"conflicting registry identities reject the adapter");
   Check((int)Call("VerifyEnumerationAgainst",new List<Guid>{fakeSource,fakeNext})==23,"conflicting registry counts reject the adapter");
   fakeCount=1;creates=0;
   Check((int)Call("EnsureMinimum",7)==0&&fakeCount==7&&creates==6,"first-use setup adds six to a one-desktop profile");
   Check((int)Call("EnsureMinimum",7)==0&&creates==6,"repeated setup does not recreate desktops");
   fakeCount=10;Check((int)Call("EnsureMinimum",7)==0&&fakeCount==10,"existing larger layouts are preserved");
   fakeCount=1;Check((int)Call("Remove",fakeSource,fakeNext)==27&&fakeCount==1,"last desktop cannot be removed");
   fakeCount=3;Check((int)Call("Remove",fakeSource,FakeId(2))==12&&fakeCount==3,"stale or non-next target rejected before removal");
   refuse=true;Check((int)Call("Remove",fakeSource,fakeNext)==29&&fakeCount==3,"Windows refusal does not report success");refuse=false;
   string folder=Path.Combine(Path.GetTempPath(),"BioPagerProfileTest-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   try{
    ProfileSetup profile=new ProfileSetup(folder,false);Check(profile.Pending&&profile.Unstarted,"fresh profile is distinct from an attempted setup");
    profile.Begin();Check(!new ProfileSetup(folder,false).Unstarted,"setup intent is persisted before OS mutation");
    File.WriteAllText(Path.Combine(folder,"settings.txt"),"saved");
    Check(new ProfileSetup(folder,true).Pending,"failed first setup remains pending after settings save");
    profile.Complete();Check(!new ProfileSetup(folder,true).Pending,"completed setup does not run again");
    string legacy=Path.Combine(folder,"legacy");Check(!new ProfileSetup(legacy,true).Pending,"upgrading existing users skips default desktop creation");
    Check(!new ProfileSetup(legacy,false).Pending,"established profile marker survives missing settings");
    string corrupt=Path.Combine(folder,"corrupt");Directory.CreateDirectory(corrupt);File.WriteAllText(Path.Combine(corrupt,"initial-desktops-v1.txt"),"unknown");
    Check(!new ProfileSetup(corrupt,false).Pending,"unrecognized setup state never creates desktops");
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
   Check(await fallback.Ensure(7)==0&&layout.Count==7&&active==c,"fallback fresh profile reaches seven desks");
   int beforeKeys=keys;Check(await fallback.Ensure(7)==0&&keys==beforeKeys,"completed fallback sends no extra shortcut");
   DesktopFallback unconfirmed=new DesktopFallback(delegate{return new List<Guid>(layout);},delegate{return active;},delegate(ushort key){keys++;return true;});
   beforeKeys=keys;Check(await unconfirmed.Ensure(8)==28&&keys==beforeKeys+1,"unacknowledged creation sends exactly one shortcut and stops");
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
  static async Task ProbeTests(){
   string marker=Path.Combine(Path.GetTempPath(),"BioPager-probe-"+Guid.NewGuid().ToString("N")+".txt");
   try{
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath,"--probe-test \""+marker+"\"",1500)){
     Check(await worker.Request("ping")==0&&worker.SelectedBackend=="2024-01-25-windows11","failed first adapter falls through to a working adapter");
     Check(await worker.Request("mutate-crash")==40,"native crash is isolated and reported");
     Check(File.ReadAllLines(marker).Length==1,"ambiguous mutation is never replayed");
     Check(await worker.Request("ping")==0,"worker reconnects and reprobes after a crash");
    }
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath,"--probe-none",1500)){
     Check(await worker.Request("create")==20,"no operation sent when all read-only probes fail");
     Check(await worker.Request("ping")==20,"failed probes have a bounded cooldown");
    }
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath,"--probe-badpayload",1500))
     Check(await worker.Request("ping")==22,"payload verification failure is not hidden by another adapter");
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath,"--probe-mutation \""+marker+"\"",1500)){
     Check(await worker.Request("create")==93,"crash after create is an uncertain mutation, not fallback eligibility");
     Check(File.ReadAllLines(marker).Length==2,"create is attempted once across all adapters");
    }
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath,"--probe-cap",1500)){
     Check(await worker.Request("switch "+Guid.NewGuid())==0&&worker.SelectedBackend=="modern","switch uses an adapter with the switch capability");
     Check(await worker.Request("create")==0&&worker.SelectedBackend=="2024-01-25-windows11","missing create capability tries a different adapter before mutation");
     Check(await worker.Request("remove source target")==20,"unavailable remove capability fails without a mutation");
     Check(await worker.Request("switch "+Guid.NewGuid())==0,"unavailable removal does not disable desktop switching");
    }
   }finally{File.Delete(marker);}
  }
  static List<Guid> Ids(){
   byte[] data=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops","VirtualDesktopIDs",null) as byte[];
   List<Guid> result=new List<Guid>();if(data!=null)for(int i=0;i<data.Length;i+=16){byte[] g=new byte[16];Array.Copy(data,i,g,0,16);result.Add(new Guid(g));}return result;
  }
  static async Task<int> Send(WorkerClient worker,string command){int result=await worker.Request(command);Console.WriteLine(command+" => "+result);return result;}
  static int OnboardingCi(){
   if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new InvalidOperationException("Fresh-profile OS test is restricted to an isolated GitHub runner.");
   List<Guid> original=DesktopFallback.ReadIds();
   Check(original.Count==1,"isolated runner starts with one desktop");
   string folder=Path.Combine(Path.GetTempPath(),"BioPager-onboarding-"+Guid.NewGuid().ToString("N"));
   string settings=Path.Combine(folder,"settings.txt");Exception failure=null;
   try{
    using(Pager pager=new Pager(settings)){
     pager.Shown+=async delegate {
      try{
       string marker=Path.Combine(folder,"initial-desktops-v1.txt");
       for(int n=0;n<600&&File.ReadAllText(marker).Trim()!="complete";n++)await Task.Delay(100);
       Check(File.ReadAllText(marker).Trim()=="complete"&&Ids().Count==7,"real fresh-profile launch is OS-confirmed at exactly seven desktops");
       Check(Ids().Contains(original[0]),"first-launch setup keeps the original desktop identity");
      }catch(Exception ex){failure=ex;}finally{pager.Close();}
     };Application.Run(pager);
    }
    if(failure!=null)throw failure;
    List<Guid> seven=Ids();
    using(Pager pager=new Pager(settings)){
     pager.Shown+=async delegate {await Task.Delay(2000);try{Check(Ids().Count==7&&Ids().TrueForAll(seven.Contains),"existing profile relaunch preserves all seven desktop identities");}catch(Exception ex){failure=ex;}finally{pager.Close();}};
     Application.Run(pager);
    }
    if(failure!=null)throw failure;
    string established=Path.Combine(folder,"established","settings.txt");
    using(Pager pager=new Pager(established)){
     pager.Shown+=async delegate {await Task.Delay(2000);try{Check(Ids().Count==7&&!new ProfileSetup(Path.GetDirectoryName(established),false).Pending,"established workspace without BioPager settings is preserved");}catch(Exception ex){failure=ex;}finally{pager.Close();}};
     Application.Run(pager);
    }
    if(failure!=null)throw failure;
    return 0;
   }finally{
    // Only remove identities absent before this isolated test. Never remove originals.
    using(WorkerClient worker=new WorkerClient(Application.ExecutablePath)){
     foreach(Guid g in Ids())if(!original.Contains(g)){
      List<Guid> ids=Ids();int index=ids.IndexOf(g);
      int result=Task.Run(delegate{return worker.Request("remove "+g+" "+ids[(index+1)%ids.Count]);}).GetAwaiter().GetResult();
      Check(result==0,"isolated onboarding test removes only its own temporary desktop");
     }
    }
    Check(Ids().Count==original.Count&&Ids().TrueForAll(original.Contains),"onboarding test restores the runner's original layout");
    if(Directory.Exists(folder))Directory.Delete(folder,true);
   }
  }
  static async Task Integration(Form host){
   await Task.Delay(500); // Let Explorer register the normal fixture app window.
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
      uint fixturePid;Native.GetWindowThreadProcessId(host.Handle,out fixturePid);
      Check(await Send(worker,"move "+host.Handle.ToInt64()+" "+fixturePid+" "+own+" "+added[0])==0,"direct worker app dragging moves normal fixture window");
      Guid dragged;manager.GetWindowDesktopId(host.Handle,out dragged);Check(dragged==added[0],"dragging destination independently confirmed");
      Check(await Send(worker,"move "+host.Handle.ToInt64()+" "+fixturePid+" "+added[0]+" "+own)==0,"worker dragging restores fixture");
      try{
       Check(await Send(worker,"switch "+added[1])==0&&DesktopFallback.ReadCurrent()==added[1],"direct switch on live Windows confirmed");
       Check(await Send(worker,"switch "+originalActive)==0&&DesktopFallback.ReadCurrent()==originalActive,"direct switch restores original desktop");
      }finally{if(DesktopFallback.ReadCurrent()!=originalActive)Task.Run(delegate{return worker.Request("switch "+originalActive);}).GetAwaiter().GetResult();}
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
   if(args.Length>0&&args[0].StartsWith("--probe-")){
    string line;while((line=Console.ReadLine())!=null){
     if(args[0]=="--probe-none")Console.WriteLine(line=="ping"?23:99);
     else if(args[0]=="--probe-mutation"&&line=="create"){File.AppendAllText(args[1],"create\n");Environment.Exit(77);}
     else if(args[0]=="--probe-mutation")Console.WriteLine(0);
     else if(args[0]=="--probe-badpayload")Console.WriteLine(22);
     else if(args[0]=="--probe-cap")Console.WriteLine(line=="supports remove"||(line=="supports create"&&args[args.Length-1]=="modern")?20:0);
     else if(args[args.Length-1]=="modern")Console.WriteLine(23);
     else if(line=="mutate-crash"){File.AppendAllText(args[1],"mutation\n");Environment.Exit(77);}
     else Console.WriteLine(0);
    }return 0;
   }
   if(args.Length>0&&args[0]=="--worker"){MoveWorker.Serve(args.Length==2?args[1]:"modern");return 0;}
   if(args.Length==5&&args[0]=="--rival"){
    using(Form rival=new Form()){rival.Text="BioPager topmost rival";rival.TopMost=true;rival.StartPosition=FormStartPosition.Manual;rival.Bounds=new Rectangle(Int32.Parse(args[1]),Int32.Parse(args[2]),Int32.Parse(args[3]),Int32.Parse(args[4]));Application.Run(rival);return 0;}
   }
   try{
    Unit();FallbackTests().GetAwaiter().GetResult();ProbeTests().GetAwaiter().GetResult();
    if(args.Length>0&&args[0]=="--unit")return 0;
    Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length>0&&args[0]=="--onboarding-ci")return OnboardingCi();
    if(args.Length>0&&args[0]=="--window-tests")return PreviewTests.RunWindows();
    if(args.Length>0&&args[0]=="--preview"){
     string profile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-profile","settings.txt");
     Pager pager=new Pager(profile);pager.Text="BioPager 0.4.3 preview";
     pager.Shown+=delegate {SetWindowLong(pager.Handle,-20,(Native.GetWindowLong(pager.Handle,-20)&~0x80)|0x40000);Native.SetWindowPos(pager.Handle,new IntPtr(-1),0,0,0,0,0x33);};
     Application.Run(pager);return 0;
    }
    Exception failure=null;using(Form host=new Form()){
     host.Text="BioPager desktop migration test";host.ShowInTaskbar=true;host.Size=new Size(220,80);
     host.Shown+=async delegate {try{await Integration(host);}catch(Exception ex){failure=ex;}finally{host.Close();}};
     Application.Run(host);
    }
    if(failure!=null)throw failure;
    string uiFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(uiFolder);File.WriteAllText(Path.Combine(uiFolder,"settings.txt"),"");
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


