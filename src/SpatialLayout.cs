// Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
using System;
using System.Collections.Generic;
using System.Drawing;
namespace BioPager {
 internal static class SpatialLayout {
  // Keep the full transformed rectangle; clip afterward instead of rescaling
  // partly off-screen windows. Negative monitor coordinates stay meaningful.
  internal static Rectangle Map(Rectangle window,Rectangle screens,Rectangle body) {
   double scale=Math.Min((double)body.Width/Math.Max(1,screens.Width),(double)body.Height/Math.Max(1,screens.Height));
   double x=body.Left+(body.Width-screens.Width*scale)/2,y=body.Top+(body.Height-screens.Height*scale)/2;
   int left=(int)Math.Round(x+(window.Left-screens.Left)*scale),top=(int)Math.Round(y+(window.Top-screens.Top)*scale);
   int right=(int)Math.Round(x+(window.Right-screens.Left)*scale),bottom=(int)Math.Round(y+(window.Bottom-screens.Top)*scale);
   return Rectangle.FromLTRB(left,top,Math.Max(left+1,right),Math.Max(top+1,bottom));
  }
  internal static List<Rectangle> Uncovered(Rectangle window,Rectangle body,IEnumerable<Rectangle> front) {
   List<Rectangle> parts=new List<Rectangle>();Rectangle initial=Rectangle.Intersect(window,body);
   if(initial.Width>0&&initial.Height>0)parts.Add(initial);
   foreach(Rectangle cover in front) {
    List<Rectangle> next=new List<Rectangle>();
    foreach(Rectangle part in parts) {
     Rectangle overlap=Rectangle.Intersect(part,cover);
     if(overlap.Width<=0||overlap.Height<=0){next.Add(part);continue;}
     if(part.Top<overlap.Top)next.Add(Rectangle.FromLTRB(part.Left,part.Top,part.Right,overlap.Top));
     if(overlap.Bottom<part.Bottom)next.Add(Rectangle.FromLTRB(part.Left,overlap.Bottom,part.Right,part.Bottom));
     if(part.Left<overlap.Left)next.Add(Rectangle.FromLTRB(part.Left,overlap.Top,overlap.Left,overlap.Bottom));
     if(overlap.Right<part.Right)next.Add(Rectangle.FromLTRB(overlap.Right,overlap.Top,part.Right,overlap.Bottom));
    }
    parts=next;
    // Bounded complexity: omit live fragments if pathological overlap would
    // need too many native handles. GDI fallback still uses the same clipping.
   }
   return parts;
  }
  internal static Rectangle SourceCrop(Rectangle part,Rectangle target,Size source) {
   int l=(int)Math.Round((double)(part.Left-target.Left)*source.Width/target.Width);
   int t=(int)Math.Round((double)(part.Top-target.Top)*source.Height/target.Height);
   int r=(int)Math.Round((double)(part.Right-target.Left)*source.Width/target.Width);
   int b=(int)Math.Round((double)(part.Bottom-target.Top)*source.Height/target.Height);
   return Rectangle.FromLTRB(Math.Max(0,l),Math.Max(0,t),Math.Min(source.Width,Math.Max(l+1,r)),Math.Min(source.Height,Math.Max(t+1,b)));
  }
 }
}
