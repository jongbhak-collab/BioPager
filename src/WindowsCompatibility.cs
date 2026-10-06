using System;
using Microsoft.Win32;
namespace BioPager {
 internal static class WindowsCompatibility {
  internal static string Select(int build,int revision) {
   if(build==22000)return "legacy21";
   if(build==22621||build==22631){
    if(revision>=3085)return "2024-01-25-windows11";
    if(revision>=2215)return "2023-11-10-windows11";
    if(build==22621)return "2023-02-22-windows11";
   }
   if((build==26100&&revision>=2605)||build==26200)return "modern";
   // Never guess the vtable on Insider or future builds.
   return null;
  }
  internal static string Backend() {
   using(RegistryKey key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){
    int build,revision;
    if(key==null||!Int32.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build))return null;
    Int32.TryParse(Convert.ToString(key.GetValue("UBR")),out revision);
    return Select(build,revision);
   }
  }
 }
}
