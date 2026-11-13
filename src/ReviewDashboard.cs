using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class ReviewDashboard : Control {
  readonly MatchSummary summary;readonly ImpactRow[] rows;readonly HeroImages portraits=new HeroImages();readonly ArtCache art=new ArtCache();
  readonly ToolTip tooltip=new ToolTip();RectangleF[] cards=new RectangleF[0];int metric,hover=-1;float factor=1;
  public event Action<Player> PlayerSelected;
  public static string[] Tabs{get{return new[]{L.Choose("Overview","Обзор"),L.Choose("Economy","Экономика"),L.Choose("Damage","Урон"),L.Choose("Healing","Лечение"),L.Choose("Structures","Строения")};}}
  public int Metric{get{return metric;}set{metric=Math.Max(0,Math.Min(4,value));Invalidate();}}
  public ReviewDashboard(MatchSummary s){summary=s;rows=s.Rank().OrderBy(x=>x.Player.Team).ThenByDescending(x=>x.Score??-1).ToArray();DoubleBuffered=true;BackColor=Theme.Background;Cursor=Cursors.Hand;Height=760;Resize+=(a,e)=>{Height=(int)(760*Width/1160f);Invalidate();};AccessibleName=L.Choose("Match player cards. Select a player for details.","Карточки игроков. Выберите игрока для подробностей.");}
  long? Value(Player p){return metric==1?p.Souls:metric==3?p.Healing:metric==4?p.ObjectDamage:p.Damage;}
  Color Accent{get{return metric==1?Theme.SoulsBlue:metric==3?Theme.Healing:metric==4?Theme.StructureDamage:Theme.HeroDamage;}}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;factor=Width/1160f;g.ScaleTransform(factor,factor);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   var background=art.Get("background");Design.Cover(g,background,new Rectangle(0,0,1160,760));using(var shade=new SolidBrush(Color.FromArgb(225,Theme.Background)))g.FillRectangle(shade,0,0,1160,760);
   Design.Text(g,"DEADLOCK / MATCH REVIEW",12,Theme.Gold,new RectangleF(24,20,600,22),true);
   string winner=summary.WinningTeam.HasValue?Patrons.Name(summary.WinningTeam.Value)+L.Choose(" · VICTORY"," · ПОБЕДА"):L.Choose("MATCH REVIEW","РАЗБОР МАТЧА");
   Design.Text(g,winner,30,Theme.Ink,new RectangleF(24,43,850,44),true);
   string duration=summary.DurationSeconds.HasValue?(summary.DurationSeconds.Value/60).ToString("00")+":"+(summary.DurationSeconds.Value%60).ToString("00"):"—";
   Design.Text(g,"#"+summary.MatchId+"   ·   "+L.Choose("Duration ","Длительность ")+duration+"   ·   "+summary.Captured.ToLocalTime().ToString("dd MMM yyyy",System.Globalization.CultureInfo.GetCultureInfo(L.Language)),13,Theme.Muted,new RectangleF(24,87,1000,24));
   Design.Text(g,L.ToLanguage(summary.Reason),12,Theme.Muted,new RectangleF(24,111,1100,24));
   for(int team=0;team<2;team++){float x=24+team*568;var logo=art.Get("patron-"+(team+2));if(logo!=null)g.DrawImage(logo,x,143,30,30);Design.Text(g,Patrons.Name(team+2).ToUpperInvariant(),16,team==0?Theme.Gold:Theme.SoulsBlue,new RectangleF(x+40,148,480,26),true);}
   cards=new RectangleF[rows.Length];var best=rows.Where(x=>x.Score.HasValue).OrderByDescending(x=>x.Score).FirstOrDefault();
   for(int i=0;i<rows.Length;i++){var r=rows[i];var p=r.Player;int team=p.Team==2?0:1;int position=rows.Take(i).Count(candidate=>candidate.Player.Team==p.Team);float x=24+team*568+position%6*91,y=190;var box=new RectangleF(x,y,86,222);cards[i]=box;
    Theme.Fill(g,i==hover?Color.FromArgb(29,54,56):Theme.Card,box,7);Design.Frame(g,box,r==best?Theme.Green:Design.Line);var image=portraits.Get(p.Hero);if(image!=null)g.DrawImage(image,x+13,y+13,60,60);else Design.Text(g,p.HeroName,12,Theme.Muted,new RectangleF(x+7,y+16,72,45));
    Design.Text(g,p.Name,12,Theme.Ink,new RectangleF(x+7,y+82,72,20),true);Design.Text(g,p.HeroName,11,Theme.Muted,new RectangleF(x+7,y+102,72,20));
    Design.Text(g,r.Score.HasValue?r.Score.Value.ToString("0.0")+" "+r.Grade:"—",22,r==best?Theme.Green:Theme.Gold,new RectangleF(x+7,y+128,77,30),true);
    Design.Text(g,MatchSummary.N(p.Kills)+" / "+MatchSummary.N(p.Deaths)+" / "+MatchSummary.N(p.Assists),11,Theme.Ink,new RectangleF(x+7,y+164,77,19));
    Design.Text(g,Compact(p.Damage)+"  /  "+Compact(p.Healing),11,Theme.HeroDamage,new RectangleF(x+7,y+187,77,19));
   }
   Design.Text(g,L.Choose("K / D / A · damage / healing   |   Click a player to explore","K / D / A · урон / лечение   |   Нажмите на игрока для подробностей"),12,Theme.Muted,new RectangleF(24,426,1100,24));
   Theme.Fill(g,Theme.Card,new RectangleF(24,464,550,266),9);Design.Frame(g,new RectangleF(24,464,550,266),Design.Line);
   Design.Text(g,Tabs[metric].ToUpperInvariant()+L.Choose(" / TEAM COMPARISON"," / СРАВНЕНИЕ КОМАНД"),14,Theme.Gold,new RectangleF(42,482,515,24),true);
   Func<Player,long?>[] values=metric==0?new Func<Player,long?>[]{p=>p.Damage,p=>p.Healing,p=>p.ObjectDamage}:new Func<Player,long?>[]{Value};
   string[] labels=metric==0?new[]{L.Choose("Hero damage","Урон героям"),L.Choose("Healing","Лечение"),L.Choose("Structure damage","Урон строениям")}:new[]{Tabs[metric]};
   for(int m=0;m<values.Length;m++){var get=values[m];bool valid=rows.Length==12&&rows.All(r=>get(r.Player).HasValue);long a=rows.Where(r=>r.Player.Team==2).Sum(r=>get(r.Player)??0),b=rows.Where(r=>r.Player.Team==3).Sum(r=>get(r.Player)??0);float y=520+m*65;
    Design.Text(g,labels[m],12,Theme.Muted,new RectangleF(42,y,200,20));Design.Text(g,valid?MatchSummary.N(a)+"   :   "+MatchSummary.N(b):L.Choose("Incomplete data","Неполные данные"),14,Theme.Ink,new RectangleF(250,y,300,24),true);
    Theme.Fill(g,Color.FromArgb(48,64,70),new RectangleF(42,y+30,508,7),3);if(valid&&a+b>0){float w=508f*a/(a+b);if(w>0)Theme.Fill(g,Theme.Gold,new RectangleF(42,y+30,w,7),Math.Min(3,w/2));}
   }
   Theme.Fill(g,Theme.Card,new RectangleF(592,464,544,266),9);Design.Frame(g,new RectangleF(592,464,544,266),Design.Line);Design.Text(g,L.Choose("STANDOUT PERFORMANCE","ЛИДЕР ВКЛАДА"),14,Theme.Gold,new RectangleF(612,482,504,24),true);
   if(best!=null){var img=portraits.Get(best.Player.Hero);if(img!=null)g.DrawImage(img,612,524,88,88);Design.Text(g,best.Player.Name,24,Theme.Ink,new RectangleF(718,521,390,34),true);Design.Text(g,best.Player.HeroName+" · "+Patrons.Name(best.Player.Team),13,Theme.Muted,new RectangleF(718,559,390,23));Design.Text(g,best.Score.Value.ToString("0.0")+" "+best.Grade,32,Theme.Green,new RectangleF(718,591,390,45),true);Design.Text(g,SummaryView.Strength(best),14,Theme.Gold,new RectangleF(612,652,500,24));}
   else Design.Text(g,L.Choose("Not enough data for an impact score","Недостаточно данных для оценки вклада"),16,Theme.Muted,new RectangleF(612,540,500,35));
  }
  static string Compact(long? value){return value.HasValue?OverlayRenderer.Format(value.Value,true):"—";}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int hit=Array.FindIndex(cards,r=>r.Contains(e.X/factor,e.Y/factor));if(hit==hover)return;hover=hit;tooltip.SetToolTip(this,hit<0?"":rows[hit].Player.Name+" · "+rows[hit].Player.HeroName+"\n"+L.Choose("Click for player details","Нажмите для подробностей"));Invalidate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=-1;Invalidate();}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);int hit=Array.FindIndex(cards,r=>r.Contains(e.X/factor,e.Y/factor));if(hit>=0&&PlayerSelected!=null)PlayerSelected(rows[hit].Player);}
  protected override void Dispose(bool disposing){if(disposing){portraits.Dispose();art.Dispose();tooltip.Dispose();}base.Dispose(disposing);}
 }
 public sealed class PlayerDetailWindow : Form {
  protected override bool ShowWithoutActivation{get{return true;}}
  public PlayerDetailWindow(MatchSummary summary,Player player){Text=player.Name+" · "+player.HeroName;Icon=Design.AppIcon;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(1120,800);MinimumSize=new Size(800,550);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;TopMost=true;
   var canvas=new PlayerDetailCanvas(summary,player){Dock=DockStyle.Fill};Controls.Add(canvas);Shown+=(a,e)=>Design.Fit(this);
  }
 }
}
