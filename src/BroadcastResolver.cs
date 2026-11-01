using System;using System.Collections.Generic;using System.IO;using System.Net;
namespace DeadlockOverlay {
 public sealed class BroadcastWaiting:Exception{public BroadcastWaiting(string text):base(text){}}
 public static class BroadcastResolver {
  static readonly object gate=new object();static readonly Dictionary<long,string> cache=new Dictionary<long,string>();static readonly Dictionary<long,DateTime> retry=new Dictionary<long,DateTime>();static DateTime listNext;
  public static string Find(object data,long id){foreach(var raw in data as object[]??new object[0]){var d=Json.Obj(raw);if(Json.Number(d,"match_id")==id&&Valid(Json.Str(d,"broadcast_url")))return Json.Str(d,"broadcast_url");}return null;}
  static bool Valid(string url){Uri u;return Uri.TryCreate(url,UriKind.Absolute,out u)&&(u.Scheme=="http"||u.Scheme=="https")&&u.Host.EndsWith(".steamcontent.com",StringComparison.OrdinalIgnoreCase);}
  public static string QueryUrl(long match,string query){return "https://api.deadlock-api.com/v1/matches/demo/live/query?broadcast_url="+Uri.EscapeDataString(Resolve(match))+"&query="+Uri.EscapeDataString(query);}
  static void Remember(long match,string url,string path){cache[match]=url;try{Directory.CreateDirectory(SettingsManager.Folder);File.WriteAllText(path,url);}catch{}}
  static string Resolve(long match){lock(gate){string url;if(cache.TryGetValue(match,out url))return url;string saved=Path.Combine(SettingsManager.Folder,"broadcast-"+match+".txt");try{if(File.Exists(saved)&&File.GetLastWriteTimeUtc(saved)>DateTime.UtcNow.AddHours(-1)){url=File.ReadAllText(saved);if(Valid(url)){cache[match]=url;return url;}}}catch{}
   if(DateTime.UtcNow>=listNext){listNext=DateTime.UtcNow.AddSeconds(10);try{url=Find(ItemAdvisor.Fetch("https://api.deadlock-api.com/v1/matches/live/urls"),match);if(url!=null){Remember(match,url,saved);return url;}}catch(Exception e){Log.Write("Public broadcast list",e);}}
   if(LiveBackoff.SpectateDelayMs>0)throw new BroadcastWaiting(L.Choose("No public broadcast yet · spectate limit: ","Трансляции в списке пока нет · лимит просмотра: ")+(int)Math.Ceiling(LiveBackoff.SpectateDelayMs/60000.0)+L.Choose(" min"," мин"));
   DateTime next;if(retry.TryGetValue(match,out next)&&DateTime.UtcNow<next)throw new BroadcastWaiting(L.Choose("Waiting for broadcast · checking the public list","Ожидание трансляции · проверяем публичный список"));retry[match]=DateTime.UtcNow.AddMinutes(2);
   try{url=Json.Str(Json.Obj(ItemAdvisor.Fetch("https://api.deadlock-api.com/v1/matches/"+match+"/live/url")),"broadcast_url");if(!Valid(url))throw new BroadcastWaiting(L.Choose("Source has no broadcast URL yet","Источник ещё не выдал ссылку трансляции"));Remember(match,url,saved);return url;}
   catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&(int)response.StatusCode==429){using(response)using(var r=new StreamReader(response.GetResponseStream()))LiveBackoff.Observe(r.ReadToEnd());throw new BroadcastWaiting(L.Choose("Source limit · waiting for a public broadcast","Лимит источника · ожидаем публичную трансляцию"));}throw;}
  }}
 }
}

