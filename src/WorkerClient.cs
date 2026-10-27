// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace BioPager {
 internal sealed class WorkerClient:IDisposable {
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  Process process;
  volatile bool disposed;
  readonly string executable,arguments;
  readonly int timeout;
  string selected;
  DateTime retryAfter;
  readonly Dictionary<string,DateTime> capabilityRetry=new Dictionary<string,DateTime>();
  internal string SelectedBackend {get{return selected;}}
  public WorkerClient(string executable,string arguments="--worker",int timeout=4000){this.executable=executable;this.arguments=arguments;this.timeout=timeout;}
  public async void Warm(){await Request("ping");}
  public async Task<int> Request(string command){
   await gate.WaitAsync();
   try{
    if(disposed)return 90;
    string capability=Capability(command);DateTime next;
    if(capability!=null&&capabilityRetry.TryGetValue(capability,out next)&&DateTime.UtcNow<next)return 20;
    if(process==null||process.HasExited){
     int ready=await Connect(capability);if(ready!=0)return ready;
    }else if(capability!=null){
     int available=await Exchange("supports "+capability);
     if(available!=0){
      if(available==21||available==22)return available;
      int ready=await Connect(capability);if(ready!=0)return ready;
     }
    }
    if(disposed)return 90;
    // Never replay a mutation after a timeout/crash. Windows may have applied it.
    int code=await Exchange(command);
    if(code==23||code==40||code==91){Stop();if(capability!=null)return 93;}
    return code;
   }catch{Stop();return 40;}finally{gate.Release();}
  }
  static string Capability(string command){
   string name=command.Split(' ')[0];
   if(name=="ensure")return "create";
   return name=="switch"||name=="move"||name=="create"||name=="remove"?name:null;
  }
  async Task<int> Connect(string capability){
   Stop();if(DateTime.UtcNow<retryAfter)return 20;
   bool anyEnumerated=false;
   string[] candidates=WindowsCompatibility.Candidates();
   if(selected!=null){int i=Array.IndexOf(candidates,selected);if(i>0){string first=candidates[0];candidates[0]=selected;candidates[i]=first;}}
   foreach(string candidate in candidates){
    if(disposed)return 90;
    try{
     ProcessStartInfo start=new ProcessStartInfo(executable,arguments+" "+candidate);
     start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardInput=true;start.RedirectStandardOutput=true;
     start.StandardOutputEncoding=new UTF8Encoding(false);start.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;
     process=Process.Start(start);
     if(disposed){Stop();return 90;}
     int code=await Exchange("ping");
     if(code==0){anyEnumerated=true;if(capability!=null)code=await Exchange("supports "+capability);}
     if(code==0){selected=candidate;retryAfter=DateTime.MinValue;return 0;}
     Stop();
     // Disk/verification failures are not an interface mismatch; fail closed.
     if(code==21||code==22)return code;
    }catch{Stop();}
   }
   selected=null;
   if(anyEnumerated&&capability!=null)capabilityRetry[capability]=DateTime.UtcNow.AddSeconds(5);
   else retryAfter=DateTime.UtcNow.AddSeconds(5);
   return 20;
  }
  async Task<int> Exchange(string command){
   Process child=process;
   Task<int> response=Task.Run(delegate {
    try{child.StandardInput.WriteLine(command);child.StandardInput.Flush();int result;
     return Int32.TryParse(child.StandardOutput.ReadLine(),out result)?result:40;
    }catch{return 40;}
   });
   int wait=command.StartsWith("ensure ")?Math.Max(timeout,20000):timeout;
   if(await Task.WhenAny(response,Task.Delay(wait))!=response){Stop();return 91;}
   return await response;
  }
  void Stop(){Process child=process;process=null;if(child==null)return;
   try{if(!child.HasExited){child.StandardInput.Close();if(!child.WaitForExit(300)){child.Kill();child.WaitForExit(300);}}}catch{}finally{child.Dispose();}
  }
  public void Dispose(){disposed=true;Stop();}
 }
}
