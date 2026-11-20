using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
namespace DeadlockOverlay {
 public sealed class HudAdvice : IDisposable {
  readonly object gate=new object();readonly Dictionary<string,ItemSuggestion[]> results=new Dictionary<string,ItemSuggestion[]>();
  readonly Dictionary<CounterMode,ItemSuggestion[]> categories=new Dictionary<CounterMode,ItemSuggestion[]>();
  readonly HashSet<long> owned=new HashSet<long>();readonly HashSet<string> manual=new HashSet<string>();readonly Dictionary<long,string> markedNames=new Dictionary<long,string>();string manualScope="";CounterContext current=new CounterContext();
  readonly Dictionary<long,DateTime> iconRetry=new Dictionary<long,DateTime>();
  HelperPick[] pendingPictures=new HelperPick[0];DateTime nextIconCheck;
  HelperSource source;string match="",key="";bool loading,iconsLoading,disposed;int revision,sourceRevision;DateTime retry=DateTime.MinValue;
  readonly bool offlineSource;
  public HudAdvice(HelperSource fixture=null){source=fixture;offlineSource=fixture!=null;}
  public string Context=L.T("Определяю вашего героя…");
  public static readonly string IconFolder=Path.Combine(SettingsManager.Folder,"item-icons");
  public int Revision{get{lock(gate)return revision;}}
  public CounterContext CurrentContext{get{lock(gate)return current;}}
  public ItemSuggestion[] Get(Player player){lock(gate){string identity=CounterContext.Key(player);ItemSuggestion[] items;return current.Enemies.Any(p=>CounterContext.Key(p)==identity&&p.Hero==player.Hero&&p.Team==player.Team)&&results.TryGetValue(identity,out items)?items:new ItemSuggestion[0];}}
  public ItemSuggestion[] Get(int hero){lock(gate){var players=current.Enemies.Where(p=>p.Hero==hero).ToArray();return players.Length==1?Get(players[0]):new ItemSuggestion[0];}}
  public ItemSuggestion[] GetCategory(CounterMode mode){lock(gate){ItemSuggestion[] items;return categories.TryGetValue(mode,out items)?items:new ItemSuggestion[0];}}
  public void SelectLane(string enemy){lock(gate){manual.Clear();if(!string.IsNullOrEmpty(enemy))manual.Add(enemy);manualScope=current.Identity;key="";revision++;}}
  public void ToggleLane(string enemy){lock(gate){if(current.Focus==null||!current.Enemies.Any(p=>CounterContext.Key(p)==enemy))return;if(!manual.Add(enemy))manual.Remove(enemy);manualScope=current.Identity;key="";revision++;}}
  public string ManualLane{get{lock(gate)return manual.OrderBy(x=>x).FirstOrDefault()??"";}}
  public string[] ManualLanes{get{lock(gate)return manual.OrderBy(x=>x).ToArray();}}
  public string OwnedName(long id){lock(gate){string name;return markedNames.TryGetValue(id,out name)?name:id.ToString();}}
  public void SetOwned(long id,bool value){if(id<=0)return;lock(gate){if(value){var item=categories.Values.SelectMany(x=>x).FirstOrDefault(x=>x.Id==id);if(item!=null)markedNames[id]=item.Name;owned.Add(id);}else{owned.Remove(id);markedNames.Remove(id);}key="";revision++;}}
  public void ClearOwned(){lock(gate){owned.Clear();markedNames.Clear();key="";revision++;}}
  public long[] MarkedOwned{get{lock(gate)return owned.OrderBy(id=>id).ToArray();}}
  static ItemSuggestion Suggest(HelperPick pick,Player enemy,long? souls){return new ItemSuggestion{Id=pick.Id,Name=pick.Name,Cost=pick.Cost,Why=pick.Why,Condition=pick.Condition,Priority=pick.Score,Target=string.IsNullOrEmpty(enemy.HeroName)?LocalSnapshot.HeroName(enemy.Hero):enemy.HeroName,Image=pick.Image,Affordable=souls.HasValue&&pick.Cost.HasValue?(bool?)(pick.Cost<=souls):null};}
  static ItemSuggestion[] Merge(IEnumerable<ItemSuggestion> picks){return picks.GroupBy(p=>p.Id).Select(g=>{var p=g.OrderByDescending(x=>x.Priority).First();return new ItemSuggestion{Id=p.Id,Name=p.Name,Cost=p.Cost,Priority=g.Max(x=>x.Priority),Why=string.Join(" | ",g.GroupBy(x=>x.Why).Select(x=>string.Join(", ",x.Select(y=>y.Target).Distinct())+": "+x.Key)),Image=p.Image,Affordable=p.Affordable,Target=string.Join(", ",g.Select(x=>x.Target).Distinct())};}).OrderByDescending(p=>p.Affordable==true).ThenByDescending(p=>p.Priority).ThenBy(p=>p.Name).ToArray();}
  public void Update(MatchState state,MapSnapshot map,Settings settings){
   lock(gate){
    if(match!=state.MatchId){match=state.MatchId;key="";manual.Clear();manualScope="";owned.Clear();markedNames.Clear();results.Clear();categories.Clear();current=new CounterContext();revision++;}
    if(!settings.ShowItems||(!state.Active&&!LocalSnapshot.Pregame(state.Phase))||state.Demo){Context=settings.ShowItems?L.Choose("Waiting for match","Ожидание матча"):L.Choose("Recommendations disabled","Подсказки отключены");if(results.Count>0||categories.Count>0||current.Focus!=null){results.Clear();categories.Clear();key="";current=new CounterContext();revision++;}return;}
    if(!offlineSource&&!UiReview.Active&&!loading&&DateTime.UtcNow>=retry){loading=true;retry=DateTime.UtcNow.AddHours(1);Task.Run(()=>{
     try{if(source==null){try{var cached=HelperSource.Load(false);lock(gate){source=cached;sourceRevision++;}}catch{}}
      var fresh=HelperSource.Load(true);lock(gate){if(!disposed){source=fresh;sourceRevision++;}}
     }catch(Exception e){Log.Write("Counter Helper refresh",e);lock(gate)retry=DateTime.UtcNow.AddMinutes(1);}finally{lock(gate)loading=false;}
    });}
    var context=CounterContext.CreateSelected(state,map,manual);if(context.Identity.Length>0&&context.Identity!=manualScope){manual.Clear();owned.Clear();markedNames.Clear();manualScope=context.Identity;context=CounterContext.CreateSelected(state,map,manual);}
    if(context.Focus!=null&&LocalSnapshot.Pregame(state.Phase)&&context.Phase.Length==0)context.Phase="early";
    current=context;var player=context.Focus;string phase=context.Phase;var mode=(CounterMode)settings.CounterMode;
    string next=L.Language+":"+sourceRevision+":"+context.Signature(source==null?"":source.AffordabilityKey(context.Souls),string.Join(",",owned.OrderBy(id=>id)),mode);
    if(next==key){EnsureIcons(next);return;}key=next;results.Clear();categories.Clear();pendingPictures=new HelperPick[0];revision++;
    if(LiveDiagnostics.Enabled)LiveDiagnostics.Event("counter","recompute match="+LiveDiagnostics.Match(state.MatchId)+" phase="+phase+" mode="+mode+" enemies="+context.Enemies.Length+" lane="+context.LaneEnemies.Length);
    Context=player==null?L.T("Определяю вашего героя…"):phase==""?L.T("Ожидание времени матча"):source==null?L.T("Загрузка Counter Helper…"):player.HeroName+" · "+(phase=="early"?L.T("Линия"):phase=="mid"?L.T("Середина"):L.T("Поздняя игра"));
    if(player==null||phase==""||source==null)return;
    var pictures=new List<HelperPick>();var team=new List<ItemSuggestion>();var lane=new List<ItemSuggestion>();var situational=new List<ItemSuggestion>();
    bool laneMode=(mode==CounterMode.Lane||mode==CounterMode.Auto&&context.LanePhase)&&context.LaneEnemies.Length>0;
    foreach(var enemy in context.Enemies){string enemyName=string.IsNullOrEmpty(enemy.HeroName)?LocalSnapshot.HeroName(enemy.Hero):enemy.HeroName;string focusName=string.IsNullOrEmpty(player.HeroName)?LocalSnapshot.HeroName(player.Hero):player.HeroName;
     var picks=source.Rank(enemyName,focusName,phase,owned,context.Souls);var situation=source.Situational(enemyName,focusName,phase,owned,context.Souls);pictures.AddRange(picks);pictures.AddRange(situation);
     var normal=picks.Select(p=>Suggest(p,enemy,context.Souls)).ToArray();var specific=situation.Select(p=>Suggest(p,enemy,context.Souls)).ToArray();team.AddRange(normal);situational.AddRange(specific);
     bool onLane=context.LaneEnemies.Any(p=>CounterContext.Key(p)==CounterContext.Key(enemy));if(onLane)lane.AddRange(normal);
     if(!laneMode||onLane)results[CounterContext.Key(enemy)]=mode==CounterMode.Situational?specific:normal;
    }
    categories[CounterMode.Team]=Merge(team);categories[CounterMode.Lane]=Merge(lane);categories[CounterMode.Situational]=situational.ToArray();
    pendingPictures=pictures.GroupBy(x=>x.Id).Select(x=>x.First()).ToArray();nextIconCheck=DateTime.MinValue;EnsureIcons(next);
   }
  }
  void EnsureIcons(string next){
    if(offlineSource||UiReview.Active||iconsLoading||DateTime.UtcNow<nextIconCheck)return;nextIconCheck=DateTime.UtcNow.AddSeconds(5);
    var missing=pendingPictures.Where(x=>!File.Exists(Path.Combine(IconFolder,x.Id+".png"))&&(!iconRetry.ContainsKey(x.Id)||DateTime.UtcNow>=iconRetry[x.Id])).Take(32).ToArray();
    if(missing.Length>0){iconsLoading=true;Task.Run(()=>{try{Directory.CreateDirectory(IconFolder);Parallel.ForEach(missing,new ParallelOptions{MaxDegreeOfParallelism=2},item=>{
     lock(gate)if(disposed||key!=next)return;string path=Path.Combine(IconFolder,item.Id+".png");if(File.Exists(path))return;
     Uri uri;if(!Uri.TryCreate(item.Image,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="assets-bucket.deadlock-api.com")return;
     try{var req=(HttpWebRequest)WebRequest.Create(uri);req.Timeout=10000;req.ReadWriteTimeout=10000;using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var image=Image.FromStream(stream))using(var bitmap=new Bitmap(image,48,48))bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}catch(Exception e){Log.Write("Counter Helper icon",e);lock(gate)iconRetry[item.Id]=DateTime.UtcNow.AddSeconds(30);}
    });}finally{lock(gate){iconsLoading=false;revision++;}}});}
  }
  public void Dispose(){lock(gate)disposed=true;}
 }
}


