// Copyright (c) 2026 Jong Bhak. Licensed under BioLicense 1.0; see LICENSE.
using System;
using System.Diagnostics;
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
  public WorkerClient(string executable,string arguments="--worker",int timeout=4000){this.executable=executable;this.arguments=arguments;this.timeout=timeout;}
  public async void Warm(){await Request("ping");}
  public async Task<int> Request(string command){
   await gate.WaitAsync();
   try{
    if(disposed)return 90;
    if(process==null||process.HasExited){
     Stop();
     ProcessStartInfo start=new ProcessStartInfo(executable,arguments);
     start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardInput=true;start.RedirectStandardOutput=true;
     start.StandardOutputEncoding=new UTF8Encoding(false);start.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;
     process=Process.Start(start);
    }
    Process child=process;
    Task<int> response=Task.Run(delegate {
     try{child.StandardInput.WriteLine(command);child.StandardInput.Flush();int result;
      return Int32.TryParse(child.StandardOutput.ReadLine(),out result)?result:40;
     }catch{return 40;}
    });
    if(await Task.WhenAny(response,Task.Delay(timeout))!=response){Stop();return 91;}
    int code=await response;
    if(code==23||code==40)Stop(); // Reconnect on the next request after a native failure.
    return code;
   }catch{Stop();return 40;}finally{gate.Release();}
  }
  void Stop(){Process child=process;process=null;if(child==null)return;
   try{if(!child.HasExited){child.StandardInput.Close();if(!child.WaitForExit(300)){child.Kill();child.WaitForExit(300);}}}catch{}finally{child.Dispose();}
  }
  public void Dispose(){disposed=true;Stop();}
 }
}
