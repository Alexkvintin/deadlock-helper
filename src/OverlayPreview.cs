using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class OverlayPreview:Control {
  readonly Settings settings;readonly HeroImages images=new HeroImages();readonly HudAdvice advice=new HudAdvice();readonly AlphaWindow[] widgets;readonly MatchState state=new MatchState();readonly ArtCache art=new ArtCache();
  readonly RectangleF[] hits=new RectangleF[6];RectangleF screen;bool dragging,resizing;Point origin;double oldX,oldY,oldScale;float initialWidth;int selected;
  public event Action<int> SelectionChanged;public event Action Edited;
  public int Selected{get{return selected;}set{if(value<0||value>5||selected==value)return;selected=value;Invalidate();if(SelectionChanged!=null)SelectionChanged(value);}}
  public OverlayPreview(Settings s){settings=s;DoubleBuffered=true;TabStop=true;SetStyle(ControlStyles.ResizeRedraw,true);BackColor=Theme.Background;
   widgets=new AlphaWindow[]{new OverlayRenderer(true,images),new OverlayRenderer(false,images),new HudOverlay(advice){SampleItems=true},new HudOverlay(advice,3),new HudOverlay(advice,4),new HudOverlay(advice,5)};foreach(var w in widgets)w.PreviewOnly=true;
  }
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;float u=DeviceDpi/96f;
   Design.Text(g,L.Choose("YOUR OVERLAY","ВАШ ОВЕРЛЕЙ"),17*u,Theme.Gold,new RectangleF(16*u,18*u,Width-32*u,28*u),true);
   Design.Text(g,L.Choose("Sample data · drag a panel · drag its corner to resize","Пример данных · двигайте блоки · тяните угол для размера"),11*u,Theme.Muted,new RectangleF(16*u,51*u,Width-32*u,35*u));
   float stageWidth=Math.Max(1,Math.Min(Width-32*u,Height*.37f*16/9));screen=new RectangleF((Width-stageWidth)/2,92*u,stageWidth,stageWidth*9/16);
   using(var b=new SolidBrush(Color.FromArgb(35,48,51)))g.FillRectangle(b,screen);Design.Cover(g,art.Get("background"),Rectangle.Round(screen));
   using(var b=new SolidBrush(Color.FromArgb(65,Theme.Background)))g.FillRectangle(b,screen);
   var renderSettings=Json.Serializer().Deserialize<Settings>(Json.Serializer().Serialize(settings));renderSettings.EditLayout=true;
   var area=new Rectangle(0,0,1920,1080);state.Apply(DemoConnector.Create(Guid.NewGuid().ToString(),25),DateTime.UtcNow);state.Spectator=true;state.SourceStatus=L.Choose("SAMPLE","ПРИМЕР");
   int k=0;foreach(var p in state.Players.Values){p.Lane=k%3+1;p.HudOrder=k++;p.UnspentSouls=450+p.Slot*120;}
   var map=new MapSnapshot{Seconds=500,NextBoss=720,Fresh=true,ScreenClock=true};
   for(int i=0;i<6;i++){
    var w=widgets[i];var hud=w as HudOverlay;if(hud!=null)hud.Map=map;
    ((IOverlayWidget)w).Refresh(state,renderSettings,area,true);var bounds=WidgetLayout.Bounds(settings,area,i);
    hits[i]=new RectangleF(screen.X+bounds.X*screen.Width/1920,screen.Y+bounds.Y*screen.Height/1080,bounds.Width*screen.Width/1920,bounds.Height*screen.Height/1080);
    if(settings.ShowHudPart(i)){using(var pixels=w.RenderPixels())g.DrawImage(pixels,hits[i]);}

   }
   using(var pen=new Pen(Theme.Green,2))g.DrawRectangle(pen,Rectangle.Round(hits[selected]));using(var brush=new SolidBrush(Theme.Green))g.FillRectangle(brush,hits[selected].Right-7,hits[selected].Bottom-7,8,8);
   float y=screen.Bottom+24*u;Design.Text(g,LayoutHandle.Names[selected],15*u,Theme.Gold,new RectangleF(16*u,y,Width-32*u,25*u),true);
   Design.Text(g,L.Choose("Size and opacity controls are on the left","Размер и прозрачность настраиваются слева"),11*u,Theme.Muted,new RectangleF(16*u,y+28*u,Width-32*u,25*u));
   var available=new RectangleF(16*u,y+64*u,Width-32*u,Math.Max(1,Height-y-80*u));using(var b=new SolidBrush(Color.FromArgb(38,51,55)))g.FillRectangle(b,available);
   using(var pixels=widgets[selected].RenderPixels()){float scale=Math.Min(available.Width/pixels.Width,1.15f*u);var saved=g.Save();g.SetClip(available);g.DrawImage(pixels,new RectangleF(available.X+(available.Width-pixels.Width*scale)/2,available.Y,pixels.Width*scale,pixels.Height*scale));g.Restore(saved);}
  }
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;for(int i=5;i>=0;i--)if((settings.ShowHudPart(i)||i==selected)&&hits[i].Contains(e.Location)){Selected=i;Focus();origin=e.Location;var p=settings.Placement(i);oldX=(hits[i].X-screen.X)/Math.Max(1,screen.Width-hits[i].Width);oldY=(hits[i].Y-screen.Y)/Math.Max(1,screen.Height-hits[i].Height);oldScale=p.Scale;initialWidth=hits[i].Width;resizing=Math.Abs(e.X-hits[i].Right)<12&&Math.Abs(e.Y-hits[i].Bottom)<12;dragging=true;Capture=true;return;}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;var p=settings.Placement(selected);if(resizing)p.Scale=Math.Max(.5,Math.Min(2,oldScale*(1+(e.X-origin.X)/Math.Max(20,initialWidth))));else {p.X=Math.Max(0,Math.Min(1,oldX+(e.X-origin.X)/Math.Max(1,screen.Width-hits[selected].Width)));p.Y=Math.Max(0,Math.Min(1,oldY+(e.Y-origin.Y)/Math.Max(1,screen.Height-hits[selected].Height)));}Invalidate();}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;if(Edited!=null)Edited();}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&dragging){dragging=false;if(Edited!=null)Edited();}}

  protected override void Dispose(bool disposing){if(disposing){foreach(var w in widgets)w.Dispose();advice.Dispose();images.Dispose();art.Dispose();}base.Dispose(disposing);}
 }
}
