using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed partial class ScoutView:Control {
  public int? UserTeam;public ScoutRow[] Rows=new ScoutRow[0];public ScoutService Service;public ScoutOptions Options;public string Phase="";
  public Dictionary<long,PartyInfo> Parties=new Dictionary<long,PartyInfo>();
  readonly ScoutRankImages ranks=new ScoutRankImages();readonly HeroImages portraits=new HeroImages();readonly ToolTip tooltip=new ToolTip();readonly ScrollRail rail=new ScrollRail();
  readonly List<KeyValuePair<RectangleF,string>> tips=new List<KeyValuePair<RectangleF,string>>();readonly List<KeyValuePair<RectangleF,ScoutRow>> hits=new List<KeyValuePair<RectangleF,ScoutRow>>();
  readonly TextBox search=new TextBox{BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Card,ForeColor=Theme.Ink};
  readonly Dictionary<long,RowData> summaries=new Dictionary<long,RowData>();
  sealed class RowData {public ScoutProfile Profile;public string Key;public ScoutMatch[] History;public PlayerInsight[] Tags;public int Main;}
  long selected;int offset,extent;string tip="";float dpiOverride;
  // Deterministic renderer seam for offscreen DPI regression tests. Production uses DeviceDpi.
  public void SetReviewDpi(float dpi){if(!UiReview.Active)throw new InvalidOperationException();dpiOverride=dpi;Invalidate();}
  float Zoom{get{return dpiOverride>0?dpiOverride/96f:DeviceDpi/96f;}}
  int W{get{return Math.Max(200,(int)((Width-14)/Zoom));}}int H{get{return (int)(Height/Zoom);}}
  public ScoutView(){DoubleBuffered=true;BackColor=Theme.Background;ForeColor=Theme.Ink;SetStyle(ControlStyles.ResizeRedraw,true);Font=new Font("Bahnschrift",10);TabStop=true;Controls.Add(rail);Controls.Add(search);search.AccessibleName="Find player / Найти игрока";search.TextChanged+=(a,e)=>{offset=0;HideTip();Invalidate();};search.KeyDown+=(a,e)=>{if(e.KeyCode==Keys.Enter){var row=Rows.FirstOrDefault(r=>MatchesSearch(r));if(row!=null){OpenProfile(row);Focus();}e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){search.Clear();e.SuppressKeyPress=true;}};tooltip.InitialDelay=350;tooltip.ReshowDelay=100;tooltip.AutoPopDelay=6000;tooltip.ShowAlways=false;tooltip.Popup+=(a,e)=>{var cursor=PointToClient(Cursor.Position);var point=new PointF(cursor.X/Zoom,cursor.Y/Zoom);if(!Visible||tip.Length==0||!tips.Any(t=>t.Key.Contains(point)))e.Cancel=true;};rail.Seek+=v=>{HideTip();offset=v;Invalidate();};}
  protected override void OnResize(EventArgs e){base.OnResize(e);rail.Bounds=new Rectangle(Math.Max(0,Width-10),8,8,Math.Max(1,Height-16));search.SetBounds(Math.Max(180,Width-(int)(210*Zoom)),(int)(12*Zoom),(int)(190*Zoom),(int)(26*Zoom));}
  string T(string en,string ru){return L.Choose(en,ru);}
  void TextAt(Graphics g,string text,float px,Color color,RectangleF box){using(var font=new Font("Bahnschrift",px,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color))using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.LineLimit})g.DrawString(text,font,brush,box,format);}
  public static string Rank(int badge){string[] names={"Unranked","Initiate","Seeker","Alchemist","Arcanist","Ritualist","Emissary","Archon","Oracle","Phantom","Ascendant","Eternus"};return badge>=10&&badge/10<names.Length?names[badge/10]+" "+badge%10:"—";}
  RowData Data(ScoutRow row){var profile=Service.Get(row.Account);RowData data;var o=Options??new ScoutOptions();string key=row.Hero+":"+L.Language+":"+o.Sample+":"+o.SmurfPercent+":"+o.Leaves+":"+o.DisabledTags;if(!summaries.TryGetValue(row.Account,out data)||!ReferenceEquals(data.Profile,profile)||data.Key!=key){data=new RowData{Profile=profile,Key=key,History=ScoutStatistics.History(profile),Tags=PlayerInsightEngine.Analyze(profile,row.Hero,o),Main=ScoutStatistics.MainHero(profile)};summaries[row.Account]=data;}return data;}
  void ScrollRange(int content,int viewport){extent=Math.Max(0,content-viewport);offset=Math.Min(offset,extent);rail.SetRange(extent,viewport,offset);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);tips.Clear();hits.Clear();if(Service==null)return;Options=Options??new ScoutOptions();var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(Zoom,Zoom);
   var focused=Rows.FirstOrDefault(r=>r.Account==selected);int top=110;profileActions.Clear();search.Visible=focused==null;if(focused!=null){DrawDetail(g,focused,0);return;}
   TextAt(g,focused==null?"SCOUT":T("PLAYER PROFILE","ПРОФИЛЬ ИГРОКА"),23,Theme.Gold,new RectangleF(16,10,W-32,32));int loaded=Rows.Count(r=>Service.Get(r.Account).Loaded);
   TextAt(g,Phase,13,Theme.Ink,new RectangleF(16,46,Math.Max(80,W-240),21));TextAt(g,T("Find player · Enter to open","Поиск игрока · Enter — открыть"),11,Theme.Muted,new RectangleF(W-210,40,200,30));
   TextAt(g,T("Players ","Игроки ")+Rows.Length+"/12   ·   "+T("History ","История ")+loaded+"/"+Rows.Length+"   ·   "+T("Available sample, not lifetime","Доступная выборка, не вся карьера"),12,Theme.Muted,new RectangleF(16,72,W-32,30));

   if(Rows.Length==0){ScrollRange(0,H);TextAt(g,T("Waiting for roster…\nValid player IDs are needed. No rank or party is guessed.","Ожидание состава…\nНужны ID участников. Ранги и пати не подставляются."),15,Theme.Muted,new RectangleF(16,top,W-32,110));return;}
   var layout=ScoutLayout.Create(W);ScrollRange(layout.Extent,H-top);var saved=g.Save();g.SetClip(new RectangleF(0,top,W,Math.Max(1,H-top)));g.TranslateTransform(0,top-offset);
   for(int t=0;t<2;t++){int team=t==0?3:2;var roster=Rows.Where(r=>r.Team==team&&MatchesSearch(r)).OrderBy(r=>r.Slot).Take(6).ToArray();var header=layout.Headers[t];Color accent=team==3?Color.FromArgb(104,191,245):Theme.Gold;int known=roster.Count(r=>Service.Get(r.Account).Badge>0);
    TextAt(g,(team==3?"ARCHMOTHER":"HIDDEN KING")+"   ·   "+T("ranks ","ранги ")+known+"/"+roster.Length,14,accent,header);
    for(int i=0;i<6;i++){var box=layout.Cards[t*6+i];if(box.Bottom<offset||box.Top>offset+H-top)continue;if(i>=roster.Length){Theme.Fill(g,Color.FromArgb(18,29,34),box,10);TextAt(g,(search.Text.Length>0?T("No matching player","Нет подходящего игрока"):T("Waiting for player","Ожидание игрока")),14,Theme.Muted,new RectangleF(box.X+14,box.Y+15,box.Width-28,25));continue;}
     var row=roster[i];DrawRow(g,row,box,accent);var hit=new RectangleF(box.X,box.Y+top-offset,box.Width,box.Height);hits.Add(new KeyValuePair<RectangleF,ScoutRow>(hit,row));
    }
   }g.Restore(saved);
  }
  void DrawRow(Graphics g,ScoutRow row,Rectangle box,Color accent){var data=Data(row);var p=data.Profile;int x=box.X,y=box.Y,w=box.Width;Theme.Fill(g,Color.FromArgb(23,35,40),box,10);PartyInfo party;if(!Parties.TryGetValue(row.Account,out party))party=PartyInfo.Unknown();
   if(ScoutDecorations.VisibleParty(party,Options)){using(var pen=new Pen(Color.FromArgb(255*Options.PartyIntensity/100,party.Color),1.6f)){if(party.Confidence==PartyConfidence.Likely)pen.DashStyle=DashStyle.Dash;using(var path=Theme.Round(new RectangleF(x+1,y+1,w-2,box.Height-2),10))g.DrawPath(pen,path);}ScoutGlyphs.Draw(g,"party",new RectangleF(x+w-98,y+82,18,18),party.Color);tips.Add(new KeyValuePair<RectangleF,string>(new RectangleF(x+w-98,y+82+110-offset,86,22),ScoutProfileData.Tip(party.Confidence==PartyConfidence.Likely?"likely":"party")));TextAt(g,party.Group+(party.Confidence==PartyConfidence.Likely?"?":""),12,party.Color,new RectangleF(x+w-76,y+82,64,20));}
   var portrait=portraits.Get(row.Hero);if(portrait!=null)g.DrawImage(portrait,x+12,y+11,40,40);
   var rank=ranks.Get(Service.RankImage(p.Badge));if(rank!=null)g.DrawImage(rank,x+w-40,y+9,28,28);
   TextAt(g,string.IsNullOrEmpty(row.Name)?"Account "+row.Account:row.Name,15,Theme.Ink,new RectangleF(x+62,y+9,Math.Max(10,w-116),22));
   TextAt(g,LocalSnapshot.HeroName(row.Hero)+"  ·  "+Rank(p.Badge),12,accent,new RectangleF(x+62,y+32,w-76,19));
   if(!p.Loaded){TextAt(g,!Options.Network?T("Network analysis off","Сетевой анализ выключен"):p.Error.Length>0?T("Source unavailable — retry scheduled","Источник недоступен — повтор запланирован"):T("Loading public statistics…","Загрузка публичной статистики…"),13,Theme.Muted,new RectangleF(x+12,y+62,w-24,42));return;}
   var recent=data.History.Take(20).ToArray();TextAt(g,"WR "+ScoutStatistics.Rate(recent)+"  ·  "+T("last ","посл. ")+recent.Length,13,Theme.Ink,new RectangleF(x+12,y+57,205,20));
   int at=x+12;foreach(var match in data.History.Take(5)){var img=portraits.Get(match.Hero);if(img!=null)g.DrawImage(img,at,y+82,21,21);using(var ink=new SolidBrush(match.Won==true?Theme.Green:match.Won==false?Color.FromArgb(236,116,112):Theme.Muted))g.FillRectangle(ink,at,y+105,21,2);at+=27;}
   int tagX=x+12;var ordered=PlayerInsightEngine.Ordered(data.Tags);foreach(var tag in ordered.Take(4)){var rect=new RectangleF(tagX,y+114,19,19);ScoutGlyphs.Draw(g,tag.Key,rect,Theme.Green);tips.Add(new KeyValuePair<RectangleF,string>(new RectangleF(rect.X,rect.Y+110-offset,rect.Width,rect.Height),ScoutProfileData.Tip(tag.Key)));tagX+=25;}
   if(ordered.Length>4){var more=new RectangleF(tagX,y+114,38,20);TextAt(g,"+"+(ordered.Length-4),13,Theme.Green,more);tips.Add(new KeyValuePair<RectangleF,string>(new RectangleF(more.X,more.Y+110-offset,more.Width,more.Height),string.Join("\n",ordered.Skip(4).Select(t=>ScoutProfileData.Tip(t.Key)))));}
   string main=data.Main==0?"—":LocalSnapshot.HeroName(data.Main);bool stale=(DateTime.UtcNow-p.Fetched).TotalMinutes>=30;TextAt(g,stale?T("Stale ","Устарело ")+p.Fetched.ToLocalTime().ToString("dd/MM HH:mm"):T("Main ","Мейн ")+main,12,stale?Theme.Gold:Theme.Muted,new RectangleF(x+225,y+57,Math.Max(1,w-237),20));
  }
  static string Number(Dictionary<string,object>d,string key){var value=LocalSnapshot.Number(d,key);return value.HasValue?value.Value.ToString("0.0"):"—";}
  bool MatchesSearch(ScoutRow row){string q=search.Text.Trim();return q.Length==0||(row.Name??"").IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||LocalSnapshot.HeroName(row.Hero).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||row.Account.ToString().Contains(q);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Escape&&selected!=0){BackToTeams();e.Handled=true;}if(e.Control&&e.KeyCode==Keys.F&&selected==0){search.Focus();e.Handled=true;}}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;var point=new PointF(e.X/Zoom,e.Y/Zoom);HideTip();foreach(var action in profileActions.ToArray())if(action.Key.Contains(point)){action.Value();return;}if(selected!=0)return;foreach(var hit in hits)if(point.Y>=110&&hit.Key.Contains(point)){OpenProfile(hit.Value);return;}}
  protected override void OnMouseWheel(MouseEventArgs e){HideTip();offset=Math.Max(0,Math.Min(extent,offset-e.Delta/120*80));Invalidate();base.OnMouseWheel(e);}
  void HideTip(){tip="";tooltip.Active=false;tooltip.Hide(this);tooltip.RemoveAll();}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var point=new PointF(e.X/Zoom,e.Y/Zoom);string next="";foreach(var item in tips.Where(t=>selected!=0||point.Y>=110))if(item.Key.Contains(point)){next=item.Value;break;}if(tip!=next){tooltip.Hide(this);tip=next;tooltip.Active=Visible&&next.Length>0;tooltip.SetToolTip(this,next);}Cursor=profileActions.Any(a=>a.Key.Contains(point))||hits.Any(h=>h.Key.Contains(point))?Cursors.Hand:Cursors.Default;}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);HideTip();}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible){HideTip();if(details!=null)details.Stop();detailSignature="";}}
  protected override void Dispose(bool disposing){if(disposing){ranks.Dispose();portraits.Dispose();tooltip.Dispose();profileImages.Dispose();profileArt.Dispose();if(details!=null)details.Dispose();}base.Dispose(disposing);}
 }
}
