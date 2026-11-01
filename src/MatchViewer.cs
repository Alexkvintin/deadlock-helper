using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Text;using System.Linq;using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class MatchViewer:Form {
  readonly TextBox input=new TextBox{Width=150};readonly Label status=new Label{Dock=DockStyle.Top,Height=40,Padding=new Padding(16,8,0,0)};readonly ViewerCanvas canvas=new ViewerCanvas();readonly CheckBox healing=new CheckBox{AutoSize=true,Checked=true};long sequence=-2;string stamp="";
  public MatchViewer(Action<long> connect){Text="Deadlock · Match viewer";Size=new Size(1100,700);MinimumSize=new Size(900,640);BackColor=Theme.Background;ForeColor=Theme.Ink;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
   var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=58,Padding=new Padding(14,10,0,0)};var button=new Button{Text=L.Choose("Connect","Подключиться"),Width=140};healing.Text=L.Choose("Show healing","Показать лечение");bar.Controls.Add(new Label{Text="Match ID",AutoSize=true,Margin=new Padding(0,8,12,0)});bar.Controls.Add(input);bar.Controls.Add(button);bar.Controls.Add(healing);
   button.Click+=(s,e)=>{long id;if(!long.TryParse(input.Text.Trim(),out id)||id<=0){status.Text=L.Choose("Enter a numeric match ID","Введите числовой ID матча");return;}connect(id);};healing.CheckedChanged+=(s,e)=>{canvas.Healing=healing.Checked;canvas.Invalidate();};canvas.Dock=DockStyle.Fill;Controls.Add(canvas);Controls.Add(status);Controls.Add(bar);Theme.Skin(bar);FormClosing+=(s,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
  }
  public void SelectMatch(long id){input.Text=id>0?id.ToString():"";sequence=-2;stamp="";status.Text=L.Choose("Connecting to match ","Подключение к матчу ")+id;}
  public void UpdateState(MatchState state,long selected,bool final){if(!Visible)return;string next=L.Language+":"+state.SourceStatus+":"+final+":"+(DateTime.UtcNow-state.Received).TotalSeconds.ToString("0");if(sequence==state.Sequence&&stamp==next)return;sequence=state.Sequence;stamp=next;status.Text=(selected>0?"#"+selected+" · ":"")+(final?L.Choose("Final totals","Итоговые значения"):L.ToLanguage(state.SourceStatus));canvas.State=state;canvas.Final=final;canvas.Invalidate();}
 }
 public sealed class ViewerCanvas:Control {
  public MatchState State=new MatchState();public bool Healing=true,Final;readonly HeroImages images=new HeroImages();readonly Font heading=new Font("Segoe UI Semibold",16),label=new Font("Segoe UI",10,FontStyle.Bold),number=new Font("Segoe UI",12,FontStyle.Bold);
  public ViewerCanvas(){DoubleBuffered=true;BackColor=Theme.Background;Resize+=(s,e)=>Invalidate();}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;float w=(Width-48)/2f;for(int t=0;t<2;t++){float x=16+t*(w+16);var rows=State.Players.Values.Where(p=>p.Team==t+2).OrderByDescending(p=>p.Damage??-1).ToArray();
   Draw(g,Patrons.Name(t+2),heading,Theme.Gold,x+12,6,w-24);float col=w/3;
   string[] titles={L.T("ГЕРОИ"),L.T("СТРОЕНИЯ"),L.T("ЛЕЧЕНИЕ")};Color[] colors={Theme.HeroDamage,Theme.StructureDamage,Theme.Healing};
   Func<Player,long?>[] getters={p=>p.Damage,p=>p.ObjectDamage,p=>p.Healing};
   for(int m=0;m<(Healing?3:2);m++){bool known=rows.Length==6&&rows.All(p=>Valid(p)&&getters[m](p).HasValue);Draw(g,titles[m],label,colors[m],x+12+m*col,38,col-16);Draw(g,known?OverlayRenderer.Format(rows.Sum(p=>getters[m](p).Value),true):"—",number,colors[m],x+12+m*col,57,col-16);}
   for(int i=0;i<6;i++){float y=90+i*69;Theme.Fill(g,Theme.Card,new RectangleF(x,y,w,61),12);if(i>=rows.Length){Draw(g,L.Choose("Waiting for player…","Ожидание игрока…"),label,Theme.Muted,x+14,y+20,w-28);continue;}var p=rows[i];var icon=images.Get(p.Hero);if(icon!=null)g.DrawImage(icon,x+10,y+10,40,40);Draw(g,p.Name,label,Theme.Ink,x+60,y+3,w-74);Theme.DrawHeroBalance(g,p.HeroName,p.UnspentSouls,Valid(p),label,x+60,y+21,w-74);float inner=(w-74)/3;for(int m=0;m<(Healing?3:2);m++)Draw(g,Valid(p)&&getters[m](p).HasValue?OverlayRenderer.Format(getters[m](p).Value,false):"—",number,colors[m],x+60+m*inner,y+39,inner-4);}
  }}
  bool Valid(Player p){return Final||(State.Healthy&&State.Connected(DateTime.UtcNow)&&(DateTime.UtcNow-p.Updated).TotalSeconds<15);}
  static void Draw(Graphics g,string text,Font font,Color color,float x,float y,float width){using(var b=new SolidBrush(color))using(var f=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text,font,b,new RectangleF(x,y,Math.Max(1,width),30),f);}
  protected override void Dispose(bool disposing){if(disposing){images.Dispose();heading.Dispose();label.Dispose();number.Dispose();}base.Dispose(disposing);}
 }
}

