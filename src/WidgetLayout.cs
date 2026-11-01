using System;
using System.Drawing;
namespace DeadlockOverlay {
 public static class WidgetLayout {
  public static Rectangle Bounds(Settings s,Rectangle area,int index,int leftTeam=2){
   var r=new Rectangle[6];
   for(int i=0;i<6;i++){var p=s.Placement(i<2?(i==0?leftTeam:5-leftTeam)-2:i);double scale=i<2?DisplayLayout.PanelScale(area,p.Scale):DisplayLayout.HudScale(area,p.Scale,i);int w=i<2?250:i==2?1750:i==5?196:140,h=i<2?490:100;r[i]=p.Bounds(area,new Size(Math.Max(1,(int)(w*scale)),Math.Max(1,(int)(h*scale))));if(i<2&&leftTeam==3)r[i].X=area.Left+area.Right-r[i].Right;}
   if(!s.AvoidOverlap)return r[index];
   int gap=Math.Max(6,area.Height/120);
   var placed=new System.Collections.Generic.List<Rectangle>();
   // Place the grouped recommendation row first, then timers, then team panels.
   foreach(int i in new[]{2,5,3,4,0,1}){
    if(!s.ShowHudPart(i<2?(i==0?leftTeam:5-leftTeam)-2:i))continue;
    var original=r[i];Rectangle winner=original;
    for(int attempt=0;attempt<(i<2?11:1);attempt++){
    double fit=1-attempt*.05;var desired=new Rectangle(original.Location,new Size((int)(original.Width*fit),(int)(original.Height*fit)));
    var xs=new System.Collections.Generic.List<int>{desired.X,area.Left,area.Right-desired.Width};var ys=new System.Collections.Generic.List<int>{desired.Y,area.Top,area.Bottom-desired.Height};
    foreach(var other in placed){xs.Add(other.Left-desired.Width-gap);xs.Add(other.Right+gap);ys.Add(other.Top-desired.Height-gap);ys.Add(other.Bottom+gap);}
    double best=double.MaxValue;
    foreach(int x in xs)foreach(int y in ys){var candidate=new Rectangle(Math.Max(area.Left,Math.Min(area.Right-desired.Width,x)),Math.Max(area.Top,Math.Min(area.Bottom-desired.Height,y)),desired.Width,desired.Height);bool free=true;
     foreach(var other in placed){var padded=other;padded.Inflate(gap,gap);if(candidate.IntersectsWith(padded)){free=false;break;}}
     double distance=(double)(candidate.X-desired.X)*(candidate.X-desired.X)+(double)(candidate.Y-desired.Y)*(candidate.Y-desired.Y);
     if(free&&distance<best){best=distance;winner=candidate;}
    }
    if(best<double.MaxValue)break;
    }
    r[i]=winner;placed.Add(winner);
   }
   return r[index];
  }
 }
}
