using System;
using System.IO;
namespace BioPager {
 // Separate onboarding state survives settings saves and failed native startup.
 internal sealed class ProfileSetup {
  readonly string marker;
  internal bool Pending {get;private set;}
  internal ProfileSetup(string directory,bool existingSettings) {
   marker=Path.Combine(directory,"initial-desktops-v1.txt");
   if(File.Exists(marker)){Pending=File.ReadAllText(marker).Trim()=="pending";return;}
   Pending=!existingSettings;
   if(Pending){Directory.CreateDirectory(directory);File.WriteAllText(marker,"pending");}
  }
  internal void Complete(){File.WriteAllText(marker,"complete");Pending=false;}
 }
}
