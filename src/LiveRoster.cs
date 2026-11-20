using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadlockOverlay {
 // Reconciles only reported roster identities. Statistics are never carried between snapshots.
 public sealed class LiveRoster {
  string scope="";readonly Dictionary<string,Player> known=new Dictionary<string,Player>();Player[] rows=new Player[0];
  public int Revision{get;private set;}
  public Player[] Rows{get{return rows;}}
  static string Key(Player p){return p.AccountId>0?"a:"+p.AccountId:"s:"+p.Slot;}
  public static bool NewerPacket(Dictionary<string,object> pending,Dictionary<string,object> incoming){if(pending==null||incoming==null)return incoming!=null;var first=Json.Number(pending,"sequence");var second=Json.Number(incoming,"sequence");bool newer=Json.Str(pending,"session")!=Json.Str(incoming,"session")||!first.HasValue||!second.HasValue||second>first;if(!newer)LiveDiagnostics.Rejected("pending");return newer;}
  public void Reset(){known.Clear();rows=new Player[0];scope="";Revision++;}
  static string Fingerprint(IEnumerable<Player> players){return string.Join(";",players.OrderBy(Key).Select(p=>Key(p)+":"+p.Team+":"+p.Hero+":"+p.Slot+":"+p.Name+":"+p.Local+":"+p.OriginalLane+":"+p.ConfirmedLane+":"+p.Lane));}
  public void Update(string matchScope,IEnumerable<Player> snapshot){
   if(string.IsNullOrEmpty(matchScope))return;
   if(scope!=matchScope){known.Clear();rows=new Player[0];scope=matchScope;Revision++;}
   var before=Fingerprint(known.Values);var input=snapshot.Where(p=>p!=null&&(p.Team==2||p.Team==3)).ToArray();
   var duplicateAccounts=new HashSet<long>(input.Where(p=>p.AccountId>0).GroupBy(p=>p.AccountId).Where(g=>g.Count()>1).Select(g=>g.Key));
   foreach(var p in input){if(duplicateAccounts.Contains(p.AccountId))continue;var key=Key(p);Player old;known.TryGetValue(key,out old);
    if(p.AccountId==0&&known.Values.Any(x=>x.AccountId>0&&x.Slot==p.Slot&&x.Team==p.Team))continue;
    if(old!=null&&old.Team==p.Team){if(p.Hero<=0){p.Hero=old.Hero;p.HeroName=old.HeroName;}else if(string.IsNullOrEmpty(p.HeroName))p.HeroName=LocalSnapshot.HeroName(p.Hero);if(string.IsNullOrEmpty(p.Name))p.Name=old.Name;if(!p.OriginalLane.HasValue)p.OriginalLane=old.OriginalLane;}
    // Confirmed lane is snapshot-scoped; missing evidence is unknown, not a rotation or a previous assignment.
    var row=new Player{Slot=p.Slot,Team=p.Team,Hero=p.Hero,HeroName=p.HeroName,Name=p.Name,SteamId=p.SteamId,Local=p.Local,Lane=p.Lane,OriginalLane=p.OriginalLane,ConfirmedLane=p.ConfirmedLane,HudOrder=p.HudOrder,LobbySlot=p.LobbySlot,Updated=p.Updated};
    known[key]=row;
   }
   // A fully identified snapshot replaces the previous roster; partial snapshots do not remove absent players.
   var observed=new HashSet<string>(input.Where(p=>!duplicateAccounts.Contains(p.AccountId)).Select(Key));
   if(input.Length==12&&duplicateAccounts.Count==0&&input.Select(Key).Distinct().Count()==12&&input.All(p=>p.AccountId>0)){foreach(var key in known.Keys.Where(k=>!observed.Contains(k)).ToArray())known.Remove(key);}
   foreach(var pair in known.Where(p=>!observed.Contains(p.Key)).ToArray())pair.Value.ConfirmedLane=null;
   foreach(var key in known.Where(k=>k.Value.AccountId==0&&known.Values.Any(p=>p.AccountId>0&&p.Slot==k.Value.Slot&&p.Team==k.Value.Team)).Select(k=>k.Key).ToArray())known.Remove(key);
   foreach(var key in known.Where(p=>!observed.Contains(p.Key)).OrderBy(p=>p.Value.Updated).Take(Math.Max(0,known.Count-12)).Select(p=>p.Key).ToArray())known.Remove(key);
   if(before!=Fingerprint(known.Values)){rows=known.Values.OrderBy(p=>p.Team).ThenBy(p=>p.Slot).ToArray();Revision++;}
  }
 }
}
