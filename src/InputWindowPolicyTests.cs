// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// Sanitized regression fixtures only; no user diagnostics or desktop contents.
using System;
using System.Collections.Generic;

namespace BioPager {
 internal static class InputWindowPolicyTests {
  const string Core="Windows.UI.Core.CoreWindow";
  const string Root=@"C:\Windows";
  const string Installed=@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe";
  const int Popup=unchecked((int)0x94000000),Ex=0x00200000;
  static int checks;
  static readonly Guid Desktop=new Guid("11111111-1111-1111-1111-111111111111");
  static void Check(bool passed,string message) {
   if(!passed)throw new InvalidOperationException(message);
   checks++;Console.WriteLine("PASS: "+message);
  }
  static bool Hidden(string cls,string title,string process,string image,Guid desktop,int style,int ex,int cloak) {
   return InputWindowPolicy.ExclusionReason(cls,title,process,image,Root,desktop,style,ex,cloak)!=null;
  }
  static bool Fallback(string title,string process,string image,Guid desktop,int style,int ex,int cloak) {
   return Hidden(Core,title,process,image,desktop,style,ex,cloak);
  }
  static void PolicyTests() {
   Check(Hidden(Core,"Windows 입력 환경","TextInputHost",Installed,Guid.Empty,Popup,Ex,2),"recognized Windows input executable is omitted from the pager");
   Check(Hidden(Core,"Different localized caption","TextInputHost",Installed,Desktop,Popup,Ex,0),"recognized input host does not depend on caption, cloaking or unresolved desktop");
   Check(InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed.Replace("\\TextInputHost.exe","\\InputApp\\TextInputHost.exe"),Root),"nested Windows input-host location is recognized");
   Check(InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed.Replace(".CBS_",".Core_"),Root),"Core package input-host location is recognized");
   Check(InputWindowPolicy.IsInstalledInputHost("InputApp",@"C:\Windows\SystemApps\InputApp_cw5n1h2txyewy\InputApp.exe",Root),"legacy InputApp executable is recognized");
   Check(InputWindowPolicy.IsInstalledInputHost("TextInputHost",@"C:\Windows\SystemApps\InputApp_cw5n1h2txyewy\TextInputHost.exe",Root),"legacy package TextInputHost location is recognized");
   Check(InputWindowPolicy.IsInstalledInputHost("TEXTINPUTHOST",Installed.ToUpperInvariant(),Root.ToUpperInvariant()),"executable and Windows paths are case-insensitive");
   Check(InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed.Replace(@"C:\Windows",@"D:\WinNT"),@"D:\WinNT"),"Windows directory is not hard-coded to drive C");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",@"C:\Users\Example\TextInputHost.exe",Root),"same executable name outside Windows system apps remains visible");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed.Replace(".CBS_",".Unexpected_"),Root),"unrecognized package location fails open");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed.Replace("Windows\\","Windows.old\\"),Root),"a sibling Windows directory is not accepted by prefix matching");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed+".other",Root),"a longer executable name is not accepted by substring matching");
   Check(!InputWindowPolicy.IsInstalledInputHost("OtherApp",Installed,Root),"contradictory process name is retained");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\..\Other\TextInputHost.exe",Root),"path traversal cannot impersonate a recognized package");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",@"SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe",Root),"relative executable path fails open");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost",Installed,""),"missing Windows directory fails open");
   Check(!InputWindowPolicy.IsInstalledInputHost(null,null,null),"missing process metadata is safe");
   Check(!InputWindowPolicy.IsInstalledInputHost("TextInputHost","bad\0path",Root),"invalid process path fails open without throwing");
   Check(Fallback("Windows 입력 환경","","",Guid.Empty,Popup,Ex,2),"reported Korean input-window signature is filtered when metadata is unavailable");
   Check(Fallback("Windows Input Experience","","",Guid.Empty,Popup,Ex,2),"English input-window signature has the same narrow fallback");
   Check(Fallback("Windows 입력 환경","TextInputHost","",Guid.Empty,Popup,Ex,2),"known input process name with unavailable image path permits fallback");
   Check(!Fallback("Windows 입력 환경","OtherApp","",Guid.Empty,Popup,Ex,2),"known unrelated process name prevents caption fallback");
   Check(!Fallback("Windows 입력 환경","TextInputHost",@"C:\Users\Example\TextInputHost.exe",Guid.Empty,Popup,Ex,2),"known unrelated image path prevents caption fallback");
   Check(!Fallback("Windows 입력 환경","","",Desktop,Popup,Ex,2),"a known desktop prevents caption-only classification");
   Check(!Fallback("Windows 입력 환경","","",Guid.Empty,Popup,Ex|0x40000,2),"explicit taskbar app styling prevents caption fallback");
   Check(!Fallback("Windows 입력 환경","","",Guid.Empty,Popup|0x00C00000,Ex,2),"normal captioned window is not removed by fallback");
   Check(!Fallback("Windows 입력 환경","","",Guid.Empty,Popup|0x00040000,Ex,2),"resizable app window is not removed by fallback");
   Check(!Fallback("Windows 입력 환경","","",Guid.Empty,0x14000000,Ex,2),"a non-popup application is not removed by fallback");
   Check(!Fallback("Windows 입력 환경","","",Guid.Empty,Popup,Ex,0),"active uncloaked unknown window is not removed by fallback");
   Check(!Fallback("Windows 입력 환경 - Notes","","",Guid.Empty,Popup,Ex,2),"similar document caption is not removed by substring matching");
   Check(!Fallback("설정","SystemSettings",@"C:\Windows\ImmersiveControlPanel\SystemSettings.exe",Desktop,Popup,Ex,2),"Settings with identical CoreWindow class and shell cloaking stays visible");
   Check(!Fallback("Other application","","",Guid.Empty,Popup,Ex,2),"genuine unresolved CoreWindow remains in Other apps");
   Check(!Fallback("","","",Guid.Empty,Popup,Ex,2),"untitled unresolved window is not broadly removed");
   Check(!Hidden("Chrome_WidgetWin_1","Windows 입력 환경","","",Guid.Empty,Popup,Ex,2),"browser window is not removed for matching the helper caption");
   Check(!Hidden("Notepad","Windows 입력 환경","TextInputHost",Installed,Guid.Empty,Popup,Ex,2),"process identity alone never removes a different window class");
   foreach(string app in new[]{"ChatGPT","Gemini","Claude"})
    Check(!Hidden("Chrome_WidgetWin_1",app,app,"",Desktop,0x14C70000,0x00200100,2),app+" on an inactive desktop stays visible");
   string reason=InputWindowPolicy.ExclusionReason(Core,"Windows 입력 환경","","",Root,Guid.Empty,Popup,Ex,2);
   Check(reason!=null&&reason.Contains("fallback")&&reason.Contains("unavailable"),"diagnostics distinguish fallback classification from executable identification");
  }
  static void ReplaySanitizedScan() {
   // Preserve the classes and shapes of the 16 included windows in the report,
   // without publishing private titles, HWNDs, PIDs, account names or desktop IDs.
   string[] classes={"Chrome_WidgetWin_1","Chrome_WidgetWin_1",Core,"MozillaWindowClass","Chrome_WidgetWin_1",
    "MozillaWindowClass","ApplicationFrameWindow","Notepad","Chrome_WidgetWin_1","Qt6112QWindowIcon",
    "CabinetWClass","EVA_Window_Dblclk","MozillaWindowClass",Core,"CabinetWClass","EVA_Window_Dblclk"};
   string[] titles={"ChatGPT","Gemini","Windows 입력 환경","Mail","Claude","Compose","Settings","Notes",
    "Browser","Telegram","Files","Chat","Compose", "설정","Files","KakaoTalk"};
   int kept=0,unknown=0,removed=0;
   for(int i=0;i<classes.Length;i++) {
    Guid desktop=i==2?Guid.Empty:Desktop;
    if(Hidden(classes[i],titles[i],"","",desktop,Popup,Ex,2)){removed++;continue;}
    kept++;if(desktop==Guid.Empty)unknown++;
   }
   Check(removed==1&&kept==15,"sanitized diagnostic replay removes only the single input helper from 16 included windows");
   Check(unknown==0,"reported scan no longer requires an Other apps tile after targeted filtering");
  }
  static int Main() {
   try{PolicyTests();ReplaySanitizedScan();Console.WriteLine("Input-window policy checks passed: "+checks);return 0;}
   catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}
  }
 }
}
