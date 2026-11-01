using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace DeadlockOverlay {
 // A private child-process pipe, not a game hook or a public HTTP endpoint.
 public sealed class LocalGameSource:IDisposable {
  readonly object gate=new object(); readonly Timer timer; Process child; bool disposed;DateTime lastMessage;
  public volatile bool Enabled;
  public event Action<Dictionary<string,object>> Snapshot;
  public LocalGameSource(){timer=new Timer(_=>Poll(),null,500,2000);}
  void Poll(){lock(gate){
   if(disposed)return;
   if(!Enabled){Stop();return;}
   if(child!=null&&!child.HasExited&&(DateTime.UtcNow-lastMessage).TotalSeconds<15)return;
   Stop();string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"companion-reader.exe");if(!File.Exists(path))return;
   try {
    var p=new Process{StartInfo=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8}};
    child=p;lastMessage=DateTime.UtcNow;p.OutputDataReceived+=(s,e)=>{if(e.Data==null||e.Data.Length>131072||!Enabled)return;try{var d=Json.Obj(Json.Serializer().DeserializeObject(e.Data));lock(gate){if(disposed||child!=p)return;lastMessage=DateTime.UtcNow;}var h=Snapshot;if(h!=null)h(d);}catch(Exception ex){Log.Write("Local snapshot",ex);}};
    p.ErrorDataReceived+=(s,e)=>{};p.Start();p.PriorityClass=ProcessPriorityClass.BelowNormal;p.BeginOutputReadLine();p.BeginErrorReadLine();
   }catch(Exception e){Log.Write("Local reader startup",e);Stop();}
  }}
  void Stop(){if(child==null)return;try{if(!child.HasExited)child.Kill();}catch{}child.Dispose();child=null;}
  public void Dispose(){lock(gate){disposed=true;timer.Dispose();Stop();}}
 }
 public static class LocalSnapshot {
  static readonly Dictionary<int,string> heroes=LoadHeroes();
  static Dictionary<int,string> LoadHeroes(){var names=new Dictionary<int,string>();try{foreach(var raw in Json.Serializer().DeserializeObject(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","sources.json"))) as object[]??new object[0]){var h=Json.Obj(raw);names[(int)(Json.Number(h,"id")??0)]=Json.Str(h,"name");}}catch(Exception e){Log.Write("Local hero names",e);}return names;}
  public static double? Number(Dictionary<string,object> d,string key){double n;return double.TryParse(Json.Str(d,key),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?(double?)n:null;}
  public static Dictionary<string,object> Packet(Dictionary<string,object> d){
   if(Json.Str(d,"kind")!="snapshot"||(Json.Number(d,"match_id")??0)<=0)return null;
   string phase=Json.Str(d,"phase");if(phase!="GameInProgress"&&!Ended(phase))return null;
   var roster=new Dictionary<string,object>{{"match_id",Json.Str(d,"match_id")}};var ages=new Dictionary<string,object>();var slots=new HashSet<long>();int index=0;
   foreach(var raw in Json.Get(d,"players") as object[]??new object[0]){
    var p=Json.Obj(raw);long team=Json.Number(p,"team")??0,slot=Json.Number(p,"slot")??-1;
    if((team!=2&&team!=3)||slot<0||!slots.Add(slot)||index>=12)return null;
    int hero=(int)(Json.Number(p,"hero_id")??0);string name;if(!heroes.TryGetValue(hero,out name))name="Hero "+hero;
    var row=new Dictionary<string,object>{{"player_name",Json.Get(p,"name")},{"hero_name",name},{"hero_id",hero},{"steam_id",Json.Get(p,"steam_id")},{"is_local",Json.Get(p,"is_local")},{"team_id",team},{"lobby_slot",Json.Get(p,"lobby_slot")},
     {"hero_damage",Json.Get(p,"hero_damage")},{"object_damage",Json.Get(p,"objective_damage")},{"hero_healing",Json.Get(p,"healing")},{"kills",Json.Get(p,"kills")},{"deaths",Json.Get(p,"deaths")},{"assists",Json.Get(p,"assists")},{"souls",Json.Get(p,"net_worth")},
     {"rejuvenator",Json.Get(p,"rejuvenator")},{"rebirth",Json.Get(p,"rebirth")},{"unspent_souls",Json.Get(p,"unspent_souls")},{"lane",Json.Get(p,"lane")}};
    row["hud_order"]=Json.Get(p,"hud_order");string key="roster_"+index++;roster[key]=row;ages[key]=0;
   }
   if(index==0)return null;
   // Keep the final roster until the application captures its report.
   bool local=false;foreach(var entry in roster)if(entry.Key.StartsWith("roster_"))local|=Json.Bool(Json.Obj(entry.Value),"is_local");
   return new Dictionary<string,object>{{"session",Json.Str(d,"session")},{"sequence",Json.Get(d,"sequence")},{"healthy",true},{"spectator",Json.Bool(d,"spectator")||!local},{"status",L.Choose("Local game · live data","Игра · локальные данные")},{"ages",ages},
    {"info",new Dictionary<string,object>{{"game_info",new Dictionary<string,object>{{"phase","GameInProgress"}}},{"match_info",roster}}}};
  }
  public static bool Ended(string phase){return phase=="PostGame"||phase=="PostGame_PlayOfTheGame"||phase=="End";}
  public static MapSnapshot Map(Dictionary<string,object> d){
   var seconds=Number(d,"seconds");var boss=Number(d,"boss_in");if(!boss.HasValue&&Json.Bool(d,"boss_alive"))boss=0;return new MapSnapshot{Fresh=true,ScreenClock=true,Seconds=seconds,Paused=Json.Bool(d,"paused"),Ended=Ended(Json.Str(d,"phase")),StreetBrawl=Json.Bool(d,"brawl"),NextBoss=seconds.HasValue&&boss.HasValue?(boss.Value<=0?0:seconds+boss):null};
  }
 }
}
