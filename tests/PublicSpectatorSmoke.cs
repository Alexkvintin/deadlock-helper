using System;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using DeadlockOverlay;

// Explicit opt-in live check. No game access, screen capture, UI, or profile lookups.
// Uses only the existing public spectator connector and its normal cooldown.
class PublicSpectatorSmoke {
 static int Main(string[] args){
  long id;if(args.Length!=1||!long.TryParse(args[0],out id)||id<=0)return 2;
  L.Language="en";
  var gate=new object();var state=new MatchState();int snapshots=0,distinctTicks=0,maxRows=0,maxStats=0,maxHeroes=0,original=0,confirmed=0;long lastTick=-1;string status="";
  using(var connector=new LiveBroadcastConnector(id)){
   connector.SnapshotData+=packet=>{lock(gate){if(!state.Apply(packet,DateTime.UtcNow))return;snapshots++;status=state.SourceStatus;maxRows=Math.Max(maxRows,state.KnownRoster.Length);maxHeroes=Math.Max(maxHeroes,state.KnownRoster.Count(p=>p.Hero>0));maxStats=Math.Max(maxStats,state.Players.Values.Count(p=>p.Damage.HasValue&&p.ObjectDamage.HasValue&&p.Healing.HasValue));original=Math.Max(original,state.KnownRoster.Count(p=>p.OriginalLane.HasValue));confirmed=Math.Max(confirmed,state.KnownRoster.Count(p=>p.ConfirmedLane.HasValue));if(state.GameTick>0&&state.GameTick!=lastTick){distinctTicks++;lastTick=state.GameTick;}}};
   var watch=Stopwatch.StartNew();connector.Start();
   while(watch.Elapsed.TotalSeconds<18){Thread.Sleep(250);lock(gate){if(maxRows==12&&maxStats==12&&distinctTicks>=2)break;}if(LiveBackoff.DelayMs>0)break;}
   lock(gate){Console.WriteLine("Match "+id+": snapshots="+snapshots+" distinctTicks="+distinctTicks+" roster="+maxRows+" heroes="+maxHeroes+" complete damage/healing rows="+maxStats+" original lanes="+original+" confirmed lanes="+confirmed+" elapsedMs="+watch.ElapsedMilliseconds);Console.WriteLine("Public connector: "+status);return maxRows==12&&maxStats==12&&distinctTicks>=2?0:1;}
  }
 }
}
