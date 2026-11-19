using System;
using System.IO;
using System.Net;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace DeadlockOverlay {
 // One cancellable worker for the selected profile only. No work is started by painting.
 public sealed class ScoutDetailService:IDisposable {
  readonly object gate=new object();readonly string folder;readonly Dictionary<string,ScoutMatchDetail> matches=new Dictionary<string,ScoutMatchDetail>();
  static readonly ScoutMetadataCache sharedMetadata=new ScoutMetadataCache();static readonly object networkGate=new object(),diskGate=new object();static DateTime backoff;
  static readonly SemaphoreSlim requestSlots=new SemaphoreSlim(2,2);
  readonly ScoutMetadataCache metadata;readonly Func<string,CancellationToken,object> fetchOverride;readonly int debounceMs;
  readonly Dictionary<string,DateTime> retryAfter=new Dictionary<string,DateTime>();
  sealed class Selection {
   public long Account;public int Hero,Generation;public ScoutMatch[] History;public bool Network;
   public long InFlightMatch,RetainedMatch;public Task<ScoutMatchDetail> Retained;
   public readonly CancellationTokenSource Cancel=new CancellationTokenSource();public bool CancelQueued,Disposed;
  }
  Selection active,pending;bool running,disposed;int generation;long account;string status="";bool loading;public event Action Changed;
  public int Requests{get;private set;}
  public string Status{get{lock(gate)return status;}}
  public bool IsLoading(long id){lock(gate)return account==id&&loading;}
  public string StatusFor(long id){lock(gate)return account==id?status:"";}
  public ScoutDetailService(string path):this(path,sharedMetadata,null,120){}
  public ScoutDetailService(string path,ScoutMetadataCache cache,Func<string,CancellationToken,object> fetch,int debounce=120){folder=path;metadata=cache??sharedMetadata;fetchOverride=fetch;debounceMs=Math.Max(0,debounce);}
  public string Avatar(long id){return Path.Combine(folder,"avatar-"+id+".png");}
  public string Art(int hero){return Path.Combine(folder,"hero-"+hero+".png");}
  static string Key(long id,long match){return id+"-"+match;}
  public ScoutMatchDetail Get(long id,long match){lock(gate){ScoutMatchDetail d;return matches.TryGetValue(Key(id,match),out d)?d:null;}}
  // Cancel on a worker: aborting an HTTP request must not hold the UI thread or the service lock.
  static void CancelSelection(Selection job){try{job.Cancel.Cancel();}catch(AggregateException e){Log.Write("Scout cancellation",e);}}
  static void RequestCancel(Selection job){if(job==null||job.CancelQueued)return;job.CancelQueued=true;ThreadPool.QueueUserWorkItem(_=>{lock(job){if(!job.Disposed)CancelSelection(job);}});}
  static void CloseSelection(Selection job){lock(job){CancelSelection(job);job.Disposed=true;job.Cancel.Dispose();}if(job.Retained!=null)job.Retained.ContinueWith(t=>{if(t.IsFaulted){var ignored=t.Exception;}},TaskScheduler.Default);}
  static void DiscardPending(Selection job){if(job.Retained==null)CloseSelection(job);else ThreadPool.QueueUserWorkItem(_=>CloseSelection(job));}
  public void Stop(){lock(gate){generation++;account=0;loading=false;status="";if(pending!=null){DiscardPending(pending);pending=null;}RequestCancel(active);}}
  bool Current(Selection job){lock(gate)return !disposed&&job.Generation==generation;}
  void Notify(Selection job){Action handler;lock(gate){if(disposed||job.Generation!=generation)return;handler=Changed;}if(handler!=null)try{handler();}catch(Exception e){Log.Write("Scout profile notification",e);}}
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=24*1024*1024,RecursionLimit=100};}
  static bool Valid(ScoutMatchDetail d,long id,ScoutMatch m){return d!=null&&d.Account==id&&d.Match==m.Id&&d.Hero==m.Hero&&d.Fetched<=DateTime.UtcNow;}
  static bool PreferExisting(ScoutMatchDetail existing,ScoutMatchDetail incoming){
   if(existing.Available&&!incoming.Available)return true;
   if(!existing.Available&&incoming.Available)return false;
   if(existing.HasRecordedData&&!incoming.HasRecordedData)return true;
   // A final timestamp alone does not establish that all previously known fields are present.
   if(existing.Available&&incoming.Available&&((existing.Damage.HasValue&&!incoming.Damage.HasValue)||(existing.Objectives.HasValue&&!incoming.Objectives.HasValue)||(existing.Healing.HasValue&&!incoming.Healing.HasValue)||(existing.TeamHealing.HasValue&&!incoming.TeamHealing.HasValue)))return true;
   if((existing.Timeline??new ReviewSample[0]).Length>(incoming.Timeline??new ReviewSample[0]).Length||(existing.Purchases??new ReviewPurchase[0]).Length>(incoming.Purchases??new ReviewPurchase[0]).Length||(existing.Events??new ReviewEvent[0]).Length>(incoming.Events??new ReviewEvent[0]).Length)return true;
   var samples=(incoming.Timeline??new ReviewSample[0]).Where(s=>s!=null).GroupBy(s=>s.Second).ToDictionary(g=>g.Key,g=>g.Last());
   foreach(var old in existing.Timeline??new ReviewSample[0]){if(old==null)continue;ReviewSample next;if(!samples.TryGetValue(old.Second,out next)||(old.Souls.HasValue&&!next.Souls.HasValue)||(old.Damage.HasValue&&!next.Damage.HasValue)||(old.Objectives.HasValue&&!next.Objectives.HasValue)||(old.Healing.HasValue&&!next.Healing.HasValue))return true;}
   return existing.Fetched>incoming.Fetched&&(existing.Available||existing.HasRecordedData);
  }
  static ScoutMatchDetail ReadDetail(string path,long id,ScoutMatch m){try{if(File.Exists(path)&&new FileInfo(path).Length<524288){var d=Serializer().Deserialize<ScoutMatchDetail>(File.ReadAllText(path));if(Valid(d,id,m)&&(d.Available||d.HasRecordedData||(DateTime.UtcNow-d.Fetched).TotalHours<0.1))return d;}}catch{/* Corrupt/unavailable cache is optional. */}return null;}
  static ScoutMatchDetail StoreDetail(string path,long id,ScoutMatch m,ScoutMatchDetail detail){
   lock(diskGate){var existing=ReadDetail(path,id,m);if(existing!=null&&PreferExisting(existing,detail))return existing;
    if(existing==null&&File.Exists(path)&&!detail.Available&&!detail.HasRecordedData)return detail;
    string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,Serializer().Serialize(detail));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    catch(Exception e){Log.Write("Scout detail cache",e);}finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
    return detail;
   }
  }
  static bool CoolingDown(){lock(networkGate)return DateTime.UtcNow<backoff;}
  byte[] Download(string url,CancellationToken token,int max){requestSlots.Wait(token);try{token.ThrowIfCancellationRequested();if(CoolingDown())throw new InvalidOperationException("cooldown");lock(gate)Requests++;
   var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=12000;req.ReadWriteTimeout=12000;req.UserAgent="DeadlockHelper/1.0 PlayerScout";req.AllowAutoRedirect=false;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   using(token.Register(()=>req.Abort()))try{using(var response=(HttpWebResponse)req.GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){if((int)response.StatusCode>=300)throw new InvalidDataException("Unexpected redirect");byte[] buffer=new byte[8192];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(memory.Length+count>max)throw new InvalidDataException("Response limit");memory.Write(buffer,0,count);}return memory.ToArray();}}
   catch(WebException e){token.ThrowIfCancellationRequested();var response=e.Response as HttpWebResponse;if(response!=null)using(response){if((int)response.StatusCode==429||(int)response.StatusCode==503||(int)response.StatusCode==504)lock(networkGate){var next=ScoutService.RetryAt(response.Headers["Retry-After"],DateTime.UtcNow);if(next>backoff)backoff=next;}}throw;}
  }finally{requestSlots.Release();}}
  object Fetch(string url,CancellationToken token){token.ThrowIfCancellationRequested();if(fetchOverride!=null){lock(gate)Requests++;return fetchOverride(url,token);}return Serializer().DeserializeObject(System.Text.Encoding.UTF8.GetString(Download(url,token,24*1024*1024)));}
  static bool Fresh(string path,int days){return File.Exists(path)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(path)).TotalDays<days;}
  void ImageFile(string url,string path,bool avatar,CancellationToken token){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||(avatar?uri.Host!="avatars.steamstatic.com"&&uri.Host!="avatars.akamai.steamstatic.com":uri.Host!="assets-bucket.deadlock-api.com"))return;
   byte[] bytes=Download(url,token,8*1024*1024);using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)){if(image.Width>4096||image.Height>4096||!avatar&&(image.Width<256||image.Height<256))return;double factor=Math.Min(1,(avatar?184:1000)/(double)Math.Max(image.Width,image.Height));using(var copy=new Bitmap(image,(int)(image.Width*factor),(int)(image.Height*factor))){token.ThrowIfCancellationRequested();copy.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
  }
  public void Open(long id,int hero,ScoutMatch[] history,bool network){
   if(id<=0)return;
   var selectedHistory=(history??new ScoutMatch[0]).Where(m=>m!=null&&m.Id>0&&m.Hero>0).GroupBy(m=>m.Id).Select(g=>g.First()).Take(20).Select(m=>new ScoutMatch{Id=m.Id,Hero=m.Hero}).ToArray();
   lock(gate){if(disposed)return;var current=pending??active;if(loading&&current!=null&&current.Generation==generation&&current.Account==id&&current.Hero==hero&&current.Network==network&&current.History.Select(m=>new{m.Id,m.Hero}).SequenceEqual(selectedHistory.Select(m=>new{m.Id,m.Hero})))return;
    generation++;account=id;loading=true;status="";
    var next=new Selection{Account=id,Hero=hero,History=selectedHistory,Network=network,Generation=generation};
    // Register the new consumer before cancelling the old one when both need the same match.
    if(network){var handoff=selectedHistory.FirstOrDefault(m=>m.Id!=0&&(m.Id==(pending==null?0:pending.InFlightMatch)||m.Id==(active==null?0:active.InFlightMatch)));if(handoff!=null){next.Retained=metadata.JoinExisting(handoff.Id,id,handoff.Hero,next.Cancel.Token);if(next.Retained!=null)next.InFlightMatch=next.RetainedMatch=handoff.Id;}}
    if(pending!=null)DiscardPending(pending);pending=next;
    RequestCancel(active);
    if(matches.Count>480)foreach(var key in matches.Keys.Where(k=>!k.StartsWith(id+"-")).ToArray())matches.Remove(key);
    foreach(var key in retryAfter.Where(p=>p.Value<=DateTime.UtcNow).Select(p=>p.Key).ToArray())retryAfter.Remove(key);
    if(!running){running=true;Task.Run((Action)Pump);}
   }
  }
  void Pump(){while(true){Selection job;lock(gate){if(disposed||pending==null){running=false;return;}job=pending;pending=null;active=job;}
   try{Run(job);}finally{lock(gate){active=null;}CloseSelection(job);}
  }}
  void Run(Selection job){long id=job.Account;int hero=job.Hero;var history=job.History.OrderByDescending(m=>m.Id==job.RetainedMatch).ToArray();bool network=job.Network;var token=job.Cancel.Token;try{
   // Debounce fast profile switching before any disk/network work.
   if(token.WaitHandle.WaitOne(debounceMs)||!Current(job))return;
   Directory.CreateDirectory(folder);
   foreach(var m in history){token.ThrowIfCancellationRequested();if(!Current(job))return;string key=Key(id,m.Id),path=Path.Combine(folder,key+".json");var d=ReadDetail(path,id,m);if(d!=null)lock(gate){if(job.Generation==generation){ScoutMatchDetail present;matches.TryGetValue(key,out present);if(!Valid(present,id,m)||!PreferExisting(present,d))matches[key]=d;}}}
   Notify(job);if(!network||!Current(job))return;
   try{if(!Fresh(Avatar(id),7)){var raw=Fetch("https://api.deadlock-api.com/v1/players/steam?account_ids="+id,token) as object[];var profile=(raw??new object[0]).Select(Json.Obj).FirstOrDefault(p=>Json.Number(p,"account_id")==id);if(profile!=null)ImageFile(Json.Str(profile,"avatarfull"),Avatar(id),true,token);}}catch(OperationCanceledException){throw;}catch{/* Optional avatar: use placeholder. */}
   Notify(job);if(!Current(job))return;
   try{if(hero>0&&!Fresh(Art(hero),30)){var asset=Json.Obj(Fetch("https://api.deadlock-api.com/v1/assets/heroes/"+hero,token));ImageFile(Json.Str(Json.Obj(Json.Get(asset,"images")),"icon_hero_card"),Art(hero),false,token);}}catch(OperationCanceledException){throw;}catch{/* Local high-resolution art or placeholder remains available. */}
   Notify(job);int completed=0;
   foreach(var m in history){token.ThrowIfCancellationRequested();if(!Current(job))return;string key=Key(id,m.Id);
    var cached=Get(id,m.Id);if(!Valid(cached,id,m))cached=null;DateTime retry;bool delayed;lock(gate)delayed=retryAfter.TryGetValue(key,out retry)&&retry>DateTime.UtcNow;
    if(cached!=null&&(delayed||cached.PayloadVersion==ScoutMatchDetail.CurrentVersion&&(DateTime.UtcNow-cached.Fetched).TotalHours<(cached.Available?168:0.1))){completed++;continue;}
    var detail=new ScoutMatchDetail{Match=m.Id,Account=id,Hero=m.Hero,Fetched=DateTime.UtcNow,PayloadVersion=ScoutMatchDetail.CurrentVersion};
    try{Task<ScoutMatchDetail> request;lock(gate){if(job.Generation!=generation||disposed)return;job.InFlightMatch=m.Id;request=job.RetainedMatch==m.Id&&job.Retained!=null?job.Retained:metadata.ResolveAsync(m.Id,id,m.Hero,sharedToken=>Fetch("https://api.deadlock-api.com/v1/matches/"+m.Id+"/metadata",sharedToken),token);}
     var parsed=request.GetAwaiter().GetResult();if(parsed!=null)detail=parsed;
    }catch(OperationCanceledException){throw;}catch{token.ThrowIfCancellationRequested();}finally{lock(gate)job.InFlightMatch=0;}
    if(!Current(job))return;
    if(!detail.Available&&!detail.HasRecordedData&&CoolingDown()&&fetchOverride==null){lock(gate){if(job.Generation==generation)status="cooldown";}break;}
    // An unavailable refresh must not erase previously recorded history.
    bool keepCached=cached!=null&&PreferExisting(cached,detail);
    token.ThrowIfCancellationRequested();if(!Current(job))return;
    var chosen=keepCached?cached:StoreDetail(Path.Combine(folder,key+".json"),id,m,detail);
    lock(gate){if(job.Generation!=generation||disposed)return;matches[key]=chosen;if(keepCached||!ReferenceEquals(chosen,detail)){retryAfter[key]=DateTime.UtcNow.AddMinutes(6);if(retryAfter.Count>480)retryAfter.Remove(retryAfter.OrderBy(p=>p.Value).First().Key);}status=(++completed)+"/"+history.Length;}
    Notify(job);
   }
  }catch(OperationCanceledException){}catch(Exception e){Log.Write("Scout selected profile",e);lock(gate){if(job.Generation==generation)status="unavailable";}}finally{lock(gate){if(job.Generation==generation)loading=false;}Notify(job);}}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;generation++;account=0;loading=false;if(pending!=null){DiscardPending(pending);pending=null;}RequestCancel(active);Changed=null;}}
 }
}
