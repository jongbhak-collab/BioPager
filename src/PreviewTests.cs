// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
// These checks use synthetic windows only; no real application is captured.
using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BioPager {
 internal static class PreviewTests {
  const int AppWindow=0x40000,ToolWindow=0x80,NoActivate=0x08000000,Child=0x40000000;
  [DllImport("dwmapi.dll")] static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

  [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

  sealed class FixtureForm : Form {
   internal int AddExtended,RemoveExtended;
   internal bool Pattern,SecondPattern,TopStripe;
   internal Rectangle Badge;
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
    if(Pattern) {
     using(Brush left=new SolidBrush(SecondPattern?Color.Lime:Color.Red))
     using(Brush right=new SolidBrush(SecondPattern?Color.Yellow:Color.Blue)) {
      int split=ClientSize.Width/2;
      e.Graphics.FillRectangle(left,0,0,split,ClientSize.Height);
      e.Graphics.FillRectangle(right,split,0,ClientSize.Width-split,ClientSize.Height);
     }
     if(TopStripe)e.Graphics.FillRectangle(Brushes.Cyan,0,0,ClientSize.Width,ClientSize.Height/4);
    }
    if(!Badge.IsEmpty)e.Graphics.FillRectangle(Brushes.DarkOrange,Badge);
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
   Rectangle body=new Rectangle(0,0,300,100),screens=new Rectangle(-1920,-200,5760,1920);
   Rectangle mapped=SpatialLayout.Map(screens,screens,body);
   Check(mapped==new Rectangle(0,0,300,100),"spatial mapping preserves negative multi-monitor origin and aspect ratio");
   Rectangle off=SpatialLayout.Map(new Rectangle(-2200,-200,1920,1920),screens,body);
   Check(off.Left<0&&off.Width==100,"off-screen windows retain original size before clipping");
   Rectangle rear=new Rectangle(0,0,100,100),front=new Rectangle(25,25,50,50);
   List<Rectangle> parts=SpatialLayout.Uncovered(rear,rear,new[]{front});
   int area=0;bool safe=true;
   for(int i=0;i<parts.Count;i++){area+=parts[i].Width*parts[i].Height;if(parts[i].IntersectsWith(front))safe=false;for(int j=i+1;j<parts.Count;j++)if(parts[i].IntersectsWith(parts[j]))safe=false;}
   Check(parts.Count==4&&area==7500&&safe,"center foreground leaves exactly four disjoint uncovered rear fragments");
   Check(SpatialLayout.Uncovered(rear,rear,new[]{rear}).Count==0,"fully covered rear window has no live destination or hit region");
   Check(SpatialLayout.SourceCrop(new Rectangle(50,0,50,100),rear,new Size(240,160))==new Rectangle(120,0,120,160),"DWM source crop preserves the rear image position instead of squeezing it");
   using(Bitmap image=new Bitmap(100,100))using(Graphics g=Graphics.FromImage(image)) {
    g.Clear(Color.Magenta);foreach(Rectangle part in parts)g.FillRectangle(Brushes.Blue,part);
    Check(image.GetPixel(50,50).ToArgb()==Color.Magenta.ToArgb()&&image.GetPixel(10,50).ToArgb()==Color.Blue.ToArgb(),"visual fallback leaves foreground pixels intact");
   }
   Random random=new Random(42);bool exact=true;
   for(int trial=0;trial<100;trial++) {
    List<Rectangle> covers=new List<Rectangle>();for(int n=0;n<8;n++)covers.Add(new Rectangle(random.Next(-5,20),random.Next(-5,20),random.Next(1,15),random.Next(1,15)));
    List<Rectangle> cuts=SpatialLayout.Uncovered(new Rectangle(-2,-2,24,24),new Rectangle(0,0,20,20),covers);
    for(int y=0;y<20;y++)for(int x=0;x<20;x++){Point point=new Point(x,y);bool covered=covers.Exists(delegate(Rectangle r){return r.Contains(point);});int count=0;foreach(Rectangle r in cuts)if(r.Contains(point))count++;if(count!=(covered?0:1))exact=false;}
   }
   Check(exact,"100 overlap layouts preserve every visible pixel exactly once and exclude covered pixels");
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
   Rectangle badge=Pager.AppBadgeBounds(new Rectangle(11,23,200,100),8);
   Check(badge.Left>=11&&badge.Top>=23&&badge.Right<=211&&badge.Bottom<=123&&badge.Width==badge.Height&&Math.Abs(badge.Width-30)<=1,"app badge is a top-left square approximately 15 percent of preview width");
   Check(badge.Left<=13&&badge.Top<=25,"app badge stays at the preview's top-left corner");
   int previous=0;
   foreach(int percent in new[]{15,20,30,50}) {
    Rectangle selected=Pager.AppBadgeBounds(new Rectangle(11,23,200,160),8,percent);
    Check(selected.Left==11&&selected.Top==23&&selected.Width==percent*2&&selected.Height==selected.Width&&selected.Width>previous,"selectable "+percent+" percent badge has the requested preview-width ratio and grows monotonically");
    previous=selected.Width;
   }
   Rectangle square=Pager.AppBadgeBounds(new Rectangle(10,20,100,100),8,50);
   Check(square.Width==50&&square.Height==50&&square.Right<110&&square.Bottom<120,"50 percent square preview badge preserves visible window content");
   Rectangle shortPreview=new Rectangle(10,20,200,20),shortBadge=Pager.AppBadgeBounds(shortPreview,12,50);
   Check(shortBadge.Width==18&&shortPreview.Contains(shortBadge)&&shortBadge.Bottom<shortPreview.Bottom,"large badge leaves a content margin when a short preview limits its size");
   Rectangle narrow=Pager.AppBadgeBounds(new Rectangle(10,20,20,100),12,50);
   Check(narrow.Width==12&&new Rectangle(10,20,20,100).Contains(narrow),"readability minimum fits inside a narrow preview without replacing it");
   Check(Pager.AppBadgeBounds(new Rectangle(0,0,6,6),12,50).IsEmpty&&Pager.AppBadgeBounds(Rectangle.Empty,8,30).IsEmpty,"tiny or empty previews omit unreadable badges");
   Check(Pager.AppBadgeBounds(new Rectangle(0,0,200,160),8,25)==Pager.AppBadgeBounds(new Rectangle(0,0,200,160),8,30),"unsupported badge percentage falls back to the 30 percent default");
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
    List<WindowInfo> order=new List<WindowInfo>{new WindowInfo{Handle=visibleOwner.Handle},new WindowInfo{Handle=untitled.Handle}};
    untitled.BringToFront();WindowCatalog.OrderFrontToBack(order);
    Check(order[0].Handle==untitled.Handle,"catalog sorts actual HWNDs from foreground to background independently of input order");
    visibleOwner.BringToFront();WindowCatalog.OrderFrontToBack(order);
    Check(order[0].Handle==visibleOwner.Handle,"catalog refresh reflects a changed native Z-order");
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
  static async Task<bool> WaitForBadgePixels(Form destination,Rectangle area,Rectangle badge,Color badgeColor,Color left,Color right,string name) {
   for(int attempt=0;attempt<24;attempt++) {
    await Task.Delay(75);
    using(Bitmap image=ReadPixels(destination,area)) {
     Color pixel=image.GetPixel(badge.Left-area.Left+badge.Width/2,badge.Top-area.Top+badge.Height/2);
     // The 50% option can cover the ordinary middle-left sample. Read the
     // lower exposed strip to verify that the surrounding source stays live.
     int sampleY=Math.Max(image.Height*3/4,badge.Bottom-area.Top+1);
     sampleY=Math.Min(image.Height-1,sampleY);
     bool surrounding=Near(image.GetPixel(image.Width/4,sampleY),left)&&Near(image.GetPixel(image.Width*3/4,sampleY),right);
     if(Near(pixel,badgeColor)&&surrounding){SaveEvidence(image,name);return true;}
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

     Rectangle badge=Pager.AppBadgeBounds(area,8,30);
     destination.Badge=badge;destination.Invalidate();destination.Update();
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0,null,badge),"grid live preview accepts an app badge without replacing source content");previews.EndFrame();
     Check(previews.RegistrationCount>1&&previews.RegistrationCount<=5,"live badge uses a bounded set of disjoint thumbnail fragments");
     Check(await WaitForBadgePixels(destination,area,badge,Color.DarkOrange,Color.Lime,Color.Yellow,"thumbnail-badge-before"),"GDI app badge remains above live source pixels");
     source.SecondPattern=false;source.Invalidate();source.Update();
     Check(await WaitForBadgePixels(destination,area,badge,Color.DarkOrange,Color.Red,Color.Blue,"thumbnail-badge-after"),"badge remains visible while uncovered live source pixels update");
     Rectangle smallerBadge=badge;
     badge=Pager.AppBadgeBounds(area,8,50);destination.Badge=badge;destination.Invalidate();destination.Update();
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0,null,badge),"live thumbnail accepts the 50 percent badge after a size change");previews.EndFrame();
     Check(previews.RegistrationCount>1&&previews.RegistrationCount<=5,"growing the badge replaces thumbnail fragments without retaining the old set");
     Check(await WaitForBadgePixels(destination,area,badge,Color.DarkOrange,Color.Red,Color.Blue,"thumbnail-badge-50"),"50 percent badge preserves the exposed live source image");
     source.SecondPattern=true;source.Invalidate();source.Update();
     Check(await WaitForBadgePixels(destination,area,badge,Color.DarkOrange,Color.Lime,Color.Yellow,"thumbnail-badge-50-update"),"exposed source content continues updating around the 50 percent badge");
     badge=smallerBadge;destination.Badge=badge;destination.Invalidate();destination.Update();
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0,null,badge),"live thumbnail accepts shrinking the badge to 30 percent");previews.EndFrame();
     Check(previews.RegistrationCount>1&&previews.RegistrationCount<=5,"shrinking the badge releases superseded native fragments");
     Check(await WaitForBadgePixels(destination,area,badge,Color.DarkOrange,Color.Lime,Color.Yellow,"thumbnail-badge-30-restored"),"shrinking the badge restores the newly exposed source pixels");
     using(Bitmap image=ReadPixels(destination,area))Check(Near(image.GetPixel(area.Width*2/5,badge.Height/2),Color.Lime),"pixels covered only by the former 50 percent live badge become visible again at 30 percent");
     source.SecondPattern=false;source.Invalidate();source.Update();
     destination.Badge=Rectangle.Empty;destination.Invalidate();destination.Update();
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0),"disabling app badges restores the complete live thumbnail");previews.EndFrame();
     Check(previews.RegistrationCount==1,"disabling badges releases every extra thumbnail fragment");
     Check(await WaitForBadgePixels(destination,area,badge,Color.Red,Color.Red,Color.Blue,"thumbnail-badge-disabled"),"turning the badge off restores source pixels at its former position");
     source.SecondPattern=true;source.Invalidate();source.Update();
     Check(await WaitForPixels(destination,area,Color.Lime,Color.Yellow,"thumbnail-badge-disabled-update"),"full thumbnail remains live after disabling the badge");

     Rectangle cover=new Rectangle(area.Left+60,area.Top,60,area.Height);
     List<Rectangle> fragments=SpatialLayout.Uncovered(area,area,new[]{cover});
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0,fragments),"spatial live preview accepts two cropped uncovered fragments");previews.EndFrame();
     await Task.Delay(250);
     using(Bitmap overlap=ReadPixels(destination,area)) {
      SaveEvidence(overlap,"spatial-live-around-fallback");
      Check(Near(overlap.GetPixel(20,60),Color.Lime)&&Near(overlap.GetPixel(90,60),Color.Magenta)&&Near(overlap.GetPixel(160,60),Color.Yellow),"DWM rear fragments preserve GDI foreground and retain correct left/right source pixels");
     }
     source.SecondPattern=false;source.Invalidate();source.Update();await Task.Delay(250);
     using(Bitmap overlap=ReadPixels(destination,area)) {
      SaveEvidence(overlap,"spatial-live-repaint");
      Check(Near(overlap.GetPixel(20,60),Color.Red)&&Near(overlap.GetPixel(90,60),Color.Magenta)&&Near(overlap.GetPixel(160,60),Color.Blue),"overlapping live preview updates without leaking into foreground fallback");
     }
     previews.BeginFrame();Check(!previews.Show(source.Handle,process,cls,area,false,0,new List<Rectangle>()),"fully occluded live preview is suppressed");previews.EndFrame();
     Check(previews.RegistrationCount==0,"fully covered source releases every fragment registration");
     source.SecondPattern=true;source.Invalidate();source.Update();

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
     previews.BeginFrame();Check(previews.Show(source.Handle,process,cls,area,false,0),"synthetic cache fixture can establish its source identity");previews.EndFrame();
     IDictionary entries=(IDictionary)typeof(WindowPreviews).GetField("entries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(previews);
     object entry=entries[source.Handle];Bitmap cached=new Bitmap(180,120);using(Graphics draw=Graphics.FromImage(cached))draw.Clear(Color.Cyan);
     entry.GetType().GetField("Frame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(entry,cached);
     previews.BeginFrame();Check(!previews.Show(source.Handle,process,cls,area,true,0,fragments),"minimized source uses its cached frame and releases live fragments");
     using(Bitmap fallback=new Bitmap(204,144))using(Graphics draw=Graphics.FromImage(fallback)) {
      draw.Clear(Color.Magenta);using(Region clip=new Region(Rectangle.Empty)){foreach(Rectangle part in fragments)clip.Union(part);draw.SetClip(clip,System.Drawing.Drawing2D.CombineMode.Replace);Check(previews.TryDrawCached(draw,source.Handle,process,cls,area),"cached minimized source draws through the overlap clip");}
      Check(Near(fallback.GetPixel(32,72),Color.Cyan)&&Near(fallback.GetPixel(102,72),Color.Magenta),"actual cached renderer preserves foreground pixels");SaveEvidence(fallback,"spatial-cached-around-foreground");
     }
     previews.EndFrame();Check(previews.RegistrationCount==0,"minimized cache path retains no live DWM surfaces");
     if(SetWindowDisplayAffinity(source.Handle,1))try {
      previews.BeginFrame();Check(!previews.Show(source.Handle,process,cls,area,false,0,fragments),"Windows protected source cannot register spatial live fragments");previews.EndFrame();
      Check(previews.CachedFrameCount==0&&previews.RegistrationCount==0,"protection purges both cached pixels and every native registration");
     }finally{SetWindowDisplayAffinity(source.Handle,0);}else Skip("Windows did not allow display-affinity protection on synthetic source");
     await PortraitBadgePixels(source,destination,previews,area);
    }
   }
  }

  static async Task PortraitBadgePixels(FixtureForm source,FixtureForm destination,WindowPreviews previews,Rectangle area) {
   Rectangle work=Screen.FromHandle(source.Handle).WorkingArea;
   if(work.Height<450){Skip("decorated portrait badge check: desktop is too short for an isolated decorated source");return;}
   using(FixtureForm portrait=Fixture("Decorated portrait preview fixture",new Rectangle(source.Left,source.Top,130,210),AppWindow,0)) {
    // Windows enforces a native minimum caption width at the desktop DPI.
    // A 100px request can therefore become landscape after Show().
    portrait.FormBorderStyle=FormBorderStyle.FixedSingle;portrait.ClientSize=new Size(220,360);portrait.Pattern=true;portrait.TopStripe=true;
    portrait.Show();portrait.Update();
    Native.RECT client;Check(Native.GetClientRect(portrait.Handle,out client),"decorated fixture exposes a native client rectangle");
    Console.WriteLine("INFO: decorated fixture managed client="+portrait.ClientSize+", native client="+client.Box+", outer="+portrait.Bounds);
    if(client.R-client.L>=client.B-client.T) {
     portrait.ClientSize=new Size(portrait.ClientSize.Width,portrait.ClientSize.Width*8/5);portrait.Invalidate();portrait.Update();
     Check(Native.GetClientRect(portrait.Handle,out client),"resized decorated fixture exposes its final native client rectangle");
    }
    if(portrait.Bottom>work.Bottom-8||portrait.Right>=destination.Left-8){Skip("decorated portrait badge check: native caption sizing leaves insufficient isolated desktop space");return;}
    Check(client.R-client.L<client.B-client.T,"decorated source is genuinely portrait after Windows applies native minimum sizes");
    uint process;Native.GetWindowThreadProcessId(portrait.Handle,out process);string cls=ClassName(portrait.Handle);
    Rectangle fitted=WindowPreviews.Fit(new Size(client.R-client.L,client.B-client.T),area),badge=Pager.AppBadgeBounds(fitted,8);
    Check(fitted.Width<area.Width-8&&fitted.Height==area.Height,"decorated fixture establishes portrait letterboxing before testing badge cropping");
    previews.BeginFrame();Check(previews.Show(portrait.Handle,process,cls,area,false,0),"decorated portrait source establishes its fitted client preview");previews.EndFrame();
    Check(await WaitForPixels(destination,fitted,Color.Red,Color.Blue,"portrait-client-before"),"decorated portrait baseline contains client pixels without its titlebar");
    using(Bitmap baseline=ReadPixels(destination,area)) {
     SaveEvidence(baseline,"portrait-client-baseline");
     destination.Badge=badge;destination.Invalidate();destination.Update();
     previews.BeginFrame();Check(previews.Show(portrait.Handle,process,cls,area,false,0,null,badge),"decorated portrait client preview accepts an app badge");previews.EndFrame();
     Check(await WaitForBadgePixels(destination,fitted,badge,Color.DarkOrange,Color.Red,Color.Blue,"portrait-client-badge"),"badge preserves portrait client scaling and source crops");
     using(Bitmap image=ReadPixels(destination,area)) {
      Check(Near(image.GetPixel(4,area.Height/2),Color.Magenta)&&Near(image.GetPixel(area.Width-5,area.Height/2),Color.Magenta),"badged portrait preview retains its original letterboxing");
      int topX=fitted.Left-area.Left+fitted.Width*3/4,topY=fitted.Top-area.Top+fitted.Height/8;
      Check(Near(baseline.GetPixel(topX,topY),Color.Cyan)&&Near(image.GetPixel(topX,topY),Color.Cyan),"badged client source preserves a distinct top stripe without a titlebar offset");
      Rectangle omit=badge;omit.Inflate(2,2);int compared=0,changed=0;
      for(int y=fitted.Top+3;y<fitted.Bottom-3;y++)for(int x=fitted.Left+3;x<fitted.Right-3;x++) {
       // Exclude only the badge and two pattern boundaries where native
       // thumbnail interpolation can differ by a pixel between fragments.
       if(omit.Contains(x,y)||Math.Abs(x-(fitted.Left+fitted.Width/2))<=2||Math.Abs(y-(fitted.Top+fitted.Height/4))<=2)continue;
       compared++;if(!Near(baseline.GetPixel(x-area.Left,y-area.Top),image.GetPixel(x-area.Left,y-area.Top)))changed++;
      }
      Check(compared>100&&changed<=compared/100,"decorated portrait pixels outside the badge match the unbadged fitted client image");
     }
    }
    destination.Badge=Rectangle.Empty;destination.Invalidate();destination.Update();
    previews.BeginFrame();previews.EndFrame();
    Check(previews.RegistrationCount==0,"closing a badged portrait source releases all native thumbnails");
   }
  }

  static FieldInfo PagerField(string name){return typeof(Pager).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
  static object PagerCall(Pager pager,string name,params object[] args){return typeof(Pager).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pager,args);}
  static Icon SolidIcon(Color color) {
   using(Bitmap image=new Bitmap(32,32,PixelFormat.Format32bppArgb)) {
    using(Graphics graphics=Graphics.FromImage(image))graphics.Clear(color);
    IntPtr handle=image.GetHicon();
    try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
   }
  }
  static void PagerBadges(FixtureForm source) {
   string folder=Path.Combine(Path.GetTempPath(),"BioPagerBadges-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   string settings=Path.Combine(folder,"settings.txt");
   File.WriteAllLines(settings,new[]{"240","132","False","False","0","0","True","True","False","False","200","-13170420","False"});
   using(Pager pager=new Pager(settings))try {
    PagerField("DeviceScale").SetValue(pager,1f);
    ToolStripMenuItem item=(ToolStripMenuItem)PagerField("badgeItem").GetValue(pager);
    Check((bool)PagerField("showAppBadges").GetValue(pager)&&item.Checked,"legacy settings enable app badges on upgrade without replacing the existing preview preference");
    Check(!(bool)PagerField("showPreviews").GetValue(pager),"badge upgrade preserves an explicit disabled-preview setting");
    Check(item.Text=="Show app logo badges on previews"&&item.CheckOnClick,"app badge option is discoverable as a checkable menu item");
    ToolStripMenuItem sizeMenu=(ToolStripMenuItem)PagerField("badgeSizeItem").GetValue(pager);
    Check((int)PagerField("appBadgePercent").GetValue(pager)==30,"legacy settings use the larger 30 percent badge by default");
    Check(sizeMenu.Text=="App logo badge size"&&sizeMenu.DropDownItems.Count==4,"badge size submenu exposes four percentage choices");
    foreach(int percent in new[]{15,20,30,50}) {
     ToolStripMenuItem choice=null;foreach(ToolStripItem child in sizeMenu.DropDownItems)if(child.Text==percent+"%")choice=(ToolStripMenuItem)child;
     Check(choice!=null&&choice.Checked==(percent==30),"badge size menu initially marks only the 30 percent default for choice "+percent);
    }
    item.Checked=false;
    Check(File.ReadAllLines(settings).Length>=15&&!Boolean.Parse(File.ReadAllLines(settings)[13]),"turning badges off preserves the appended badge size setting");
    PagerField("showAppBadges").SetValue(pager,true);PagerCall(pager,"LoadSettings");
    Check(!(bool)PagerField("showAppBadges").GetValue(pager),"disabled app badge preference survives a settings reload");
    string[] legacy=new string[14];Array.Copy(File.ReadAllLines(settings),legacy,14);File.WriteAllLines(settings,legacy);
    PagerField("appBadgePercent").SetValue(pager,15);PagerCall(pager,"LoadSettings");
    Check((int)PagerField("appBadgePercent").GetValue(pager)==30&&!(bool)PagerField("showAppBadges").GetValue(pager),"0.4.14 settings upgrade to 30 percent while preserving the disabled badge preference");
    foreach(int percent in new[]{15,20,30,50}) {
     ToolStripMenuItem choice=null;foreach(ToolStripItem child in sizeMenu.DropDownItems)if(child.Text==percent+"%")choice=(ToolStripMenuItem)child;
     choice.PerformClick();
     int checkedCount=0;foreach(ToolStripItem child in sizeMenu.DropDownItems)if(((ToolStripMenuItem)child).Checked)checkedCount++;
     string[] saved=File.ReadAllLines(settings);
     Check((int)PagerField("appBadgePercent").GetValue(pager)==percent&&choice.Checked&&checkedCount==1,"selecting "+percent+" percent through the real menu updates the exclusive choice");
     Check(saved.Length==15&&saved[14]==percent.ToString()&&!Boolean.Parse(saved[13]),"selecting "+percent+" percent persists its value without enabling disabled badges");
     PagerField("appBadgePercent").SetValue(pager,-1);PagerCall(pager,"LoadSettings");
     Check((int)PagerField("appBadgePercent").GetValue(pager)==percent,"selected "+percent+" percent badge size survives reloading settings");
    }
    foreach(string invalid in new[]{"25","0","100","-1","20.0","invalid",""}) {
     string[] saved=File.ReadAllLines(settings);saved[14]=invalid;File.WriteAllLines(settings,saved);
     PagerField("appBadgePercent").SetValue(pager,15);PagerCall(pager,"LoadSettings");
     Check((int)PagerField("appBadgePercent").GetValue(pager)==30&&!(bool)PagerField("showAppBadges").GetValue(pager),"invalid badge size '"+invalid+"' resets to 30 percent and preserves badge visibility");
    }
    PagerCall(pager,"SetAppBadgePercent",30);
    item.Checked=true;PagerField("showAppBadges").SetValue(pager,false);PagerCall(pager,"LoadSettings");
    Check((bool)PagerField("showAppBadges").GetValue(pager)&&Boolean.Parse(File.ReadAllLines(settings)[13]),"re-enabling app badges persists and reloads");

    PagerField("showPreviews").SetValue(pager,true);
    uint process;Native.GetWindowThreadProcessId(source.Handle,out process);string cls=ClassName(source.Handle);
    WindowPreviews previews=(WindowPreviews)PagerField("previews").GetValue(pager);
    Rectangle tile=new Rectangle(10,10,180,132),content=new Rectangle(11,11,178,118),badge=Pager.AppBadgeBounds(content,8,30);
    using(Icon icon=SolidIcon(Color.Yellow)) {
     WindowInfo app=new WindowInfo{Handle=source.Handle,Process=process,Class=cls,Title="Synthetic white app",Icon=icon};
     previews.BeginFrame();
     using(Bitmap scratch=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(scratch))previews.TryDrawCached(graphics,app.Handle,app.Process,app.Class,content);
     IDictionary entries=(IDictionary)typeof(WindowPreviews).GetField("entries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(previews);
     object entry=entries[source.Handle];Bitmap cached=new Bitmap(content.Width,content.Height);using(Graphics graphics=Graphics.FromImage(cached))graphics.Clear(Color.White);
     entry.GetType().GetField("Frame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(entry,cached);
     using(Bitmap image=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(image)) {
      graphics.Clear(Color.Magenta);PagerCall(pager,"DrawApplication",graphics,app,tile,false,null);
      Check(Near(image.GetPixel(badge.Left+badge.Width/2,badge.Top+badge.Height/2),Color.Yellow),"actual cached preview renderer overlays the app logo at the top-left");
      Check(Near(image.GetPixel(content.Right-10,content.Top+content.Height/2),Color.White),"cached window content remains visible around the app logo badge");
      SaveEvidence(image,"pager-cached-app-badge");
     }
     PagerCall(pager,"SetAppBadgePercent",50);badge=Pager.AppBadgeBounds(content,8,50);
     using(Bitmap image=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(image)) {
      graphics.Clear(Color.Magenta);PagerCall(pager,"DrawApplication",graphics,app,tile,false,null);
      Check(Near(image.GetPixel(badge.Right-4,badge.Top+badge.Height/2),Color.Yellow),"cached preview renderer honors the selected 50 percent badge width");
      Check(Near(image.GetPixel(content.Left+content.Width/4,content.Bottom-8),Color.White)&&Near(image.GetPixel(content.Right-10,content.Top+content.Height/2),Color.White),"50 percent cached badge preserves the exposed window image below and beside it");
      SaveEvidence(image,"pager-cached-app-badge-50");
     }
     PagerCall(pager,"SetAppBadgePercent",30);badge=Pager.AppBadgeBounds(content,8,30);
     using(Bitmap image=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(image)) {
      graphics.Clear(Color.Magenta);PagerCall(pager,"DrawApplication",graphics,app,tile,false,null);
      Check(Near(image.GetPixel(badge.Right+10,badge.Top+badge.Height/2),Color.White),"shrinking cached badge to 30 percent restores newly exposed window pixels");
     }
     PagerField("showAppBadges").SetValue(pager,false);
     using(Bitmap image=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(image)) {
      graphics.Clear(Color.Magenta);PagerCall(pager,"DrawApplication",graphics,app,tile,false,null);
      Check(Near(image.GetPixel(badge.Left+badge.Width/2,badge.Top+badge.Height/2),Color.White),"disabling the badge restores cached content at the same position");
     }
     PagerField("showAppBadges").SetValue(pager,true);
     Rectangle front=Rectangle.FromLTRB(badge.Left+badge.Width/2,badge.Top,badge.Right,badge.Bottom);
     List<Rectangle> visible=SpatialLayout.Uncovered(tile,tile,new[]{front});
     using(Bitmap image=new Bitmap(210,160))using(Graphics graphics=Graphics.FromImage(image))using(Region clip=new Region(Rectangle.Empty)) {
      graphics.Clear(Color.Magenta);foreach(Rectangle part in visible)clip.Union(part);graphics.SetClip(clip,System.Drawing.Drawing2D.CombineMode.Replace);
      PagerCall(pager,"DrawApplication",graphics,app,tile,false,visible);
      Check(Near(image.GetPixel(badge.Left+badge.Width/4,badge.Top+badge.Height/2),Color.Yellow),"spatial cached preview draws the exposed part of its app badge");
      Check(Near(image.GetPixel(front.Left+front.Width/2,front.Top+front.Height/2),Color.Magenta),"rear app badge obeys foreground clipping in spatial previews");
      SaveEvidence(image,"pager-cached-app-badge-spatial-clip");
     }
     previews.EndFrame();
     Check(previews.CachedFrameCount==1&&previews.RegistrationCount==0,"cached badge rendering keeps its frame without registering live surfaces");
    }
   }finally {
    // Never show this pager: Shown would initialize the user's virtual desktops.
    PagerCall(pager,"OnFormClosed",new FormClosedEventArgs(CloseReason.None));Directory.Delete(folder,true);
   }
  }
  static void PagerSpatial(FixtureForm source) {
   string folder=Path.Combine(Path.GetTempPath(),"BioPagerSpatial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   string settings=Path.Combine(folder,"settings.txt");
   File.WriteAllLines(settings,new[]{"240","132","False","False","0","0","True","True","False","False","200","-13170420","False"});
   using(Pager pager=new Pager(settings))try {
    PagerField("DeviceScale").SetValue(pager,1f);
    Check((bool)PagerField("iconGrid").GetValue(pager),"existing grid arrangement remains selected on upgrade");
    PagerField("gridItem").GetValue(pager).GetType().GetProperty("Checked").SetValue(PagerField("gridItem").GetValue(pager),false,null);
    PagerField("iconGrid").SetValue(pager,true);PagerCall(pager,"LoadSettings");
    Check(!(bool)PagerField("iconGrid").GetValue(pager),"spatial setting saves through the menu and reloads from disk");
    Guid desktop=Guid.NewGuid();
    List<Guid> desks=(List<Guid>)PagerField("desktops").GetValue(pager);desks.Clear();desks.Add(desktop);
    PagerField("current").SetValue(pager,desktop);PagerField("embedded").SetValue(pager,false);
    Rectangle screen=Screen.PrimaryScreen.Bounds;PagerField("screenBounds").SetValue(pager,screen);
    List<WindowInfo> apps=(List<WindowInfo>)PagerField("windows").GetValue(pager);apps.Clear();
    Rectangle front=new Rectangle(screen.Left+screen.Width/4,screen.Top+screen.Height/4,screen.Width/2,screen.Height/2);
    apps.Add(new WindowInfo{Handle=source.Handle,Desktop=desktop,Bounds=screen,Title="Rear browser",Class="Rear"});
    apps.Add(new WindowInfo{Handle=new IntPtr(123),Desktop=desktop,Bounds=front,Title="Foreground app",Class="Front"});
    apps.Add(new WindowInfo{Handle=new IntPtr(124),Desktop=desktop,Bounds=screen,Title="Minimized app",Class="Minimized",Minimized=true});
    using(Bitmap image=new Bitmap(260,132))using(Graphics g=Graphics.FromImage(image)) {
     PagerCall(pager,"DrawPager",g,new Rectangle(0,0,260,132));SaveEvidence(image,"pager-spatial-layout");
    }
    Rectangle body=new Rectangle(24,19,232,87),frontMap=SpatialLayout.Map(front,screen,body),rearMap=SpatialLayout.Map(screen,screen,body);
    Point frontPoint=new Point(frontMap.Left+frontMap.Width/2,frontMap.Top+frontMap.Height/2);
    object hit=PagerCall(pager,"HitAt",frontPoint);FieldInfo window=hit.GetType().GetField("Window",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    Check((IntPtr)window.GetValue(hit)==new IntPtr(123),"actual pager hit testing selects foreground app within overlap");
    hit=PagerCall(pager,"HitAt",new Point(rearMap.Left+2,rearMap.Top+2));
    Check((IntPtr)window.GetValue(hit)==source.Handle,"actual pager hit testing selects exposed rear app");
    hit=PagerCall(pager,"HitAt",new Point(30,120));
    Check((IntPtr)window.GetValue(hit)==new IntPtr(124),"minimized app stays accessible in separate strip without occluding desktop");
    PagerCall(pager,"OnDown",pager,new MouseEventArgs(MouseButtons.Left,1,frontPoint.X,frontPoint.Y,0));
    Check((IntPtr)PagerField("dragWindow").GetValue(pager)==new IntPtr(123),"drag gesture starts from the visible foreground hit");
    PagerCall(pager,"CancelDrag");Check((IntPtr)PagerField("dragWindow").GetValue(pager)==IntPtr.Zero,"canceling spatial drag clears the selected window");
    ((ToolStripMenuItem)PagerField("gridItem").GetValue(pager)).Checked=true;
    PagerField("iconGrid").SetValue(pager,false);PagerCall(pager,"LoadSettings");
    Check((bool)PagerField("iconGrid").GetValue(pager),"grid alternative also persists after selecting it again");
   }finally {
    // Never show this pager: Shown would initialize the user's virtual desktops.
    PagerCall(pager,"OnFormClosed",new FormClosedEventArgs(CloseReason.None));Directory.Delete(folder,true);
   }
  }

  internal static int RunWindows() {
   Exception failure=null;
   using(FixtureForm host=Fixture("BioPager preview test source",new Rectangle(40,40,240,160),AppWindow,0)) {
    host.Shown+=async delegate {
     try {NativePolicy(host);PagerSpatial(host);PagerBadges(host);await PixelPreviews(host);}
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
