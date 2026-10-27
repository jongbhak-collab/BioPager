// Test-only synthetic application. Never included in the distributed installer.
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#if UNRELATED
[assembly: AssemblyProduct("Unrelated application")]
[assembly: AssemblyCompany("Unrelated publisher")]
#else
[assembly: AssemblyProduct("BioPager")]
[assembly: AssemblyCompany("Jong Bhak")]
#endif
static class Fixture {
 [STAThread] static void Main(string[] args) {
  Application.EnableVisualStyles();
  using(Form window=new Form()) {
   window.Text="BioPager";
   window.Width=320;window.Height=120;
   bool block=Array.IndexOf(args,"--block")>=0;
   string marker=args.Length>0?args[0]:null;
   window.FormClosing+=delegate(object sender,FormClosingEventArgs e){
    if(block){e.Cancel=true;return;}
    if(marker!=null)File.WriteAllText(marker,"graceful close");
   };
#if UNRELATED
   Application.Run(window);
#else
   using(Mutex mutex=new Mutex(true,@"Local\VernStylePager_01"))Application.Run(window);
#endif
  }
 }
}
