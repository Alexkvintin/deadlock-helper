using System;using System.Threading;using System.IO;
namespace DeadlockOverlay {
 public static class LiveBackoff {
  static long untilTicks=Load("live-backoff.txt"),spectateTicks=Load("spectate-backoff.txt");
  static long Load(string name){try{long value;if(long.TryParse(File.ReadAllText(Path.Combine(SettingsManager.Folder,name)),out value)&&value>DateTime.UtcNow.Ticks&&value<DateTime.UtcNow.AddDays(1).Ticks)return value;}catch{}return 0;}
  static int Remaining(long ticks){return (int)Math.Max(0,Math.Min(86400000,(new DateTime(ticks,DateTimeKind.Utc)-DateTime.UtcNow).TotalMilliseconds));}
  public static int DelayMs{get{return Remaining(Interlocked.Read(ref untilTicks));}}
  public static int SpectateDelayMs{get{return Remaining(Interlocked.Read(ref spectateTicks));}}
  public static bool IsSpectateLimit(string detail){var quota=Json.Obj(Json.Get(Json.Obj(Json.Get(Json.Obj(detail),"error")),"quota"));return (Json.Number(quota,"period")??0)>=3600&&(Json.Number(quota,"limit")??100)>0&&(Json.Number(quota,"limit")??100)<=6;}
  public static int RetryMilliseconds(string detail){var error=Json.Obj(Json.Get(Json.Obj(detail),"error"));return (int)Math.Max(60000,Math.Min(86400000,(Json.Number(error,"next_request_in")??60)*1000));}
  static void Save(ref long field,string name,long proposed){long old;do{old=Interlocked.Read(ref field);if(old>=proposed)return;}while(Interlocked.CompareExchange(ref field,proposed,old)!=old);try{Directory.CreateDirectory(SettingsManager.Folder);File.WriteAllText(Path.Combine(SettingsManager.Folder,name),Interlocked.Read(ref field).ToString());}catch{}}
  public static void Observe(string detail){long proposed=DateTime.UtcNow.AddMilliseconds(RetryMilliseconds(detail)).Ticks;if(IsSpectateLimit(detail))Save(ref spectateTicks,"spectate-backoff.txt",proposed);else Save(ref untilTicks,"live-backoff.txt",proposed);}
  public static bool Wait(WaitHandle stop){int delay;while((delay=DelayMs)>0)if(stop.WaitOne(delay))return false;return true;}
 }
}
