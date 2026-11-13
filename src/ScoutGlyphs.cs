using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace DeadlockOverlay {
 public static class ScoutGlyphs {
  public static void Draw(Graphics graphics,string key,RectangleF bounds,Color color){var saved=graphics.Save();graphics.TranslateTransform(bounds.X,bounds.Y);graphics.ScaleTransform(bounds.Width/24f,bounds.Height/24f);graphics.SmoothingMode=SmoothingMode.AntiAlias;
   using(var p=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})using(var b=new SolidBrush(color)){
    if(key=="main"){graphics.DrawPolygon(p,new[]{new PointF(3,7),new PointF(7,11),new PointF(12,3),new PointF(17,11),new PointF(21,7),new PointF(19,19),new PointF(5,19)});graphics.DrawLine(p,6,15,18,15);}
    else if(key=="smurf"){using(var path=new GraphicsPath()){path.AddBezier(2,7,7,3,17,3,22,7);path.AddBezier(22,7,22,20,16,20,12,15);path.AddBezier(12,15,8,20,2,20,2,7);graphics.DrawPath(p,path);}graphics.DrawLine(p,6,10,9,11);graphics.DrawLine(p,15,11,18,10);}
    else if(key=="leaver"){graphics.DrawLines(p,new[]{new PointF(12,3),new PointF(4,3),new PointF(4,21),new PointF(12,21)});graphics.DrawLine(p,10,12,22,12);graphics.DrawLines(p,new[]{new PointF(18,8),new PointF(22,12),new PointF(18,16)});}
    else if(key=="low"||key=="high"){float a=key=="high"?4:20,other=24-a;graphics.DrawLine(p,12,a,12,other);graphics.DrawLines(p,new[]{new PointF(6,12),new PointF(12,a),new PointF(18,12)});}
    else if(key=="new"){graphics.DrawLine(p,12,21,12,10);graphics.DrawEllipse(p,3,6,9,6);graphics.DrawEllipse(p,12,3,9,7);}
    else if(key=="streak"){using(var path=new GraphicsPath()){path.AddBezier(12,2,14,9,22,10,19,17);path.AddBezier(19,17,14,25,5,22,4,16);path.AddBezier(4,16,3,10,9,8,12,2);graphics.DrawPath(p,path);graphics.DrawLine(p,12,14,12,19);}}
    else {graphics.DrawEllipse(p,8,3,8,8);graphics.DrawArc(p,4,12,16,12,180,180);graphics.DrawArc(p,0,6,7,7,80,240);graphics.DrawArc(p,17,6,7,7,220,240);}
   }graphics.Restore(saved);
  }
 }
 public sealed class ScoutLayout {
  public int Columns,Width,Extent;public Rectangle[] Cards;public Rectangle[] Headers;
  public static ScoutLayout Create(int width){int pad=16,gap=14;bool split=width>=900;int cardWidth=split?(width-2*pad-gap)/2:width-2*pad;var result=new ScoutLayout{Columns=split?2:1,Width=width,Cards=new Rectangle[12],Headers=new Rectangle[2]};
   for(int team=0;team<2;team++){int x=split?pad+team*(cardWidth+gap):pad,y=split?0:team*(36+6*154+20);result.Headers[team]=new Rectangle(x,y,cardWidth,30);for(int i=0;i<6;i++)result.Cards[team*6+i]=new Rectangle(x,y+36+i*154,cardWidth,144);}
   result.Extent=split?36+6*154:2*(36+6*154+20);return result;
  }
 }
}
