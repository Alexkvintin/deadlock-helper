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
   var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var canvas=new PlayerDetailCanvas(summary,player){Width=1080};scroll.Controls.Add(canvas);scroll.Resize+=(a,e)=>canvas.Width=Math.Max(760,scroll.ClientSize.Width-22);Controls.Add(scroll);Shown+=(a,e)=>Design.Fit(this);
  }
 }
 public sealed class PlayerDetailCanvas : Control {
  readonly MatchSummary summary;readonly Player player;readonly ImpactRow row;readonly HeroImages portraits=new HeroImages();readonly ArtCache art=new ArtCache();readonly ToolTip tooltip=new ToolTip();int selected;
  public PlayerDetailCanvas(MatchSummary s,Player p){summary=s;player=p;row=s.Rank().First(x=>x.Player.Slot==p.Slot);DoubleBuffered=true;BackColor=Theme.Background;Height=720;Resize+=(a,e)=>{Height=(int)(720*Width/1080f);Invalidate();};}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.ScaleTransform(Width/1080f,Width/1080f);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   var heroArt=art.Get("hero-"+player.Hero);if(heroArt!=null)Design.Cover(g,heroArt,new Rectangle(0,0,290,245));else{var portrait=portraits.Get(player.Hero);if(portrait!=null)g.DrawImage(portrait,45,35,180,180);}
   Design.Text(g,L.Choose("PLAYER / MATCH ","ИГРОК / МАТЧ ")+summary.MatchId,12,Theme.Gold,new RectangleF(310,26,700,22),true);Design.Text(g,player.Name,34,Theme.Ink,new RectangleF(310,57,690,48),true);Design.Text(g,player.HeroName+" · "+Patrons.Name(player.Team),16,Theme.Muted,new RectangleF(310,110,650,25));
   Design.Text(g,row.Score.HasValue?row.Score.Value.ToString("0.0")+" "+row.Grade:"—",34,Theme.Green,new RectangleF(310,152,160,50),true);Design.Text(g,L.Choose("IMPACT SCORE","ОЦЕНКА ВКЛАДА"),11,Theme.Muted,new RectangleF(310,205,190,23));
   Design.Text(g,MatchSummary.N(player.Kills)+" / "+MatchSummary.N(player.Deaths)+" / "+MatchSummary.N(player.Assists),26,Theme.Ink,new RectangleF(530,164,245,40),true);Design.Text(g,"K / D / A",11,Theme.Muted,new RectangleF(530,205,245,23));
   Design.Text(g,MatchSummary.N(player.Souls),26,Theme.SoulsBlue,new RectangleF(806,164,245,40),true);Design.Text(g,L.Choose("NET WORTH","ЦЕННОСТЬ ГЕРОЯ"),11,Theme.Muted,new RectangleF(806,205,245,23));
   string[] tabs={L.Choose("Overview","Обзор"),L.Choose("Progression","Динамика"),L.Choose("Items","Предметы"),L.Choose("Play style","Стиль игры")};
   for(int i=0;i<4;i++){var r=new RectangleF(24+i*258,260,250,42);Theme.Fill(g,selected==i?Color.FromArgb(28,66,60):Theme.Card,r,6);Design.Text(g,tabs[i],15,selected==i?Theme.Green:Theme.Ink,new RectangleF(r.X+16,r.Y+11,220,24),true);}
   if(selected==1||selected==2){Theme.Fill(g,Theme.Card,new RectangleF(24,325,1032,300),9);Design.Text(g,selected==1?L.Choose("No timeline recorded","История изменений не записана"):L.Choose("Purchase history is unavailable","История покупок недоступна"),24,Theme.Ink,new RectangleF(55,380,950,40),true);Design.Text(g,L.Choose("This report contains aggregate match totals. No values have been estimated.","В отчёте есть только итоговые показатели. Недостающие значения не вычисляются."),15,Theme.Muted,new RectangleF(55,437,940,35));return;}
   Theme.Fill(g,Theme.Card,new RectangleF(24,325,410,340),9);Design.Frame(g,new RectangleF(24,325,410,340),Design.Line);Design.Text(g,L.Choose("WITHIN THIS MATCH","СРАВНЕНИЕ В ЭТОМ МАТЧЕ"),13,Theme.Gold,new RectangleF(44,344,370,25),true);
   var values=SummaryView.Values(row);for(int ring=1;ring<=4;ring++)using(var pen=new Pen(Color.FromArgb(53,72,73)))g.DrawPolygon(pen,SummaryView.Points(Enumerable.Repeat(ring/4.0,5).ToArray(),230,497,105));
   if(row.Score.HasValue){var points=SummaryView.Points(values,230,497,105);using(var fill=new SolidBrush(Color.FromArgb(65,Theme.Green)))g.FillPolygon(fill,points);using(var pen=new Pen(Theme.Green,2))g.DrawPolygon(pen,points);}
   PointF[] labels={new PointF(185,373),new PointF(327,451),new PointF(282,590),new PointF(84,590),new PointF(46,451)};
   for(int i=0;i<5;i++)Design.Text(g,SummaryView.Labels[i],10,Theme.Muted,new RectangleF(labels[i].X,labels[i].Y,100,20));
   Design.Text(g,L.Choose("Relative to the best value among these 12 players","Относительно максимума среди этих 12 игроков"),11,Theme.Muted,new RectangleF(44,630,370,23));
   Theme.Fill(g,Theme.Card,new RectangleF(454,325,602,340),9);Design.Frame(g,new RectangleF(454,325,602,340),Design.Line);
   Design.Text(g,selected==3?L.Choose("STRENGTHS / CONTRIBUTION","СИЛЬНЫЕ СТОРОНЫ / ВКЛАД"):L.Choose("PERFORMANCE BREAKDOWN","ПОКАЗАТЕЛИ ИГРОКА"),13,Theme.Gold,new RectangleF(474,344,562,25),true);
   long?[] raw={player.Damage,player.ObjectDamage,player.Healing,null,player.Souls};Color[] colors={Theme.HeroDamage,Theme.StructureDamage,Theme.Healing,Theme.Green,Theme.SoulsBlue};
   for(int i=0;i<5;i++){float y=386+i*44;Design.Text(g,SummaryView.Labels[i],13,Theme.Muted,new RectangleF(474,y,230,24));Design.Text(g,i==3?(row.Score.HasValue?(values[i]*100).ToString("0")+"%":"—"):MatchSummary.N(raw[i]),16,colors[i],new RectangleF(852,y,180,24),true);Theme.Fill(g,Color.FromArgb(42,58,62),new RectangleF(474,y+28,557,4),2);if(row.Score.HasValue&&values[i]>0)Theme.Fill(g,colors[i],new RectangleF(474,y+28,(float)(557*values[i]),4),Math.Min(2,(float)(557*values[i]/2)));}
   Design.Text(g,SummaryView.Strength(row),13,Theme.Gold,new RectangleF(24,686,1032,24));
  }
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);float scale=Width/1080f;float x=e.X/scale,y=e.Y/scale;if(y>=260&&y<=302&&x>=24&&x<1056){selected=Math.Min(3,(int)((x-24)/258));Invalidate();}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);float scale=Width/1080f;float y=e.Y/scale;Cursor=y>=260&&y<=302?Cursors.Hand:Cursors.Default;tooltip.SetToolTip(this,y>325?L.Choose("Scores compare players in this match, not the global player population.","Оценки сравнивают участников этого матча, а не всех игроков."):"");}
  protected override void Dispose(bool disposing){if(disposing){portraits.Dispose();art.Dispose();tooltip.Dispose();}base.Dispose(disposing);}
 }
}
