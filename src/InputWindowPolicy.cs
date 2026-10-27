// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.IO;

namespace BioPager {
 // This is a pager inclusion policy, not a security or signature validator.
 // Never terminate, disable, or change the visibility of the Windows component.
 internal static class InputWindowPolicy {
  const string CoreWindow="Windows.UI.Core.CoreWindow";
  const int AppWindow=0x00040000;
  const uint Popup=0x80000000,Child=0x40000000,Caption=0x00C00000,ThickFrame=0x00040000;

  internal static string ExclusionReason(string className,string title,string processName,string imagePath,
                                       string windowsDirectory,Guid desktop,int style,int extendedStyle,int cloak) {
   // CoreWindow alone is not a reason to exclude: Settings uses it as well.
   if(!String.Equals(className,CoreWindow,StringComparison.Ordinal))return null;
   if(IsInstalledInputHost(processName,imagePath,windowsDirectory))
    return "Windows input helper (recognized system-app executable)";

   // If executable information contradicts the classification, keep the app.
   // Only the tightly constrained signature reported by the user is a fallback
   // when querying the process image was unavailable. This is logged separately.
   if(!String.IsNullOrWhiteSpace(imagePath))return null;
   if(!String.IsNullOrWhiteSpace(processName)&&!IsInputHostName(processName))return null;
   if(desktop!=Guid.Empty||(extendedStyle&AppWindow)!=0||(cloak&2)==0)return null;
   uint flags=unchecked((uint)style);
   if((flags&Popup)==0||(flags&(Child|Caption|ThickFrame))!=0)return null;
   string caption=(title??String.Empty).Trim();
   if(caption=="Windows 입력 환경"||caption=="Windows Input Experience")
    return "Windows input helper (caption/class fallback; executable path unavailable)";
   return null;
  }

  static bool IsInputHostName(string name) {
   return String.Equals(name,"TextInputHost",StringComparison.OrdinalIgnoreCase)||
          String.Equals(name,"InputApp",StringComparison.OrdinalIgnoreCase);
  }

  internal static bool IsInstalledInputHost(string processName,string imagePath,string windowsDirectory) {
   if(!IsInputHostName(processName)||String.IsNullOrWhiteSpace(imagePath)||
      String.IsNullOrWhiteSpace(windowsDirectory))return false;
   try {
    if(!Path.IsPathRooted(imagePath)||!Path.IsPathRooted(windowsDirectory))return false;
    string path=Path.GetFullPath(imagePath);
    string root=Path.GetFullPath(windowsDirectory).TrimEnd('\\','/')+"\\SystemApps\\";
    if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
    string relative=path.Substring(root.Length);
    // Exact locations prevent similarly named applications elsewhere from being
    // hidden. Unrecognized future package locations fail open, not closed.
    string[] textHosts={
     @"MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe",
     @"MicrosoftWindows.Client.CBS_cw5n1h2txyewy\InputApp\TextInputHost.exe",
     @"MicrosoftWindows.Client.Core_cw5n1h2txyewy\TextInputHost.exe",
     @"MicrosoftWindows.Client.Core_cw5n1h2txyewy\InputApp\TextInputHost.exe",
     @"InputApp_cw5n1h2txyewy\TextInputHost.exe"
    };
    if(String.Equals(processName,"TextInputHost",StringComparison.OrdinalIgnoreCase)) {
     foreach(string location in textHosts)
      if(String.Equals(relative,location,StringComparison.OrdinalIgnoreCase))return true;
    }
    return String.Equals(processName,"InputApp",StringComparison.OrdinalIgnoreCase)&&
           String.Equals(relative,@"InputApp_cw5n1h2txyewy\InputApp.exe",StringComparison.OrdinalIgnoreCase);
   }catch(ArgumentException){return false;}
    catch(NotSupportedException){return false;}
    catch(PathTooLongException){return false;}
    catch(System.Security.SecurityException){return false;}
  }
 }
}
