using System;
namespace BioPager {
 // Candidate names identify interface adapters, not accepted Windows builds.
 // Every adapter is validated in a separate worker; there is no OS whitelist.
 internal static class WindowsCompatibility {
  internal static string[] Candidates(){return new[]{"modern","2024-01-25-windows11","2023-11-10-windows11","2023-02-22-windows11","legacy21"};}
  internal static bool IsBackend(string value){return Array.IndexOf(Candidates(),value)>=0;}
 }
}
