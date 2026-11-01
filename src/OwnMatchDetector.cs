using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
namespace DeadlockOverlay {
 // Public roster lookup is independent of OCR and never captures the desktop.
 public sealed class OwnMatchDetector:IDisposable {
  readonly Timer timer;int busy,disposed;DateTime next=DateTime.MinValue;bool hadGame;
  public volatile bool Enabled;
  public event Action<long> Found;
  public event Action<string> Status;
  public OwnMatchDetector(){timer=new Timer(_=>Poll(),null,200,1000);}
  public static long AccountId(string steam){long id;return long.TryParse(steam,out id)&&id>76561197960265728L&&id<=76561197960265728L+uint.MaxValue?id-76561197960265728L:0;}
  public static long Find(object data,long account){
   if(account<=0)return 0;long result=0;
   foreach(var raw in data as object[]??new object[0]){var m=Json.Obj(raw);long id=Json.Number(m,"match_id")??0;var winner=Json.Number(m,"winning_team");if(id<=0||winner==0||winner==1)continue;
    bool own=(Json.Get(m,"players") as object[]??new object[0]).Select(Json.Obj).Any(p=>Json.Number(p,"account_id")==account&&!Json.Bool(p,"abandoned"));
    if(own){if(result!=0&&result!=id)return 0;result=id;}
   }return result;
  }
  static bool Running(){bool found=false;foreach(string name in new[]{"deadlock","project8"})foreach(var p in Process.GetProcessesByName(name)){found=true;p.Dispose();}return found;}
  void Report(string message){if(Volatile.Read(ref disposed)==0&&Enabled){var h=Status;if(h!=null)h(message);}}
  void Poll(){if(Interlocked.Exchange(ref busy,1)!=0)return;try{
   if(Volatile.Read(ref disposed)!=0||!Enabled||DateTime.UtcNow<next)return;
   if(!Running()){next=DateTime.UtcNow.AddSeconds(3);if(hadGame){hadGame=false;var ended=Found;if(ended!=null)ended(0);}Report(L.Choose("Waiting for Deadlock","Ожидание запуска Deadlock"));return;}hadGame=true;
   next=DateTime.UtcNow.AddSeconds(10);long account=AccountId(ViewedHero.LocalSteamId());
   if(account==0){Report(L.Choose("Steam account unavailable · use a match ID or screen recognition","Аккаунт Steam не определён · введите ID или включите распознавание"));return;}
   var req=(HttpWebRequest)WebRequest.Create("https://api.deadlock-api.com/v1/matches/active?account_ids="+account);req.Timeout=8000;req.ReadWriteTimeout=8000;req.UserAgent="DeadlockHelper/1.0";ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   object data;using(var response=req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))data=new JavaScriptSerializer{MaxJsonLength=2097152}.DeserializeObject(reader.ReadToEnd());
   if(Volatile.Read(ref disposed)!=0||!Enabled)return;long id=Find(data,account);
   if(id>0){var h=Found;if(h!=null)h(id);Report(L.Choose("Your match found via Steam account: ","Ваш матч найден по аккаунту Steam: ")+id);}
   else Report(L.Choose("Your match is not in the public list · use screen recognition or enter its ID","Вашего матча нет в публичном списке · включите распознавание или введите ID"));
  }catch(WebException e){int delay=30;var response=e.Response as HttpWebResponse;if(response!=null){int seconds;if(int.TryParse(response.Headers["Retry-After"],out seconds))delay=Math.Max(delay,Math.Min(86400,seconds));response.Dispose();}next=DateTime.UtcNow.AddSeconds(delay);Report(L.Choose("Match lookup unavailable · retry in ","Поиск матча недоступен · повтор через ")+delay+L.Choose(" s. Manual ID remains available."," с. Можно ввести ID вручную."));Log.Write("Own match lookup",e);
  }catch(Exception e){next=DateTime.UtcNow.AddSeconds(30);Log.Write("Own match lookup",e);}finally{Volatile.Write(ref busy,0);}}
  public void Dispose(){Interlocked.Exchange(ref disposed,1);timer.Dispose();}
 }
}
