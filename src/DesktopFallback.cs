using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
namespace BioPager {
 // Windows' keyboard shortcuts do not depend on undocumented COM vtables.
 // Every step is acknowledged from Explorer before the next input is sent.
 internal sealed class DesktopFallback {
  readonly Func<List<Guid>> ids;
  readonly Func<Guid> current;
  readonly Func<ushort,bool> shortcut;
  volatile bool cancelled;
  internal void Cancel(){cancelled=true;}
  internal DesktopFallback(Func<List<Guid>> ids,Func<Guid> current,Func<ushort,bool> shortcut){this.ids=ids;this.current=current;this.shortcut=shortcut;}
  internal static List<Guid> ReadIds(){
   byte[] value=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops","VirtualDesktopIDs",null) as byte[];
   List<Guid> result=new List<Guid>();
   if(value!=null&&value.Length%16==0)for(int i=0;i<value.Length;i+=16){byte[] part=new byte[16];Array.Copy(value,i,part,0,16);Guid g=new Guid(part);if(g==Guid.Empty||result.Contains(g))return new List<Guid>();result.Add(g);}
   return result;
  }
  internal static Guid ReadCurrent(){
   string root=@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\";
   byte[] value=Registry.GetValue(root+"VirtualDesktops","CurrentVirtualDesktop",null) as byte[];
   if(value==null||value.Length!=16)value=Registry.GetValue(root+"SessionInfo\\"+Process.GetCurrentProcess().SessionId+"\\VirtualDesktops","CurrentVirtualDesktop",null) as byte[];
   return value!=null&&value.Length==16?new Guid(value):Guid.Empty;
  }
  static bool Same(List<Guid> a,List<Guid> b){if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(a[i]!=b[i])return false;return true;}
  internal async Task<int> Switch(Guid target){
   if(cancelled)return 90;
   List<Guid> layout=ids();int to=layout.IndexOf(target),from=layout.IndexOf(current());
   if(to<0||from<0)return 12;
   int direction=to>from?1:-1;
   while(from!=to){
    if(cancelled)return 90;
    if(!Same(layout,ids())||current()!=layout[from])return 12;
    Guid next=layout[from+direction];
    if(!shortcut((ushort)(direction>0?0x27:0x25)))return 31;
    bool done=false;for(int n=0;n<60;n++){if(cancelled)return 90;if(!Same(layout,ids()))return 12;if(current()==next){done=true;break;}await Task.Delay(20);}
    if(!done)return 26;
    if(!Same(layout,ids()))return 12;
    from+=direction;
   }
   return 0;
  }
  internal async Task<int> Create(){
   if(cancelled)return 90;
   List<Guid> before=ids();Guid original=current();
   if(before.Count<1||!before.Contains(original))return 12;
   if(!shortcut(0x44))return 31;
   for(int n=0;n<75;n++){
    if(cancelled)return 90;
    List<Guid> after=ids();
    if(after.Count==before.Count+1){
     for(int i=0;i<before.Count;i++)if(after[i]!=before[i])return 12;
     if(before.Contains(after[before.Count]))return 12;
     // Acknowledge the shortcut's switch before restoring the old desktop.
     if(current()!=after[before.Count]){await Task.Delay(20);continue;}
     return await Switch(original);
    }
    if(!Same(before,after))return 12;
    await Task.Delay(20);
   }
   return 28; // No retry: a delayed Windows operation may still have succeeded.
  }
  internal async Task<int> Ensure(int minimum){
   if(cancelled)return 90;
   if(minimum<1||minimum>32)return 10;
   while(ids().Count<minimum){int result=await Create();if(result!=0)return result;}
   return 0;
  }
  internal static bool CanFallback(int code){return code==20||code==23||code==40||code==91;}
 }
}
