using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed partial class ScoutView {
  readonly ProfileImages profileImages=new ProfileImages();readonly ArtCache profileArt=new ArtCache();
  readonly List<KeyValuePair<RectangleF,Action>> profileActions=new List<KeyValuePair<RectangleF,Action>>();
  ScoutDetailService details;string detailSignature="";int teamOffset,profileTab,chartMetric,historyFilter;bool currentHeroOnly;long matchSelected;int matchesOffset;float contentTop=58;
  public void OpenProfile(ScoutRow row){HideTip();teamOffset=offset;selected=row.Account;offset=0;profileTab=0;historyFilter=0;currentHeroOnly=false;matchSelected=0;detailSignature="";RefreshProfile();Invalidate();}
  public void RefreshProfile(){
   if(selected==0||!Visible||Options==null||!Options.Enabled){if(details!=null)details.Stop();detailSignature="";return;}
   var row=Rows.FirstOrDefault(r=>r.Account==selected);if(row==null){selected=0;detailSignature="";if(details!=null)details.Stop();return;}
   var p=Service.Get(row.Account);var h=ScoutStatistics.History(p).Take(20).ToArray();string signature=row.Account+":"+ScoutStatistics.MainHero(p)+":"+Options.Network+":"+string.Join(",",h.Select(m=>m.Id));if(signature==detailSignature)return;detailSignature=signature;
   if(details==null){details=new ScoutDetailService(Path.Combine(SettingsManager.Folder,"scout-cache","profiles"));details.Changed+=()=>{try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)Invalidate();}));}catch(InvalidOperationException){}};}
   details.Open(row.Account,ScoutStatistics.MainHero(p),h,Options.Network);
  }
  ScoutMatchDetail Detail(long match){return details==null?null:details.Get(selected,match);}
  void BackToTeams(){HideTip();selected=0;offset=teamOffset;if(details!=null)details.Stop();detailSignature="";Invalidate();}
  void ActionBox(RectangleF box,Action action,bool content=true){if(content){box.Y+=contentTop-offset;var visible=RectangleF.Intersect(box,new RectangleF(0,contentTop,W,Math.Max(1,H-contentTop)));if(visible.Height<=0)return;box=visible;}profileActions.Add(new KeyValuePair<RectangleF,Action>(box,action));}
  void TipBox(RectangleF box,string text){box.Y+=contentTop-offset;box=RectangleF.Intersect(box,new RectangleF(0,contentTop,W,Math.Max(1,H-contentTop)));if(box.Height>0)tips.Add(new KeyValuePair<RectangleF,string>(box,text));}
  void Button(Graphics g,string label,RectangleF box,Action action,bool active=false){Theme.Fill(g,active?Color.FromArgb(28,66,60):Theme.Card,box,7);TextAt(g,label,14,active?Theme.Green:Theme.Ink,new RectangleF(box.X+12,box.Y+10,box.Width-24,26));ActionBox(box,()=>{HideTip();action();Invalidate();});}
  string TacticalTitle(ScoutRow row){return !UserTeam.HasValue?T("TACTICAL NOTES","ТАКТИЧЕСКИЕ ЗАМЕТКИ"):row.Team==UserTeam.Value?T("WHAT TO EXPECT FROM YOUR ALLY","ЧЕГО ОЖИДАТЬ ОТ СОЮЗНИКА"):T("HOW TO PLAY AGAINST THEM","КАК ИГРАТЬ ПРОТИВ НЕГО");}
  void DrawDetail(Graphics g,ScoutRow row,int unused){
   var data=Data(row);var p=data.Profile;var h=data.History.Take(20).ToArray();TextAt(g,T("← Teams","← Команды"),15,Theme.Green,new RectangleF(16,14,160,30));ActionBox(new RectangleF(12,8,170,40),BackToTeams,false);
   TextAt(g,T("PLAYER PROFILE","ПРОФИЛЬ ИГРОКА"),13,Theme.Gold,new RectangleF(190,17,W>=620?W-440:W-210,25));if(W>=620){int ready=h.Count(m=>Detail(m.Id)!=null);string status=details!=null&&details.Status=="cooldown"?T("Source busy · reopen later","Источник занят · откройте позже"):ready<h.Length&&Options.Network?T("Loading details ","Загрузка данных ")+ready+"/"+h.Length:T("Match details ","Данные матчей ")+h.Count(m=>Detail(m.Id)!=null&&Detail(m.Id).Available)+"/"+h.Length;TextAt(g,status,12,Theme.Muted,new RectangleF(W-232,17,220,25));}
   float width=W-32;bool wide=width>=760;Image art=details==null?null:profileImages.Get(details.Art(data.Main));if(art==null)art=profileArt.Get("hero-"+data.Main);bool hasArt=art!=null&&art.Width>=256&&art.Height>=256;int headerHeight=246;int y=headerHeight+16;
   var saved=g.Save();g.SetClip(new RectangleF(0,contentTop,W,Math.Max(1,H-contentTop)));g.TranslateTransform(0,contentTop-offset);
   var banner=new RectangleF(16,0,width,headerHeight);Theme.Fill(g,Color.FromArgb(21,35,40),banner,12);
   float artWidth=wide?Math.Min(300,width*.31f):Math.Min(110,width*.25f);if(hasArt){var clip=g.Save();using(var path=Theme.Round(banner,12))g.SetClip(path,System.Drawing.Drawing2D.CombineMode.Intersect);var artBox=new Rectangle((int)(banner.Right-artWidth),0,(int)Math.Ceiling(artWidth),headerHeight);Design.Cover(g,art,artBox);using(var shade=new System.Drawing.Drawing2D.LinearGradientBrush(artBox,Color.FromArgb(21,35,40),Color.FromArgb(35,21,35,40),0f)){shade.WrapMode=System.Drawing.Drawing2D.WrapMode.TileFlipX;g.FillRectangle(shade,artBox);}g.Restore(clip);}
   float identityWidth=width-artWidth-40;ProfileVisuals.Avatar(g,details==null?null:profileImages.Get(details.Avatar(row.Account)),new RectangleF(34,22,78,78),row.Name);
   TextAt(g,row.Name,25,Theme.Ink,new RectangleF(128,22,Math.Max(90,identityWidth-112),38));
   TextAt(g,T("Steam profile ↗","Профиль Steam ↗"),13,Theme.Green,new RectangleF(128,64,180,24));ActionBox(new RectangleF(128,60,180,32),()=>System.Diagnostics.Process.Start("https://steamcommunity.com/profiles/"+(row.Account+76561197960265728L)));
   var badge=ranks.Get(Service.RankImage(p.Badge));if(badge!=null){g.DrawImage(badge,34,116,42,42);TipBox(new RectangleF(34,116,42,42),Rank(p.Badge));}TextAt(g,Rank(p.Badge),17,Theme.Gold,new RectangleF(86,117,identityWidth-70,26));
   TextAt(g,T("Currently: ","Сейчас: ")+LocalSnapshot.HeroName(row.Hero),14,Theme.Muted,new RectangleF(34,164,identityWidth,24));
   TextAt(g,T("Most played: ","Основной: ")+(data.Main>0?LocalSnapshot.HeroName(data.Main):ScoutProfileData.Missing)+" · "+h.Count(m=>m.Hero==data.Main)+"/"+h.Length,14,Theme.Ink,new RectangleF(34,191,identityWidth,24));
   int tx=wide?(int)(width-artWidth+20):34,ty=wide?199:218;foreach(var tag in data.Tags){var box=new RectangleF(tx,ty,23,23);ScoutGlyphs.Draw(g,tag.Key,box,Theme.Green);TipBox(box,ScoutProfileData.Tip(tag.Key));tx+=31;}
   PartyInfo party;if(Parties.TryGetValue(row.Account,out party)&&ScoutDecorations.VisibleParty(party,Options)){var box=new RectangleF(tx,ty,24,24);ScoutGlyphs.Draw(g,"party",box,party.Color);TipBox(box,ScoutProfileData.Tip(party.Confidence==PartyConfidence.Likely?"likely":"party"));}
   string[] tabs={T("Overview","Обзор"),T("Matches","Матчи"),T("Play Style","Стиль игры")};for(int i=0;i<3;i++){int index=i;float tw=(width-16)/3;Button(g,tabs[i],new RectangleF(16+i*(tw+8),y,tw,42),()=>{profileTab=index;matchSelected=0;offset=0;},profileTab==i);}y+=60;
   if(profileTab==0){
    float half=(width-8)/2;
    Button(g,T("Overall","Все герои"),new RectangleF(16,y,half,40),()=>{currentHeroOnly=false;offset=0;},!currentHeroOnly);
    Button(g,T("Current hero","Текущий герой"),new RectangleF(24+half,y,half,40),()=>{currentHeroOnly=true;offset=0;},currentHeroOnly);y+=52;
    y=DrawOverview(g,row,data,currentHeroOnly?h.Where(m=>m.Hero==row.Hero).ToArray():h,y,width);
   }
   else if(profileTab==1){
    string[] filters={T("All","Все"),T("Ranked","Рейтинг"),T("Unranked","Без рейтинга"),T("Current hero","Текущий герой")};
    int cols=width>=620?4:2;float fw=(width-(cols-1)*8)/cols;
    for(int j=0;j<4;j++){int f=j;Button(g,filters[j],new RectangleF(16+j%cols*(fw+8),y+j/cols*48,fw,40),()=>{historyFilter=f;matchSelected=0;offset=0;},historyFilter==j);}y+=((4+cols-1)/cols)*48+4;
    y=DrawMatches(g,h.Where(m=>historyFilter==0||historyFilter==1&&m.Mode==4||historyFilter==2&&m.Mode==1||historyFilter==3&&m.Hero==row.Hero).ToArray(),y,width);
   }
   else {y=Paragraph(g,T("CURRENT HERO","ТЕКУЩИЙ ГЕРОЙ"),ScoutProfileData.PlayStyle(h.Where(m=>m.Hero==row.Hero).ToArray(),Detail),y,width);y=Paragraph(g,TacticalTitle(row),ScoutProfileData.Tactics(h,row.Hero,Detail,UserTeam.HasValue?(bool?)(row.Team==UserTeam.Value):null),y,width);y=DrawSources(g,p,data,y,width);}
   g.Restore(saved);ScrollRange(y+20,H-(int)contentTop);
  }
  int DrawOverview(Graphics g,ScoutRow row,RowData data,ScoutMatch[] h,int y,float width){
   int columns=width>=850?4:width>=550?3:2;float cw=(width-(columns-1)*12)/columns;int i=0;
   Action<string,string,string,Color> card=(label,value,source,color)=>{ProfileVisuals.Card(g,new RectangleF(16+i%columns*(cw+12),y+i/columns*120,cw,108),label,value,source,color);i++;};
   var scored=h.Where(m=>m.Won.HasValue).ToArray();var ranked=currentHeroOnly?h.Where(m=>m.Mode==4).ToArray():data.History.Where(m=>m.Mode==4).Take(20).ToArray();string sample=h.Length+T(" recent matches"," последних матчей");
   card(T("Win rate","Победы"),currentHeroOnly&&scored.Length<5?ScoutProfileData.Missing:ScoutProfileData.Rate(h),scored.Length+T(" scored matches"," игр с результатом"),Theme.Green);
   card(T("Ranked win rate","Победы в рейтинге"),currentHeroOnly&&ranked.Count(m=>m.Won.HasValue)<5?ScoutProfileData.Missing:ScoutProfileData.Rate(ranked),ranked.Count(m=>m.Won.HasValue)+T(" recent ranked"," последних рейтинговых"),Theme.Green);
   var kda=h.Where(m=>m.Kills.HasValue&&m.Deaths.HasValue&&m.Assists.HasValue).ToArray();card(T("Average K / D / A","Средние K / D / A"),kda.Length==0?ScoutProfileData.Missing:ScoutProfileData.N(kda.Average(m=>(double)m.Kills.Value))+" / "+ScoutProfileData.N(kda.Average(m=>(double)m.Deaths.Value))+" / "+ScoutProfileData.N(kda.Average(m=>(double)m.Assists.Value)),kda.Length+T(" complete records"," полных записей"),Theme.Ink);
   card(T("KDA ratio","Коэффициент KDA"),kda.Length==0?ScoutProfileData.Missing:ScoutProfileData.N((double)kda.Sum(m=>m.Kills.Value+m.Assists.Value)/Math.Max(1,kda.Sum(m=>m.Deaths.Value))),T("Total (K+A) / max(1,D)","Сумма (K+A) / max(1,D)"),Theme.Ink);
   card(T("Average souls","Средние души"),ScoutProfileData.N(ScoutProfileData.Mean(h.Select(m=>(double?)m.Souls))),h.Count(m=>m.Souls.HasValue)+T(" matches · history"," матчей · история"),Theme.SoulsBlue);
   for(int metric=4;metric<=6;metric++){int m=metric;var values=h.Select(match=>ScoutProfileData.Metric(match,Detail(match.Id),m)).ToArray();card(MetricNames()[m],ScoutProfileData.N(ScoutProfileData.Mean(values)),values.Count(v=>v.HasValue)+"/"+h.Length+T(" · average, metadata"," · среднее, метаданные"),m==4?Theme.HeroDamage:m==5?Theme.StructureDamage:Theme.Healing);}
   var allRecent=data.History.Take(20).ToArray();
   card(T("Current hero experience","Опыт текущего героя"),allRecent.Count(m=>m.Hero==row.Hero)+"/"+allRecent.Length,LocalSnapshot.HeroName(row.Hero),Theme.Gold);
   card(T("Overall win rate","Победы на всех героях"),ScoutProfileData.Rate(allRecent),allRecent.Count(m=>m.Won.HasValue)+T(" scored matches"," игр с результатом"),Theme.Muted);
   card(T("Available history","Доступная история"),ScoutProfileData.N(data.History.Length),T("Sample, not career total","Выборка, не вся карьера"),Theme.Ink);
   card(T("Performance","Эффективность"),ScoutProfileData.Missing,T("Calibration required","Нужна калибровка"),Theme.Muted);
   y+=((i+columns-1)/columns)*120+8;y=Paragraph(g,T("PLAY STYLE","СТИЛЬ ИГРЫ"),ScoutProfileData.PlayStyle(h,Detail),y,width);
   y=Paragraph(g,TacticalTitle(row),ScoutProfileData.Tactics(data.History.Take(20).ToArray(),row.Hero,Detail,UserTeam.HasValue?(bool?)(row.Team==UserTeam.Value):null),y,width);y=DrawSources(g,data.Profile,data,y,width);return y;
  }
  string[] MetricNames(){return new[]{"KDA",T("Souls","Души"),T("Results","Результаты"),T("Rank","Ранг"),T("Hero damage","Урон героям"),T("Objectives","Строения"),T("Healing","Лечение")};}
  int DrawMatches(Graphics g,ScoutMatch[] h,int y,float width){
   var chosen=h.FirstOrDefault(m=>m.Id==matchSelected);if(chosen!=null){Button(g,T("← All matches","← Все матчи"),new RectangleF(16,y,200,42),()=>{matchSelected=0;offset=matchesOffset;});y+=55;var d=Detail(chosen.Id);string result=chosen.Won==true?T("Victory","Победа"):chosen.Won==false?T("Defeat","Поражение"):T("Unscored","Без результата");y=Paragraph(g,LocalSnapshot.HeroName(chosen.Hero)+" · "+result,ScoutProfileData.Date(chosen.Start)+" · "+ScoutProfileData.Duration(chosen.Duration)+" · #"+chosen.Id,y,width);int cols=width>=650?3:2;float cw=(width-(cols-1)*12)/cols;string[] labels={"K / D / A",T("Souls","Души"),T("Hero damage","Урон героям"),T("Objectives","Строения"),T("Healing","Лечение"),T("Ally healing","Лечение союзников")};string[] values={ScoutStatistics.Value(chosen.Kills)+" / "+ScoutStatistics.Value(chosen.Deaths)+" / "+ScoutStatistics.Value(chosen.Assists),ScoutProfileData.N(chosen.Souls),ScoutProfileData.N(d!=null&&d.Available?d.Damage:null),ScoutProfileData.N(d!=null&&d.Available?d.Objectives:null),ScoutProfileData.N(d!=null&&d.Available?d.Healing:null),ScoutProfileData.N(d!=null&&d.Available?d.TeamHealing:null)};for(int i=0;i<6;i++)ProfileVisuals.Card(g,new RectangleF(16+i%cols*(cw+12),y+i/cols*120,cw,108),labels[i],values[i],i<2?T("Public match history","Публичная история"):T("Recorded match metadata","Метаданные матча"),i==2?Theme.HeroDamage:i==3?Theme.StructureDamage:i>=4?Theme.Healing:Theme.Ink);y+=((6+cols-1)/cols)*120;return Paragraph(g,T("DATA COVERAGE","ПОЛНОТА ДАННЫХ"),d!=null&&d.Available?T("Last recorded sample: ","Последний снимок: ")+ScoutProfileData.Duration(d.LastSecond)+T(". Healing is the source total and may include self-healing.",". Лечение — общий счётчик источника, может включать самолечение."):T("Detailed statistics are loading or unavailable. Missing values are not zero.","Подробные показатели загружаются или недоступны. Пропуски не равны нулю."),y,width);}
   string[] names=MetricNames();int btnCols=Math.Max(2,Math.Min(7,(int)(width/125))),btnRows=(7+btnCols-1)/btnCols;float bw=(width-(btnCols-1)*8)/btnCols;
   for(int i=0;i<7;i++){int index=i;Button(g,names[i],new RectangleF(16+i%btnCols*(bw+8),y+i/btnCols*48,bw,40),()=>chartMetric=index,chartMetric==i);}y+=btnRows*48+8;
   DrawChart(g,h.Reverse().ToArray(),new RectangleF(16,y,width,220));y+=235;
   TextAt(g,T("LATEST MATCHES · select a match","ПОСЛЕДНИЕ МАТЧИ · нажмите на матч"),13,Theme.Gold,new RectangleF(16,y,width,25));y+=34;
   bool wide=width>=720;foreach(var m in h){int height=wide?88:126;var box=new RectangleF(16,y,width,height);Theme.Fill(g,Theme.Card,box,10);var image=portraits.Get(m.Hero);if(image!=null)g.DrawImage(image,30,y+17,48,48);Color color=m.Won==true?Theme.Green:m.Won==false?Color.FromArgb(235,122,119):Theme.Muted;using(var pen=new Pen(color,3))g.DrawLine(pen,18,y+14,18,y+height-14);
    TextAt(g,LocalSnapshot.HeroName(m.Hero),16,Theme.Ink,new RectangleF(92,y+12,wide?190:width-100,24));TextAt(g,ScoutProfileData.Date(m.Start),12,Theme.Muted,new RectangleF(92,y+40,200,22));
    TextAt(g,m.Won==true?T("VICTORY","ПОБЕДА"):m.Won==false?T("DEFEAT","ПОРАЖЕНИЕ"):T("UNSCORED","БЕЗ РЕЗУЛЬТАТА"),12,color,new RectangleF(wide?300:30,wide?y+14:y+78,160,20));
    string mode=m.Mode==4?T("Ranked","Рейтинг"):T("Unranked","Без рейтинга");TextAt(g,mode+" · "+ScoutProfileData.Duration(m.Duration),12,Theme.Muted,new RectangleF(wide?300:30,wide?y+40:y+101,210,21));
    float sx=wide?Math.Max(510,width-220):Math.Max(210,width-220);float sy=wide?y+14:y+78;TextAt(g,ScoutStatistics.Value(m.Kills)+" / "+ScoutStatistics.Value(m.Deaths)+" / "+ScoutStatistics.Value(m.Assists)+"   KDA",14,Theme.Ink,new RectangleF(sx,sy,Math.Max(40,width-sx),22));TextAt(g,ScoutProfileData.N(m.Souls)+T(" souls"," душ")+" · Δ "+ScoutStatistics.Value(m.Delta),12,Theme.SoulsBlue,new RectangleF(sx,sy+26,Math.Max(40,width-sx),22));
    long id=m.Id;ActionBox(box,()=>{matchesOffset=offset;matchSelected=id;offset=0;Invalidate();});y+=height+10;
   }if(h.Length==0)y=Paragraph(g,T("No match history","Нет истории матчей"),T("Waiting for available public records.","Ожидание доступных публичных записей."),y,width);return y;
  }
  void DrawChart(Graphics g,ScoutMatch[] h,RectangleF box){Theme.Fill(g,Theme.Card,box,10);var values=h.Select(m=>ScoutProfileData.Metric(m,Detail(m.Id),chartMetric)).ToArray();TextAt(g,MetricNames()[chartMetric]+" · "+values.Count(v=>v.HasValue)+"/"+h.Length+T(" records · oldest → newest"," записей · старые → новые"),12,Theme.Muted,new RectangleF(box.X+16,box.Y+12,box.Width-32,24));var known=values.Where(v=>v.HasValue).Select(v=>v.Value).ToArray();if(known.Length==0){TextAt(g,ScoutProfileData.Missing,20,Theme.Muted,new RectangleF(box.X+24,box.Y+85,box.Width-48,40));return;}
   double min=chartMetric==3?known.Min():0,max=known.Max();if(max<=min)max=min+1;float left=box.X+65,right=box.Right-28,top=box.Y+50,bottom=box.Bottom-36;
   for(int i=0;i<4;i++){float yy=bottom-(bottom-top)*i/3;using(var pen=new Pen(Color.FromArgb(45,62,66)))g.DrawLine(pen,left,yy,right,yy);double v=min+(max-min)*i/3;TextAt(g,chartMetric==3?Rank(ScoutProfileData.StepBadge(v)).Substring(0,3)+" "+ScoutProfileData.StepBadge(v)%10:ScoutProfileData.N(v),10,Theme.Muted,new RectangleF(box.X+10,yy-7,52,28));}
   PointF? previous=null;for(int i=0;i<h.Length;i++){if(!values[i].HasValue){previous=null;continue;}float x=h.Length==1?(left+right)/2:left+i*(right-left)/(h.Length-1),y=bottom-(float)((values[i].Value-min)/(max-min))*(bottom-top);var point=new PointF(x,y);Color c=h[i].Won==true?Theme.Green:h[i].Won==false?Color.FromArgb(235,122,119):Theme.Muted;if(previous.HasValue)using(var pen=new Pen(Color.FromArgb(118,165,168),1.5f))g.DrawLine(pen,previous.Value,point);using(var brush=new SolidBrush(c))g.FillEllipse(brush,x-4,y-4,8,8);previous=point;long chartMatch=h[i].Id;ActionBox(new RectangleF(x-9,y-9,18,18),()=>{matchesOffset=offset;matchSelected=chartMatch;offset=0;HideTip();Invalidate();});TipBox(new RectangleF(x-9,y-9,18,18),ScoutProfileData.Date(h[i].Start)+" · "+LocalSnapshot.HeroName(h[i].Hero)+"\n"+MetricNames()[chartMetric]+": "+(chartMetric==3?Rank(h[i].Badge.Value):ScoutProfileData.N(values[i]))+" · "+(h[i].Won==true?T("Victory","Победа"):h[i].Won==false?T("Defeat","Поражение"):T("Unscored","Без результата")));}
   TextAt(g,T("Hover a point for match details","Наведите на точку для подробностей"),11,Theme.Muted,new RectangleF(left,box.Bottom-23,right-left,20));
  }
  int Paragraph(Graphics g,string title,string body,int y,float width){int height;using(var f=new Font("Bahnschrift",14,FontStyle.Regular,GraphicsUnit.Pixel))height=(int)Math.Ceiling(g.MeasureString(body,f,(int)Math.Max(80,width-36)).Height)+65;Theme.Fill(g,Theme.Card,new RectangleF(16,y,width,height),10);TextAt(g,title,13,Theme.Gold,new RectangleF(34,y+14,width-36,23));TextAt(g,body,14,Theme.Ink,new RectangleF(34,y+43,width-36,height-48));return y+height+14;}
  int DrawSources(Graphics g,ScoutProfile p,RowData data,int y,float width){string date=p.Fetched==DateTime.MinValue?ScoutProfileData.Missing:p.Fetched.ToLocalTime().ToString("g",ScoutProfileData.Culture);string body=T("History updated: ","История обновлена: ")+date+T(". Public normal-mode ranked and unranked matches. Healing includes the source's total; ally healing is separate. ",". Публичные обычные и рейтинговые матчи. Лечение — общий счётчик источника; лечение союзников отдельно. ");body+=T("Rank source: match #","Источник ранга: матч №")+p.RankMatch+" · "+(p.RankFetched==DateTime.MinValue?ScoutProfileData.Missing:p.RankFetched.ToLocalTime().ToString("g",ScoutProfileData.Culture))+". ";foreach(var tag in data.Tags)body+=tag.Label+": "+tag.Reason+". ";PartyInfo party;if(Parties.TryGetValue(p.Account,out party)&&party.Confidence!=PartyConfidence.Unknown)body+=party.Reason;y=Paragraph(g,T("SAMPLE & SOURCES","ВЫБОРКА И ИСТОЧНИКИ"),body,y,width);
   foreach(var mate in (p.Mates??new HistoricalMate[0]).OrderByDescending(m=>m.Matches).Take(5)){
    var known=Rows.FirstOrDefault(r=>r.Account==mate.Account);var shared=ScoutStatistics.History(p).Where(m=>mate.MatchIds.Contains(m.Id)).ToArray();
    string name=known==null?"Account "+mate.Account:known.Name;
    string evidence=mate.Matches+T(" shared matches in the API friendship sample. Historical party ID: unavailable. Last shared match in cached history: "," общих матчей в выборке дружбы API. Исторический ID пати: недоступен. Последняя общая игра в кеше: ")+(shared.Length==0?ScoutProfileData.Missing:ScoutProfileData.Date(shared.Max(m=>m.Start)));
    y=Paragraph(g,name,evidence,y,width);
   }return y;}
 }
}
