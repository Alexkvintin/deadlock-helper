using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class WidgetPlacement {
  public double X,Y,Scale=1,Opacity=.88;
  public WidgetPlacement(){}
  public WidgetPlacement(double x,double y,double scale,double opacity){X=x;Y=y;Scale=scale;Opacity=opacity;}
  public Rectangle Bounds(Rectangle area,Size size){return new Rectangle(area.Left+(int)(Math.Max(0,area.Width-size.Width)*X),area.Top+(int)(Math.Max(0,area.Height-size.Height)*Y),size.Width,size.Height);}
 }
 // Separate handles exist only during explicit layout editing; normal widgets stay click-through.
 public sealed class LayoutHandle : Form {
  public static string[] Names{get{return new string[]{"Hidden King","Archmother",L.T("Контрпредметы (вместе)"),L.T("Кристаллы"),L.T("Мидбосс"),L.T("Рифт")};}}
  readonly int index;Settings settings;Rectangle area;Point origin,initial;bool dragging;
  public LayoutHandle(int index){this.index=index;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.FromArgb(35,46,50);Opacity=.75;Cursor=Cursors.SizeAll;DoubleBuffered=true;}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
  protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
  void StopDrag(){dragging=false;if(Capture)Capture=false;}
  public void Update(Settings s,Rectangle screen,Rectangle bounds){settings=s;area=screen;if(!s.EditLayout){StopDrag();if(Visible)Hide();return;}if(!dragging&&Bounds!=bounds)Bounds=bounds;if(!Visible)Show();}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=false;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Theme.Gold,3))e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);using(var font=new Font("Segoe UI",11,FontStyle.Bold))using(var brush=new SolidBrush(Theme.Gold))e.Graphics.DrawString(Names[index]+L.T(" · перетащите"),font,brush,new RectangleF(8,8,Width-16,Height-16));}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;dragging=true;origin=Cursor.Position;initial=Location;Capture=true;}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;var mouse=Cursor.Position;int x=Math.Max(area.Left,Math.Min(area.Right-Width,initial.X+mouse.X-origin.X)),y=Math.Max(area.Top,Math.Min(area.Bottom-Height,initial.Y+mouse.Y-origin.Y));Location=new Point(x,y);var p=settings.Placement(index);p.X=(x-area.Left)/(double)Math.Max(1,area.Width-Width);p.Y=(y-area.Top)/(double)Math.Max(1,area.Height-Height);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;SettingsManager.Save(settings);}
 }
}

