using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace DeadlockOverlay {
 // One bounded worker, cached public data; never polls a player per frame.
 public sealed class ScoutService:IDisposable {
  readonly object gate=new object();readonly Dictionary<long,ScoutProfile> profiles=new Dictionary<long,ScoutProfile>();
  readonly HashSet<long> queued=new HashSet<long>();readonly Queue<long> queue=new Queue<long>();readonly Dictionary<long,DateTime> retry=new Dictionary<long,DateTime>();
  readonly string folder;bool running,disposed,enabled;int generation,executingGeneration;DateTime backoff;HttpWebRequest request;
  public int Revision{get;private set;}
  public int Requests{get;private set;}
  public ScoutService(string cacheFolder){folder=cacheFolder;}
  public string RankImage(int badge){return Path.Combine(folder,"rank-"+badge+".png");}
  void LoadRankImage(int badge){if(badge<10||badge>116||File.Exists(RankImage(badge)))return;lock(gate){if(!enabled||disposed||DateTime.UtcNow<backoff)return;}
   var req=(HttpWebRequest)WebRequest.Create("https://api.deadlock-api.com/v1/assets/ranks/"+(badge/10)+"/"+(badge%10)+"/image");req.UserAgent="DeadlockHelper/1.0 PlayerScout";req.Timeout=8000;req.ReadWriteTimeout=8000;lock(gate){if(!enabled||disposed)return;request=req;Requests++;}
   try{using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var bytes=new MemoryStream()){byte[] buffer=new byte[4096];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){if(bytes.Length+count>2*1024*1024)throw new InvalidDataException("Rank icon too large");bytes.Write(buffer,0,count);}bytes.Position=0;using(var image=System.Drawing.Image.FromStream(bytes))using(var small=new System.Drawing.Bitmap(image,48,48)){lock(gate){if(!enabled||disposed)return;Directory.CreateDirectory(folder);small.Save(RankImage(badge),System.Drawing.Imaging.ImageFormat.Png);Revision++;}}}}
   catch(WebException e){var r=e.Response as HttpWebResponse;if(r!=null)using(r){if((int)r.StatusCode==429||(int)r.StatusCode==503||(int)r.StatusCode==504)lock(gate)backoff=RetryAt(r.Headers["Retry-After"],DateTime.UtcNow);}}
   catch(Exception e){Log.Write("Scout rank icon",e);}finally{lock(gate)if(request==req)request=null;}
  }
  public ScoutProfile Get(long id){lock(gate){ScoutProfile p;return profiles.TryGetValue(id,out p)?p:new ScoutProfile{Account=id};}}
  public void SetEnabled(bool value){lock(gate){if(enabled==value)return;enabled=value;if(!value){generation++;queue.Clear();queued.Clear();if(request!=null)request.Abort();}}}
  public void Enqueue(IEnumerable<long> ids){lock(gate){if(!enabled||disposed)return;foreach(long id in ids.Where(x=>x>0).Distinct()){ScoutProfile p;DateTime next;if(retry.TryGetValue(id,out next)&&DateTime.UtcNow<next)continue;if(profiles.TryGetValue(id,out p)&&p.Loaded&&(DateTime.UtcNow-p.Fetched).TotalMinutes<30)continue;if(queued.Add(id))queue.Enqueue(id);}if(!running&&queue.Count>0){running=true;Task.Run((Action)Work);}}}
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=8*1024*1024,RecursionLimit=64};}
  string Fetch(string url){
   lock(gate){if(!enabled||disposed||executingGeneration!=generation)throw new OperationCanceledException();if(DateTime.UtcNow<backoff)throw new InvalidOperationException("API cooldown until "+backoff.ToLocalTime().ToString("HH:mm:ss"));}
   var req=(HttpWebRequest)WebRequest.Create(url);req.UserAgent="DeadlockHelper/1.0 PlayerScout";req.Accept="application/json";req.Timeout=12000;req.ReadWriteTimeout=12000;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   lock(gate){if(!enabled||disposed||executingGeneration!=generation)throw new OperationCanceledException();request=req;Requests++;}
   try{using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){var buffer=new char[8192];var text=new System.Text.StringBuilder();int count;while((count=reader.Read(buffer,0,buffer.Length))>0){if(text.Length+count>8*1024*1024)throw new InvalidDataException("Response too large");text.Append(buffer,0,count);}return text.ToString();}}
   catch(WebException ex){var response=ex.Response as HttpWebResponse;if(response!=null){using(response){int code=(int)response.StatusCode;if(code==429||code==503||code==504){lock(gate)backoff=RetryAt(response.Headers["Retry-After"],DateTime.UtcNow);}}}throw;}
   finally{lock(gate)if(request==req)request=null;}
  }
  public static DateTime RetryAt(string value,DateTime now){double seconds;DateTime date;if(double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out seconds)&&seconds>=0&&seconds<864000)return now.AddSeconds(Math.Max(30,seconds));if(DateTime.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AdjustToUniversal,out date)&&date>now)return date;return now.AddMinutes(2);}
  void Publish(ScoutProfile p,int gen){lock(gate){if(!disposed&&enabled&&generation==gen){profiles[p.Account]=p;Revision++;}}}
  ScoutProfile ReadCache(long id){try{string path=Path.Combine(folder,id+".json");if(!File.Exists(path)||new FileInfo(path).Length>8*1024*1024)return null;string text=File.ReadAllText(path);if(Json.Number(Json.Obj(Serializer().DeserializeObject(text)),"Schema")!=2)return null;var p=Serializer().Deserialize<ScoutProfile>(text);if(p==null||p.Account!=id||p.History==null||p.Heroes==null||p.Fetched>DateTime.UtcNow||(DateTime.UtcNow-p.Fetched).TotalMinutes>=30)return null;return p;}catch(Exception e){Log.Write("Scout cache ignored",e);return null;}}
  void Work(){
   long[] initial;int initialGeneration;lock(gate){initial=queue.ToArray();initialGeneration=generation;executingGeneration=generation;}
   foreach(long id in initial){var cached=ReadCache(id);if(cached!=null)Publish(cached,initialGeneration);}
   initial=initial.Where(id=>(DateTime.UtcNow-Get(id).RankFetched).TotalMinutes>=30).ToArray();
   try{if(initial.Length>1){var ranks=Serializer().DeserializeObject(Fetch("https://api.deadlock-api.com/v1/players/rank?account_ids="+string.Join(",",initial))) as object[];foreach(var raw in ranks??new object[0]){var rank=Json.Obj(raw);long id=Json.Number(rank,"account_id")??0;if(!initial.Contains(id))continue;var p=Get(id).Copy();p.Badge=(int)(Json.Number(rank,"badge")??0);p.RankFetched=DateTime.UtcNow;var last=Json.Obj(Json.Get(rank,"last_match"));p.RankMatch=Json.Number(last,"match_id")??0;p.RankTime=Json.Number(last,"start_time")??0;Publish(p,initialGeneration);}}}catch(Exception e){if(!(e is OperationCanceledException))Log.Write("Scout rank batch unavailable",e);}
   while(true){long id;int gen;lock(gate){if(disposed||!enabled||queue.Count==0){running=false;return;}id=queue.Dequeue();gen=generation;executingGeneration=gen;}
    var p=Get(id).Copy();string path=Path.Combine(folder,id+".json");
    try{
     ScoutProfile cached=ReadCache(id);
     if(cached!=null){if(p.RankFetched>cached.RankFetched){cached=cached.Copy();cached.Badge=p.Badge;cached.RankFetched=p.RankFetched;cached.RankMatch=p.RankMatch;cached.RankTime=p.RankTime;}p=cached;Publish(p,gen);}
     else {
      try{if((DateTime.UtcNow-p.RankFetched).TotalMinutes>=30){var rank=Json.Obj(Serializer().DeserializeObject(Fetch("https://api.deadlock-api.com/v1/players/"+id+"/rank")));p.Badge=(int)(Json.Number(rank,"badge")??0);p.RankFetched=DateTime.UtcNow;var last=Json.Obj(Json.Get(rank,"last_match"));p.RankMatch=Json.Number(last,"match_id")??0;p.RankTime=Json.Number(last,"start_time")??0;Publish(p,gen);}}catch(WebException){ /* History can remain available when the rank is protected. */ }
      var history=Serializer().DeserializeObject(Fetch("https://api.deadlock-api.com/v1/players/"+id+"/match-history")) as object[];if(history==null)throw new InvalidDataException("Invalid history response");
      p=p.Copy();p.RawHistoryCount=history.Length;p.History=history.Select(ScoutMatch.Parse).Where(m=>m.Completed).OrderByDescending(m=>m.Start).GroupBy(m=>m.Id).Select(g=>g.First()).Take(500).ToArray();p.Loaded=true;p.Fetched=DateTime.UtcNow;Publish(p,gen);
      try{var stats=Serializer().DeserializeObject(Fetch("https://api.deadlock-api.com/v1/players/hero-stats?account_ids="+id+"&game_mode=normal&match_mode=ranked,unranked")) as object[];var heroStats=new Dictionary<string,Dictionary<string,object>>();foreach(var raw in stats??new object[0]){var d=Json.Obj(raw);heroStats[Json.Str(d,"hero_id")]=d;}p=p.Copy();p.Heroes=heroStats;Publish(p,gen);}catch(WebException){p=p.Copy();p.Error="Hero aggregates unavailable";}
      try{long from=(long)(DateTime.UtcNow.Date.AddDays(-14)-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;var mates=Serializer().DeserializeObject(Fetch("https://api.deadlock-api.com/v1/players/"+id+"/mate-stats?game_mode=normal&same_party=true&min_matches_played=3&min_unix_timestamp="+from)) as object[];if(mates!=null){p=p.Copy();p.Mates=mates.Select(Json.Obj).Select(d=>new HistoricalMate{Account=Json.Number(d,"mate_id")??0,Matches=(int)(Json.Number(d,"matches_played")??0),Wins=(int)(Json.Number(d,"wins")??0),MatchIds=(Json.Get(d,"matches") as object[]??new object[0]).Select(x=>Convert.ToInt64(x)).ToArray()}).ToArray();p.MatesFetched=DateTime.UtcNow;Publish(p,gen);}}catch(WebException){/* No evidence means Unknown, never an invented party. */}
      lock(gate){if(enabled&&!disposed&&gen==generation){Directory.CreateDirectory(folder);File.WriteAllText(path,Serializer().Serialize(p));}}
     }
    }catch(Exception e){if(!(e is OperationCanceledException)){p=p.Copy();p.Error=e is WebException?"API unavailable / rate limited":e.Message;Publish(p,gen);}lock(gate)retry[id]=backoff>DateTime.UtcNow?backoff:DateTime.UtcNow.AddMinutes(2);}
    finally{lock(gate){if(gen==generation)queued.Remove(id);}if(p.Loaded&&gen==generation)LoadRankImage(p.Badge);}
   }}
  public void Dispose(){lock(gate){disposed=true;enabled=false;generation++;queue.Clear();if(request!=null)request.Abort();}}
 }
}
