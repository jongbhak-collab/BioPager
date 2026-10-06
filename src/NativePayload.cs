using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace BioPager {
 // Each worker owns a verified, private extraction. The lease protects live workers
 // from cleanup; version/hash names prevent mixing native code across releases.
 internal static class NativePayload {
  internal const string ExpectedHash="8740c572a1c000e3b87ffeb1e4c397eae9af3bd4a2abdc3bcffacab4493f8ff5";
  static string directory;
  static FileStream lease;
  internal static string Extract(string backend="modern") {
   string expected=ExpectedHash,resource="BioPager.VirtualDesktopAccessor.dll";
   switch(backend){
    case "modern":break;
    case "2023-02-22-windows11":expected="f6dc5f4acd7f1553769eda0a84e63fbc004e530132fe66fa69ae944fab8a234e";resource="BioPager."+backend+".dll";break;
    case "2023-11-10-windows11":expected="6fde6f5f409b026688f01ac44973a9d95fb37ae71632e4cbdd8bdd8c7f7c9c17";resource="BioPager."+backend+".dll";break;
    case "2024-01-25-windows11":expected="f78ff6334f6c0ef5175ec0819026cec31d421a564b9ed1ee1ac4b6ed98d4f999";resource="BioPager."+backend+".dll";break;
    default:throw new InvalidDataException("Unknown native backend.");
   }
   string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BioPager","NativeRuntime");
   Directory.CreateDirectory(root);
   Cleanup(root);
   directory=Path.Combine(root,"v0.4.5-"+expected+"-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(directory);
   try {
    lease=new FileStream(Path.Combine(directory,"lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
    string path=Path.Combine(directory,"VirtualDesktopAccessor.dll");
    using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
     if(source==null)throw new FileNotFoundException("Embedded native helper is missing.");
     using(FileStream target=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))source.CopyTo(target);
    }
    using(SHA256 hash=SHA256.Create())using(FileStream file=File.OpenRead(path)) {
     if(BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=expected)
      throw new InvalidDataException("Embedded native helper failed verification.");
    }
    return path;
   } catch {Release();throw;}
  }
  internal static void Release() {
   if(lease!=null){lease.Dispose();lease=null;}
   if(directory!=null){TryDelete(directory);directory=null;}
  }
  static void Cleanup(string root) {
   foreach(string path in Directory.GetDirectories(root,"v*")) {
    try {
     if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
     if(Directory.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-1))continue;
     // A crashed worker leaves an unlocked lease. Never touch an active worker.
     using(new FileStream(Path.Combine(path,"lease"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
      File.Delete(Path.Combine(path,"VirtualDesktopAccessor.dll"));
     }
     TryDelete(path);
    } catch(IOException) {} catch(UnauthorizedAccessException) {}
   }
  }
  static void TryDelete(string path) {
   try {
    // Only our two known files; never recursively delete an arbitrary directory.
    File.Delete(Path.Combine(path,"VirtualDesktopAccessor.dll"));
    File.Delete(Path.Combine(path,"lease"));
    Directory.Delete(path,false);
   } catch(IOException) {} catch(UnauthorizedAccessException) {}
  }
 }
}
