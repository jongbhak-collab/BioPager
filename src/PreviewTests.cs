// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// These checks use synthetic windows only; no real application is captured.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BioPager {
 internal static class PreviewTests {
  const int AppWindow=0x40000,ToolWindow=0x80,NoActivate=0x08000000,Child=0x40000000;
  [DllImport("dwmapi.dll")] static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

  sealed class FixtureForm : Form {
   internal int AddExtended,RemoveExtended;
   internal bool Pattern,SecondPattern;
   internal FixtureForm() {
    AutoScaleMode=AutoScaleMode.None;
    FormBorderStyle=FormBorderStyle.None;
    StartPosition=FormStartPosition.Manual;
    ShowInTaskbar=false;
    TopMost=true;
   }
   protected override CreateParams CreateParams {
    get {CreateParams value=base.CreateParams;value.ExStyle=(value.ExStyle|AddExtended)&~RemoveExtended;return value;}
   }
   protected override void OnPaint(PaintEventArgs e) {
    base.OnPaint(e);
    if(!Pattern)return;
    using(Brush left=new SolidBrush(SecondPattern?Color.Lime:Color.Red))
    using(Brush right=new SolidBrush(SecondPattern?Color.Yellow:Color.Blue)) {
     int split=ClientSize.Width/2;
     e.Graphics.FillRectangle(left,0,0,split,ClientSize.Height);
     e.Graphics.FillRectangle(right,split,0,ClientSize.Width-split,ClientSize.Height);
    }
   }
   internal void RebuildHandle(){RecreateHandle();}
  }

  static void Check(bool condition,string message) {
   if(!condition)throw new InvalidOperationException(message);
   Console.WriteLine("PASS: "+message);
  }
  static void Skip(string reason){Console.WriteLine("SKIP: "+reason);}
  static bool Eligible(int style,int extended,bool owned,bool ownerVisible,bool sameProcess,int cloak,string cls) {
   return WindowCatalog.ExclusionReason(true,style,extended,owned,ownerVisible,sameProcess,cloak,cls)==null;
  }
  internal static void Unit() {
   Check(Eligible(0,AppWindow,true,true,true,0,"OwnedApplication"),"owned APPWINDOW application remains eligible");
   Check(Eligible(0,0,true,false,true,0,"HiddenOwnerApplication"),"main application with a hidden owner remains eligible");
   Check(!Eligible(0,0,true,true,true,0,"ModalDialog"),"ordinary owned dialog is not duplicated as a separate app");
   Check(!Eligible(0,ToolWindow,false,false,false,0,"Tooltip"),"floating helper/tool window remains excluded");
   Check(!Eligible(Child,AppWindow,false,false,false,0,"ChildControl"),"child control is not mistaken for a desktop app");
   Check(!Eligible(0,AppWindow,false,false,false,0,"Shell_TrayWnd"),"shell taskbar remains excluded even with application styling");
   Check(!Eligible(0,NoActivate,false,false,false,0,"Overlay"),"non-activating helper remains excluded");
   Check(Eligible(0,AppWindow,false,false,false,2,"InactiveDesktopApp"),"shell cloaking on another virtual desktop does not hide the app from BioPager");
   Check(!Eligible(0,AppWindow,false,false,false,1,"AppHiddenSurface"),"application-cloaked helper remains excluded");
   Check(WindowCatalog.DisplayTitle("Genome browser - Chrome","chrome","Chrome_WidgetWin_1")=="Genome browser - Chrome","active browser page title is preserved");
   Check(WindowCatalog.DisplayTitle("","Gemini","Chrome_WidgetWin_1")=="Gemini","untitled app receives a useful process-name fallback");
   Check(!String.IsNullOrWhiteSpace(WindowCatalog.DisplayTitle("","","")),"missing title and process metadata never produce a blank app label");

   Guid first=Guid.NewGuid(),second=Guid.NewGuid();
   List<Guid> known=new List<Guid>{first,second};bool inferred;
   Check(WindowCatalog.SelectDesktop(first,Guid.Empty,Guid.Empty,known,second,true,false,2,out inferred)==first&&!inferred,"confirmed desktop assignment survives shell cloaking");
   Check(WindowCatalog.SelectDesktop(Guid.Empty,Guid.Empty,first,known,second,true,true,0,out inferred)==second&&inferred,"positive current-desktop evidence replaces a stale cached assignment");
   Check(WindowCatalog.SelectDesktop(Guid.Empty,Guid.Empty,second,known,second,true,false,0,out inferred)==Guid.Empty,"cached current assignment contradicted by Windows is not reused");
   Check(WindowCatalog.SelectDesktop(Guid.Empty,Guid.Empty,Guid.Empty,known,second,false,false,2,out inferred)==Guid.Empty,"unknown desktop is not invented or treated as pinned everywhere");

   Rectangle target=new Rectangle(11,23,160,80);
   Rectangle wide=WindowPreviews.Fit(new Size(1920,1080),target);
   Check(target.Contains(wide)&&wide.Width>0&&wide.Height>0&&Math.Abs(wide.Width-wide.Height*1920.0/1080.0)<=1.1,"wide preview fits without distortion or clipping");
   Check(Math.Abs((wide.Left-target.Left)-(target.Right-wide.Right))<=1&&Math.Abs((wide.Top-target.Top)-(target.Bottom-wide.Bottom))<=1,"preview is centered inside its destination");
   Rectangle tall=WindowPreviews.Fit(new Size(400,1200),target);
   Check(target.Contains(tall)&&tall.Width>0&&Math.Abs(tall.Width-tall.Height/3.0)<=1.1,"portrait app preview preserves its aspect ratio");
   Check(WindowPreviews.Fit(Size.Empty,target).IsEmpty&&WindowPreviews.Fit(new Size(10,10),Rectangle.Empty).IsEmpty,"invalid preview dimensions produce no draw rectangle");
   Check(WindowPreviews.ThumbnailPropertiesSize==48,"DWM thumbnail property layout matches the Win32 ABI");
  }

  static string ClassName(IntPtr window) {
   StringBuilder text=new StringBuilder(256);Native.GetClassName(window,text,text.Capacity);return text.ToString();
  }
  static string NativeExclusion(IntPtr window) {
   IntPtr owner=Native.GetWindow(window,4);uint process,ownerProcess=0;
   Native.GetWindowThreadProcessId(window,out process);
   if(owner!=IntPtr.Zero)Native.GetWindowThreadProcessId(owner,out ownerProcess);
   int cloak; if(Native.DwmGetWindowAttribute(window,14,out cloak,4)!=0)cloak=0;
   return WindowCatalog.ExclusionReason(Native.IsWindowVisible(window),Native.GetWindowLong(window,-16),Native.GetWindowLong(window,-20),owner!=IntPtr.Zero,owner!=IntPtr.Zero&&Native.IsWindowVisible(owner),process==ownerProcess,cloak,ClassName(window));
  }
  static FixtureForm Fixture(string title,Rectangle bounds,int add,int remove) {
   FixtureForm form=new FixtureForm();form.Text=title;form.Bounds=bounds;form.AddExtended=add;form.RemoveExtended=remove;return form;
  }
  static void NativePolicy(Form visibleOwner) {
   Rectangle bounds=new Rectangle(visibleOwner.Left,visibleOwner.Bottom+20,180,70);
   using(FixtureForm hiddenOwner=Fixture("Hidden fixture owner",bounds,0,AppWindow)) {
    IntPtr hiddenHandle=hiddenOwner.Handle;
    Check(hiddenHandle!=IntPtr.Zero&&!Native.IsWindowVisible(hiddenHandle),"hidden-owner fixture is an actual hidden HWND");
    using(FixtureForm owned=Fixture("Owned application fixture",bounds,AppWindow,0)) {
     owned.Owner=visibleOwner;owned.Show();owned.Update();
     Check(Native.GetWindow(owned.Handle,4)!=IntPtr.Zero&&(Native.GetWindowLong(owned.Handle,-20)&AppWindow)!=0,"owned APPWINDOW fixture carries real ownership and application flags");
     Check(NativeExclusion(owned.Handle)==null,"actual owned APPWINDOW is included by the native policy");
    }
    using(FixtureForm main=Fixture("Hidden-owner main fixture",bounds,0,AppWindow)) {
     main.Owner=hiddenOwner;main.Show();main.Update();
     Check(Native.GetWindow(main.Handle,4)==hiddenHandle&&!Native.IsWindowVisible(hiddenHandle),"hidden-owner application fixture keeps its hidden owner");
     Check(NativeExclusion(main.Handle)==null,"actual main window with hidden owner is included");
    }
   }
   using(FixtureForm popup=Fixture("Popup fixture",bounds,0,AppWindow)) {
    popup.Owner=visibleOwner;popup.Show();popup.Update();
    Check(NativeExclusion(popup.Handle)!=null,"actual visible-owner popup is not duplicated");
   }
   using(FixtureForm untitled=Fixture("",bounds,AppWindow,0)) {
    untitled.Show();untitled.Update();
    Check(NativeExclusion(untitled.Handle)==null,"actual untitled application window remains eligible");
    StringBuilder title=new StringBuilder(128);Native.GetWindowText(untitled.Handle,title,title.Capacity);
    Check(title.Length==0&&!String.IsNullOrWhiteSpace(WindowCatalog.DisplayTitle(title.ToString(),Process.GetCurrentProcess().ProcessName,ClassName(untitled.Handle))),"actual empty window caption has a nonblank display fallback");
   }
   using(FixtureForm tool=Fixture("Tool fixture",bounds,ToolWindow,AppWindow)) {
    tool.Show();tool.Update();
    Check((Native.GetWindowLong(tool.Handle,-20)&ToolWindow)!=0&&NativeExclusion(tool.Handle)!=null,"actual tool window remains excluded");
   }
  }

  static bool Near(Color actual,Color expected) {
   return Math.Abs(actual.R-expected.R)<=24&&Math.Abs(actual.G-expected.G)<=24&&Math.Abs(actual.B-expected.B)<=24;
  }
  static Bitmap ReadPixels(Form form,Rectangle client) {
   Bitmap image=new Bitmap(client.Width,client.Height,PixelFormat.Format32bppArgb);
   try {
    using(Graphics graphics=Graphics.FromImage(image))graphics.CopyFromScreen(form.PointToScreen(client.Location),Point.Empty,client.Size,CopyPixelOperation.SourceCopy);
    return image;
   } catch {image.Dispose();throw;}
  }
  static bool Matches(Bitmap image,Color left,Color right) {
   return Near(image.GetPixel(image.Width/4,image.Height/2),left)&&Near(image.GetPixel(image.Width*3/4,image.Height/2),right);
  }
  static void SaveEvidence(Bitmap image,string name) {
   string folder=Environment.GetEnvironmentVariable("BIOPAGER_TEST_ARTIFACTS");
   if(String.IsNullOrWhiteSpace(folder))return;
   Directory.CreateDirectory(folder);image.Save(Path.Combine(folder,name+".png"),ImageFormat.Png);
  }
  static async Task<bool> WaitForPixels(Form destination,Rectangle area,Color left,Color right,string name) {
   for(int attempt=0;attempt<24;attempt++) {
    await Task.Delay(75);
    using(Bitmap image=ReadPixels(destination,area)) {
     if(Matches(image,left,right)){SaveEvidence(image,name);return true;}
     if(attempt==23)SaveEvidence(image,name+"-failed");
    }
   }
   return false;
  }
  static async Task PixelPreviews(FixtureForm source) {
   bool composition;
   if(!Environment.UserInteractive||DwmIsCompositionEnabled(out composition)!=0||!composition) {
    Skip("live thumbnail pixel check: this Windows session has no interactive DWM compositor");return;
   }
   Rectangle work=Screen.FromHandle(source.Handle).WorkingArea;
   if(work.Width<620||work.Height<260){Skip("live thumbnail pixel check: desktop is too small for isolated source and destination fixtures");return;}
   source.Bounds=new Rectangle(work.Left+24,work.Top+24,240,160);source.Pattern=true;source.Invalidate();source.Update();source.BringToFront();
   await Task.Delay(250);
   try {
    using(Bitmap baseline=ReadPixels(source,source.ClientRectangle)) {
     if(!Matches(baseline,Color.Red,Color.Blue)) {
      SaveEvidence(baseline,"source-unavailable");Skip("live thumbnail pixel check: the visible source fixture cannot be read from this desktop session");return;
     }
     SaveEvidence(baseline,"source-before");
    }
   } catch(System.ComponentModel.Win32Exception ex) {Skip("live thumbnail pixel check: desktop pixel access is unavailable ("+ex.NativeErrorCode+")");return;}

   using(FixtureForm destination=Fixture("Preview destination fixture",new Rectangle(source.Right+48,source.Top,204,144),0,AppWindow)) {
    destination.BackColor=Color.Magenta;destination.Show();destination.Update();
    uint process;Native.GetWindowThreadProcessId(source.Handle,out process);string cls=ClassName(source.Handle);
    Rectangle area=new Rectangle(12,12,180,120);
    using(WindowPreviews previews=new WindowPreviews(destination.Handle)) {
     previews.BeginFrame();bool registered=previews.Show(source.Handle,process,cls,area,false,0);previews.EndFrame();
     Check(registered&&previews.RegistrationCount==1,"real top-level DWM source is registered exactly once");
     Check(await WaitForPixels(destination,area,Color.Red,Color.Blue,"thumbnail-before"),"DWM thumbnail displays the source window's actual red and blue pixels");
     source.SecondPattern=true;source.Invalidate();source.Update();
     Check(await WaitForPixels(destination,area,Color.Lime,Color.Yellow,"thumbnail-after"),"thumbnail changes with source content without changing the HWND or re-registering");
     Check(previews.RegistrationCount==1,"live source updates preserve one registration");

     // Spatial mode and status overlays may request only a cached bitmap.
     // That still marks an entry as used, but must not retain its live surface.
     previews.BeginFrame();
     using(Graphics graphics=destination.CreateGraphics())previews.TryDrawCached(graphics,source.Handle,process,cls,area);
     previews.EndFrame();
     Check(previews.RegistrationCount==0,"cache-only frame releases a previously live thumbnail even when the source entry is still used");
     Check(await WaitForPixels(destination,area,Color.Magenta,Color.Magenta,"thumbnail-cache-only"),"cache-only frame cannot leave an old DWM surface above spatial previews or status text");

     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0),"live thumbnail resumes after a cache-only frame");previews.EndFrame();
     previews.BeginFrame();previews.EndFrame();
     Check(previews.RegistrationCount==0,"unused thumbnail registration is released on the next frame");
     Check(await WaitForPixels(destination,area,Color.Magenta,Color.Magenta,"thumbnail-removed"),"released thumbnail stops covering the destination");

     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0),"thumbnail can be registered again after removal");previews.EndFrame();
     previews.ResetDestination(IntPtr.Zero);
     Check(previews.RegistrationCount==0,"destination destruction releases thumbnail registrations");
     destination.RebuildHandle();destination.Show();destination.Update();
     previews.ResetDestination(destination.Handle);
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0),"thumbnail can be restored after destination handle recreation");previews.EndFrame();
     Check(await WaitForPixels(destination,area,Color.Lime,Color.Yellow,"thumbnail-recreated"),"recreated destination displays the current source content");

     previews.BeginFrame();Check(!previews.Show(source.Handle,process+1,cls,area,false,0),"mismatched source process identity cannot reuse another app's thumbnail");previews.EndFrame();
     Check(previews.RegistrationCount==0,"rejected source identity leaves no stale registration");
    }
   }
  }

  internal static int RunWindows() {
   Exception failure=null;
   using(FixtureForm host=Fixture("BioPager preview test source",new Rectangle(40,40,240,160),AppWindow,0)) {
    host.Shown+=async delegate {
     try {NativePolicy(host);await PixelPreviews(host);}
     catch(Exception ex){failure=ex;}
     finally {host.Close();}
    };
    Application.Run(host);
   }
   if(failure!=null)throw new InvalidOperationException("BioPager native preview regression failed",failure);
   return 0;
  }
 }
}
