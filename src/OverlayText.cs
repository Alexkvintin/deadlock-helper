using System.Drawing;
namespace DeadlockOverlay {
 // A dark one-pixel outline is independent of panel opacity and preserves
 // contrast over bright game menus without making an opaque rectangle.
 public static class OverlayText {
  public static void Draw(Graphics g,string text,Font font,Brush ink,float x,float y){Draw(g,text,font,ink,new PointF(x,y));}
  public static void Draw(Graphics g,string text,Font font,Brush ink,PointF at){using(var shadow=new SolidBrush(Color.FromArgb(235,4,10,14))){g.DrawString(text,font,shadow,at.X-1,at.Y);g.DrawString(text,font,shadow,at.X+1,at.Y);g.DrawString(text,font,shadow,at.X,at.Y-1);g.DrawString(text,font,shadow,at.X,at.Y+1);}g.DrawString(text,font,ink,at);}
  public static void Draw(Graphics g,string text,Font font,Brush ink,RectangleF box){Draw(g,text,font,ink,box,StringFormat.GenericDefault);}
  public static void Draw(Graphics g,string text,Font font,Brush ink,RectangleF box,StringFormat format){using(var shadow=new SolidBrush(Color.FromArgb(235,4,10,14))){foreach(var delta in new[]{new PointF(-1,0),new PointF(1,0),new PointF(0,-1),new PointF(0,1)}){var moved=box;moved.Offset(delta);g.DrawString(text,font,shadow,moved,format);}}g.DrawString(text,font,ink,box,format);}
 }
}
