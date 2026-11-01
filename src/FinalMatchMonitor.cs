using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace DeadlockOverlay {
 public sealed class FinalMatchMonitor {
  readonly Func<string,object> fetch;
  public FinalMatchMonitor():this(ItemAdvisor.Fetch){}
  public FinalMatchMonitor(Func<string,object> fetch){this.fetch=fetch;}
  readonly object gate=new object();string match="";Dictionary<string,object> result;DateTime next=DateTime.MinValue;bool busy;
  public static bool IsFinal(Dictionary<string,object> info,string id){long? winner=Json.Number(info,"winning_team");return Json.Str(info,"match_id")==id&&(winner==0||winner==1)&&(Json.Number(info,"duration_s")??0)>0&&(Json.Get(info,"players") as object[]??new object[0]).Length>=2;}
  public bool Update(MatchState state,MapSnapshot map){lock(gate){
   if(match!=state.MatchId){match=state.MatchId;result=null;next=DateTime.MinValue;}
   if(match.Length==0||state.Demo||state.Players.Count==0)return false;
   if(result!=null){double duration=Json.Number(result,"duration_s")??0;if(!map.Ended&&map.ScreenClock&&map.Seconds.HasValue&&map.Seconds.Value<duration-3)return false;
    foreach(var raw in Json.Get(result,"players") as object[]??new object[0]){var p=Json.Obj(raw);string steam=((Json.Number(p,"account_id")??0)+76561197960265728L).ToString();var target=state.Players.Values.FirstOrDefault(x=>x.SteamId==steam);if(target==null)continue;var stats=(Json.Get(p,"stats") as object[]??new object[0]).Select(Json.Obj).OrderBy(x=>Json.Number(x,"time_stamp_s")??0).LastOrDefault();if(stats==null)continue;target.Damage=Json.Number(stats,"player_damage");target.ObjectDamage=Json.Number(stats,"boss_damage");target.Healing=Json.Number(stats,"player_healing");target.Kills=Json.Number(p,"kills");target.Deaths=Json.Number(p,"deaths");target.Assists=Json.Number(p,"assists");target.Souls=Json.Number(p,"net_worth");target.UnspentSouls=null;target.Updated=DateTime.UtcNow;}
    state.WinningTeam=(int)(Json.Number(result,"winning_team").Value+2);state.DurationSeconds=Json.Number(result,"duration_s");
    state.Healthy=true;state.Received=DateTime.UtcNow;return true;
   }
   // A delayed but healthy stream is not proof that a match is still running.
   if(!busy&&DateTime.UtcNow>=next){busy=true;next=DateTime.UtcNow.AddSeconds(map.Ended?4:10);string requested=match;Task.Run(()=>{try{var metadata=Json.Obj(fetch("https://api.deadlock-api.com/v1/matches/"+requested+"/metadata"));var info=Json.Obj(Json.Get(metadata,"match_info"));lock(gate){if(match==requested&&IsFinal(info,requested)){result=info;}}}catch{}finally{lock(gate)busy=false;}});}
   return false;
  }}
 }
}



