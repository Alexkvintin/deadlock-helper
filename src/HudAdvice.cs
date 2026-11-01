using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
namespace DeadlockOverlay {
 public sealed class HudAdvice : IDisposable {
  readonly object gate=new object();readonly Dictionary<int,ItemSuggestion[]> results=new Dictionary<int,ItemSuggestion[]>();
  readonly Dictionary<long,DateTime> iconRetry=new Dictionary<long,DateTime>();
  HelperSource source;string match="",key="";bool loading,iconsLoading,disposed;int revision,sourceRevision;DateTime retry=DateTime.MinValue;
  public string Context=L.T("Определяю вашего героя…");
  public static readonly string IconFolder=Path.Combine(SettingsManager.Folder,"item-icons");
  public int Revision{get{lock(gate)return revision;}}
  public ItemSuggestion[] Get(int hero){lock(gate){ItemSuggestion[] items;return results.TryGetValue(hero,out items)?items:new ItemSuggestion[0];}}
  public void Update(MatchState state,MapSnapshot map,Settings settings){
   if(!settings.ShowItems||!state.Active||state.Demo)return;
   lock(gate){
    if(match!=state.MatchId){match=state.MatchId;retry=DateTime.MinValue;key="";results.Clear();revision++;}
    if(!loading&&DateTime.UtcNow>=retry){loading=true;retry=DateTime.UtcNow.AddHours(1);Task.Run(()=>{
     try{if(source==null){try{var cached=HelperSource.Load(false);lock(gate){source=cached;sourceRevision++;}}catch{}}
      var fresh=HelperSource.Load(true);lock(gate){if(!disposed){source=fresh;sourceRevision++;}}
     }catch(Exception e){Log.Write("Counter Helper refresh",e);lock(gate)retry=DateTime.UtcNow.AddMinutes(1);}finally{lock(gate)loading=false;}
    });}
    var player=!state.Spectator?state.Players.Values.FirstOrDefault(p=>p.Local):null;player=player??state.Players.Values.FirstOrDefault(p=>p.Hero==state.ViewedHeroId);
    string phase=map.Seconds.HasValue&&map.Fresh?HelperSource.Phase(map.Seconds.Value):"";
    string next=L.Language+":"+match+":"+(player==null?0:player.Hero)+":"+phase+":"+sourceRevision+":"+DateTime.UtcNow.Ticks/TimeSpan.TicksPerSecond/30+":"+string.Join(",",state.Players.Values.Select(p=>p.Hero));
    if(next==key)return;key=next;results.Clear();revision++;
    Context=player==null?L.T("Определяю вашего героя…"):phase==""?L.T("Ожидание времени матча"):source==null?L.T("Загрузка Counter Helper…"):player.HeroName+" · "+(phase=="early"?L.T("Линия"):phase=="mid"?L.T("Середина"):L.T("Поздняя игра"));
    if(player==null||phase==""||source==null)return;
    var pictures=new List<HelperPick>();
    foreach(var enemy in state.Players.Values.Where(p=>p.Team!=player.Team)){
     var picks=source.Rank(enemy.HeroName,player.HeroName,phase);pictures.AddRange(picks);
     results[enemy.Hero]=picks.Select(p=>new ItemSuggestion{Id=p.Id,Name=p.Name}).ToArray();
    }
    var missing=pictures.GroupBy(x=>x.Id).Select(x=>x.First()).Where(x=>!File.Exists(Path.Combine(IconFolder,x.Id+".png"))&&(!iconRetry.ContainsKey(x.Id)||DateTime.UtcNow>=iconRetry[x.Id])).ToArray();
    if(!iconsLoading&&missing.Length>0){iconsLoading=true;Task.Run(()=>{try{Directory.CreateDirectory(IconFolder);Parallel.ForEach(missing,new ParallelOptions{MaxDegreeOfParallelism=4},item=>{
     lock(gate)if(disposed||key!=next)return;string path=Path.Combine(IconFolder,item.Id+".png");if(File.Exists(path))return;
     Uri uri;if(!Uri.TryCreate(item.Image,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="assets-bucket.deadlock-api.com")return;
     try{var req=(HttpWebRequest)WebRequest.Create(uri);req.Timeout=10000;req.ReadWriteTimeout=10000;using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var image=Image.FromStream(stream))using(var bitmap=new Bitmap(image,48,48))bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);lock(gate)revision++;}catch(Exception e){Log.Write("Counter Helper icon",e);lock(gate)iconRetry[item.Id]=DateTime.UtcNow.AddSeconds(30);}
    });}finally{lock(gate){iconsLoading=false;key="";}}});}
   }
  }
  public void Dispose(){lock(gate)disposed=true;}
 }
}


