using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadlockOverlay {
 // Only fixed event types, match IDs and public hero/lane numbers; no names, accounts or packet bodies.
 public static class LiveDiagnostics {
  public static volatile bool Enabled;
  public static string Match(string id){long value;return long.TryParse(id,out value)&&value>0?value.ToString():"unknown";}
  static readonly object gate=new object();
  static readonly Dictionary<string,DateTime> rejected=new Dictionary<string,DateTime>();
  public static void Event(string source,string message){if(Enabled)Log.Write("Live validation ["+source+"] "+message);}
  public static void Rejected(string source){
   if(!Enabled)return;lock(gate){DateTime last;var now=DateTime.UtcNow;if(rejected.TryGetValue(source,out last)&&(now-last).TotalSeconds<5)return;rejected[source]=now;}
   Event(source,"stale or conflicting event discarded (rate limited)");
  }
  public static void Roster(string source,string match,IEnumerable<Player> before,IEnumerable<Player> after){
   if(!Enabled)return;var prior=before.GroupBy(CounterContext.Key).ToDictionary(g=>g.Key,g=>g.First());
   foreach(var p in after){Player old;if(!prior.TryGetValue(CounterContext.Key(p),out old))continue;
    if(old.Hero!=p.Hero&&p.Hero>0)Event(source,"match="+match+" team="+p.Team+" hero "+old.Hero+" -> "+p.Hero);
    if(old.OriginalLane!=p.OriginalLane||old.ConfirmedLane!=p.ConfirmedLane)Event(source,"match="+match+" hero="+p.Hero+" original="+Lane(p.OriginalLane)+" confirmed="+Lane(p.ConfirmedLane));
   }
  }
  static string Lane(int? value){return value.HasValue?value.Value.ToString():"unknown";}
 }
}
