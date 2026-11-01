using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class HudOverlay : AlphaWindow,IOverlayWidget {
  readonly HudAdvice advice;readonly int part;readonly Dictionary<long,Image> icons=new Dictionary<long,Image>();
  readonly ArtCache art=new ArtCache();
  readonly Font small=new Font("Segoe UI Semibold",12,FontStyle.Bold,GraphicsUnit.Pixel),timerFont=new Font("Segoe UI Semibold",25,FontStyle.Bold,GraphicsUnit.Pixel);
  Player[] left=new Player[0],right=new Player[0];MapSnapshot map=new MapSnapshot();string signature="",layoutSignature="";float scale=1;bool editing;
  public bool SampleItems;
  public MapSnapshot Map{set{map=value??new MapSnapshot();}}
  public HudOverlay(HudAdvice advice,int part=2){this.advice=advice;this.part=part;StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Theme.Background;AutoScaleMode=AutoScaleMode.None;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x08000000|0x80;return p;}}
  protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
  public static int LeftTeam(MatchState state){return !state.Spectator&&state.LocalTeam.HasValue?state.LocalTeam.Value:2;}
  public static Player[] Ordered(MatchState s,int team){var rows=s.Players.Values.Where(p=>p.Team==team).ToArray();if(rows.Length!=6||rows.Any(p=>!p.Lane.HasValue||p.Lane<=0))return new Player[0];return rows.OrderBy(p=>p.Lane).ThenBy(p=>p.HudOrder??p.Slot).ToArray();}
  public void Refresh(MatchState state,Settings settings,Rectangle bounds,bool show){
   editing=settings.EditLayout;if((!show||!settings.ShowHudPart(part)||map.StreetBrawl)&&!editing){if(Visible)Hide();return;}
   if(part==2){int leftTeam=LeftTeam(state);left=Ordered(state,leftTeam);right=Ordered(state,5-leftTeam);}var placement=settings.Placement(part);scale=DisplayLayout.HudScale(bounds,placement.Scale,part);int width=part==2?1750:part==5?196:140;
   if(width*scale>bounds.Width)scale=bounds.Width/(float)width;
   var desired=WidgetLayout.Bounds(settings,bounds,part,settings.EditLayout?2:LeftTeam(state));bool moved=Bounds!=desired;if(moved)Bounds=desired;BackgroundOpacity=placement.Opacity;
   string layout=part==2?advice.Revision+":"+string.Join(",",left.Concat(right).Select(p=>p.Hero+"/"+p.Lane+"/"+p.HudOrder)):"";
   string value=part==2?layout:part==3?map.CrystalText:part==4?map.BossText:map.Rift+":"+map.RiftText;
   string sig=L.Language+":"+state.MatchId+":"+editing+":"+BackgroundOpacity+":"+value;
   if(moved||layout!=layoutSignature){layoutSignature=layout;}
   if(sig!=signature||moved){signature=sig;Present();}if(!PreviewOnly&&!Visible)Show();
  }
  Image ItemIcon(long id){Image image;if(icons.TryGetValue(id,out image))return image;string file=Path.Combine(HudAdvice.IconFolder,id+".png");if(!File.Exists(file)&&PreviewOnly)file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","ui",id+".png");try{if(File.Exists(file)){using(var source=Image.FromFile(file))image=new Bitmap(source);icons[id]=image;return image;}}catch(Exception e){Log.Write("Item icon",e);}return null;}
  protected override void DrawOverlay(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;g.ScaleTransform(scale,scale);float center=Width/scale/2;
   if(part!=2)Theme.Fill(g,Surface(Theme.Background),new RectangleF(1,1,Width/scale-2,Height/scale-2),12);
   if(part!=2)Design.Frame(g,new RectangleF(1,1,Width/scale-2,Height/scale-2),Theme.Gold);
   using(var muted=new SolidBrush(Theme.Muted))using(var ink=new SolidBrush(Theme.Ink))using(var gold=new SolidBrush(Theme.Gold)){
    if(part==3||part==4){DrawTimer(g,part==3?L.T("КРИСТАЛЛЫ"):L.T("МИДБОСС"),part==3?map.CrystalText:map.BossText,gold,ink);return;}
    if(part==5){
     Color color=map.Rift=="green"?Theme.Healing:map.Rift=="yellow"?Theme.HeroDamage:Theme.StructureDamage;
     var riftImage=art.Get("rift");
     if(riftImage!=null)g.DrawImage(riftImage,10,28,34,50);
     using(var brush=new SolidBrush(color)){
      // No guessed substitute for the game icon when the optional asset is absent.
      OverlayText.Draw(g,L.Choose("UNSTABLE RIFT","НЕСТАБИЛЬНЫЙ РИФТ"),small,brush,new RectangleF(12,10,172,20));
     }
     OverlayText.Draw(g,map.RiftValue,timerFont,ink,riftImage==null?12:49,34);OverlayText.Draw(g,map.RiftCaption,small,muted,new RectangleF(12,73,172,22));return;
    }
    for(int team=0;team<2;team++){var players=team==0?left:right;for(int i=0;i<players.Length;i++){var p=players[i];float x=center+(team==0?-780+i*117:194+i*117);var items=advice.Get(p.Hero);if(PreviewOnly&&SampleItems)items=new[]{new ItemSuggestion{Id=1009965641},new ItemSuggestion{Id=1047818222}};if(items.Length==0)continue;Theme.Fill(g,Surface(Theme.Background),new RectangleF(x-43,7,86,66),9);
     for(int j=0;j<2;j++){var rect=new RectangleF(x-36+j*38,13,34,34);var icon=j<items.Length?ItemIcon(items[j].Id):null;if(icon!=null)g.DrawImage(icon,rect);else OverlayText.Draw(g,"·",timerFont,muted,rect.Location);}
     using(var format=new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})OverlayText.Draw(g,p.HeroName,small,muted,new RectangleF(x-42,52,84,16),format);
    }}

   }
  }
  void DrawTimer(Graphics g,string name,string value,Brush accent,Brush ink){var icon=art.Get(part==3?"crystals":"midboss");if(icon!=null)g.DrawImage(icon,12,12,24,24);OverlayText.Draw(g,name,small,accent,icon==null?14:40,13);OverlayText.Draw(g,value,timerFont,ink,13,43);}
  protected override void Dispose(bool disposing){if(disposing){art.Dispose();foreach(var icon in icons.Values)icon.Dispose();small.Dispose();timerFont.Dispose();}base.Dispose(disposing);}
 }
}



