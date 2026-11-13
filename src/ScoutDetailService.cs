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
  CancellationTokenSource cancel;Task worker=Task.FromResult(0);bool disposed;int generation;DateTime backoff;public event Action Changed;
  public int Requests{get;private set;}public string Status{get;private set;}
  public ScoutDetailService(string path){folder=path;Status="";}
  public string Avatar(long id){return Path.Combine(folder,"avatar-"+id+".png");}
  public string Art(int hero){return Path.Combine(folder,"hero-"+hero+".png");}
  static string Key(long id,long match){return id+"-"+match;}
  public ScoutMatchDetail Get(long id,long match){lock(gate){ScoutMatchDetail d;return matches.TryGetValue(Key(id,match),out d)?d:null;}}
  public void Stop(){lock(gate){generation++;if(cancel!=null)cancel.Cancel();}}
  void Notify(){var handler=Changed;if(handler!=null)handler();}
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=24*1024*1024,RecursionLimit=100};}
  byte[] Download(string url,CancellationToken token,int max){token.ThrowIfCancellationRequested();lock(gate){if(DateTime.UtcNow<backoff)throw new InvalidOperationException("cooldown");Requests++;}
   var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=12000;req.ReadWriteTimeout=12000;req.UserAgent="DeadlockHelper/1.0 PlayerScout";req.AllowAutoRedirect=false;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   using(token.Register(()=>req.Abort()))try{using(var response=(HttpWebResponse)req.GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){if((int)response.StatusCode>=300)throw new InvalidDataException("Unexpected redirect");byte[] buffer=new byte[8192];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(memory.Length+count>max)throw new InvalidDataException("Response limit");memory.Write(buffer,0,count);}return memory.ToArray();}}
   catch(WebException e){token.ThrowIfCancellationRequested();var response=e.Response as HttpWebResponse;if(response!=null)using(response){if((int)response.StatusCode==429||(int)response.StatusCode==503||(int)response.StatusCode==504)lock(gate)backoff=ScoutService.RetryAt(response.Headers["Retry-After"],DateTime.UtcNow);}throw;}
  }
  object Fetch(string url,CancellationToken token){return Serializer().DeserializeObject(System.Text.Encoding.UTF8.GetString(Download(url,token,24*1024*1024)));}
  static bool Fresh(string path,int days){return File.Exists(path)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(path)).TotalDays<days;}
  void ImageFile(string url,string path,bool avatar,CancellationToken token){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||(avatar?uri.Host!="avatars.steamstatic.com"&&uri.Host!="avatars.akamai.steamstatic.com":uri.Host!="assets-bucket.deadlock-api.com"))return;
   byte[] bytes=Download(url,token,8*1024*1024);using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)){if(image.Width>4096||image.Height>4096||!avatar&&(image.Width<256||image.Height<256))return;double factor=Math.Min(1,(avatar?184:1000)/(double)Math.Max(image.Width,image.Height));using(var copy=new Bitmap(image,(int)(image.Width*factor),(int)(image.Height*factor))){token.ThrowIfCancellationRequested();copy.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
  }
  public void Open(long id,int hero,ScoutMatch[] history,bool network){
   if(id<=0)return;int gen;CancellationToken token;Task previous;CancellationTokenSource previousCancel;
   lock(gate){if(disposed)return;generation++;gen=generation;previousCancel=cancel;if(cancel!=null)cancel.Cancel();cancel=new CancellationTokenSource();token=cancel.Token;previous=worker;
    if(matches.Count>480)foreach(var key in matches.Keys.Where(k=>!k.StartsWith(id+"-")).ToArray())matches.Remove(key);
    worker=Task.Run(async()=>{try{await previous.ConfigureAwait(false);}catch{}if(previousCancel!=null)previousCancel.Dispose();if(token.IsCancellationRequested)return;Run(id,hero,history.Take(20).ToArray(),network,gen,token);});}
  }
  void Run(long id,int hero,ScoutMatch[] history,bool network,int gen,CancellationToken token){try{
   // Debounce fast profile switching before any disk/network work.
   if(token.WaitHandle.WaitOne(350))return;
   Directory.CreateDirectory(folder);
   foreach(var m in history){token.ThrowIfCancellationRequested();string key=Key(id,m.Id),path=Path.Combine(folder,key+".json");try{if(File.Exists(path)&&new FileInfo(path).Length<524288){var d=Serializer().Deserialize<ScoutMatchDetail>(File.ReadAllText(path));if(d!=null&&d.Account==id&&d.Match==m.Id&&d.Hero==m.Hero&&d.Fetched<=DateTime.UtcNow&&(DateTime.UtcNow-d.Fetched).TotalHours<(d.Available?168:0.1))lock(gate)matches[key]=d;}}catch{/* Corrupt caches are disposable. */}}
   Notify();if(!network)return;
   try{if(!Fresh(Avatar(id),7)){var raw=Fetch("https://api.deadlock-api.com/v1/players/steam?account_ids="+id,token) as object[];var profile=(raw??new object[0]).Select(Json.Obj).FirstOrDefault(p=>Json.Number(p,"account_id")==id);if(profile!=null)ImageFile(Json.Str(profile,"avatarfull"),Avatar(id),true,token);}}catch(OperationCanceledException){throw;}catch{/* Optional avatar: use placeholder. */}
   Notify();
   try{if(hero>0&&!Fresh(Art(hero),30)){var asset=Json.Obj(Fetch("https://api.deadlock-api.com/v1/assets/heroes/"+hero,token));ImageFile(Json.Str(Json.Obj(Json.Get(asset,"images")),"icon_hero_card"),Art(hero),false,token);}}catch(OperationCanceledException){throw;}catch{/* Local high-resolution art or placeholder remains available. */}
   Notify();int completed=0;
   foreach(var m in history){token.ThrowIfCancellationRequested();lock(gate){if(gen!=generation||disposed)return;if(DateTime.UtcNow<backoff){Status="cooldown";break;}}
    var cached=Get(id,m.Id);if(cached!=null&&cached.PayloadVersion==3&&(DateTime.UtcNow-cached.Fetched).TotalHours<(cached.Available?168:0.1)){completed++;continue;}
    var detail=new ScoutMatchDetail{Match=m.Id,Account=id,Hero=m.Hero,Fetched=DateTime.UtcNow};
    try{detail=ScoutMatchDetail.Parse(Fetch("https://api.deadlock-api.com/v1/matches/"+m.Id+"/metadata",token),m.Id,id);if(detail.Hero!=m.Hero)detail.Available=false;}catch(OperationCanceledException){throw;}catch{token.ThrowIfCancellationRequested();}
    token.ThrowIfCancellationRequested();lock(gate){if(gen!=generation||disposed)return;matches[Key(id,m.Id)]=detail;Status=(++completed)+"/"+history.Length;}
    File.WriteAllText(Path.Combine(folder,Key(id,m.Id)+".json"),Serializer().Serialize(detail));Notify();
   }
  }catch(OperationCanceledException){}catch(Exception e){Log.Write("Scout selected profile",e);}finally{if(!token.IsCancellationRequested)Notify();}}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;generation++;var last=cancel;if(last!=null){last.Cancel();worker.ContinueWith(_=>last.Dispose());}Changed=null;}}
 }
}
