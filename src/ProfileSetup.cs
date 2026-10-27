using System;
using System.IO;
namespace BioPager {
 // Separate onboarding state survives settings saves and failed native startup.
 internal sealed class ProfileSetup {
  readonly string marker;
  internal bool Pending {get;private set;}
  internal bool Unstarted {get;private set;}
  internal ProfileSetup(string directory,bool existingSettings) {
   marker=Path.Combine(directory,"initial-desktops-v1.txt");
   if(File.Exists(marker)){string state=File.ReadAllText(marker).Trim();Pending=state=="pending"||state=="new";Unstarted=state=="new";return;}
   Pending=!existingSettings;
   Unstarted=Pending;
   Directory.CreateDirectory(directory);File.WriteAllText(marker,Pending?"new":"existing");
  }
  internal void Begin(){File.WriteAllText(marker,"pending");Unstarted=false;}
  internal void Complete(){File.WriteAllText(marker,"complete");Pending=false;}
 }
}
