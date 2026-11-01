using System;
using System.Drawing;
namespace DeadlockOverlay {
 public static class DisplayLayout {
  // Game client rectangles are physical pixels (per-monitor DPI aware). Do not
  // multiply by desktop DPI again: that would double-scale the game overlay.
  public static double PanelScale(Rectangle area,double userScale) {
   return Math.Max(.01,Math.Min(Math.Max(.8,Math.Min(area.Height/1080.0,area.Width/1920.0))*userScale,
    Math.Min(area.Width/560.0,area.Height/510.0)));
  }
  public static float HudScale(Rectangle area,double userScale,int part) {
   int width=part==2?1750:part==5?196:140;
   double basis=part==2?Math.Min(area.Height/1440.0,area.Width/2560.0):Math.Max(.85,Math.Min(area.Height/1080.0,area.Width/1920.0));
   return (float)Math.Max(.01,Math.Min(basis*userScale,Math.Min(area.Width/(double)width,area.Height/100.0)));
  }
 }
}
