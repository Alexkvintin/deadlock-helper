using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
namespace DeadlockOverlay {
 public enum PartyConfidence { Unknown,Likely,Confirmed }
 public sealed class HistoricalMate {public long Account;public int Matches,Wins;public long[] MatchIds=new long[0];}
 public sealed class CurrentPartyEvidence {public long Account,MatchId;public string Group="",Source="";public DateTime Observed;public bool DirectCurrentRoster;}
 public sealed class PartyInfo {
  public PartyConfidence Confidence;public string Group="",Reason="";public Color Color;public long[] Members=new long[0];
  public static PartyInfo Unknown(){return new PartyInfo{Confidence=PartyConfidence.Unknown,Reason=L.Choose("Current party is unknown. Shared games alone do not establish a group.","Текущая пати неизвестна. Общие игры сами по себе не подтверждают группу.")};}
 }
 // One registry per match/session. Keep it when the roster or evidence changes.
 // Persist Export() and restore with the constructor to preserve allocations on restart.
 // State contains identities only: it never persists or manufactures party evidence.
 public sealed class PartySessionRegistry {
  readonly List<string> identities=new List<string>();
  readonly Dictionary<string,int> slots=new Dictionary<string,int>(StringComparer.Ordinal);
  static readonly Color[] palette={Color.FromArgb(86,180,233),Color.FromArgb(230,159,0),Color.FromArgb(0,158,115),Color.FromArgb(204,121,167),Color.FromArgb(240,228,66),Color.FromArgb(213,94,0)};
  public PartySessionRegistry(){}
  public PartySessionRegistry(string state){
   if(state==null)throw new ArgumentNullException("state");
   var lines=state.Split('\n');
   if(lines[0]!="party-slots-v1")throw new ArgumentException("Unsupported party registry state.","state");
   foreach(var key in lines.Skip(1)){
    if(key.Length==0||slots.ContainsKey(key))throw new ArgumentException("Invalid party registry state.","state");
    slots.Add(key,identities.Count);identities.Add(key);
   }
  }
  public string Export(){lock(slots)return "party-slots-v1"+(identities.Count==0?"":"\n"+string.Join("\n",identities));}
  internal static string Key(int team,IEnumerable<long> members){return team.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+string.Join(",",members.Distinct().OrderBy(x=>x).Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)));}
  internal void Decorate(PartyInfo info,int team){lock(slots){
   string key=Key(team,info.Members);int slot;
   if(!slots.TryGetValue(key,out slot)){
    // Never wrap/reuse a color: fail explicitly if the RGB space is exhausted.
    if(identities.Count>=0x1000000)throw new InvalidOperationException("Party color space exhausted.");
    slot=identities.Count;slots.Add(key,slot);identities.Add(key);
   }
   int n=slot;string label="";do{label=(char)('A'+n%26)+label;n=n/26-1;}while(n>=0);
   info.Group=label;info.Color=SlotColor(slot);
  }}
  static Color SlotColor(int slot){
   if(slot<palette.Length)return palette[slot];
   // Enumerate remaining RGB values bijectively, excluding the reserved palette.
   int rgb=slot-palette.Length;
   foreach(int reserved in palette.Select(c=>c.ToArgb()&0xffffff).OrderBy(x=>x))if(rgb>=reserved)rgb++;
   return Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255);
  }
 }
 public static class PartyDetectionService {
  // Compatibility identity only; display allocation uses the full identity, never this hash.
  static readonly Dictionary<string,PartySessionRegistry> sessions=new Dictionary<string,PartySessionRegistry>(StringComparer.Ordinal);
  // The legacy API cannot distinguish successive unknown-ID matches with identical rosters.
  // Use the registry overload for explicit lifecycle and Export/restore for restart persistence.
  public static uint Identity(int team,IEnumerable<long> members){unchecked{uint hash=2166136261;foreach(char c in team+":"+string.Join(",",members.Distinct().OrderBy(x=>x)))hash=(hash^c)*16777619;return hash;}}
  public static Dictionary<long,PartyInfo> Analyze(ScoutRow[] rows,Func<long,ScoutProfile> profile,IEnumerable<CurrentPartyEvidence> current,long match,DateTime now){
   string key=match>0?"match:"+match.ToString(System.Globalization.CultureInfo.InvariantCulture):"roster:"+string.Join(";",rows.GroupBy(r=>r.Team).OrderBy(g=>g.Key).Select(g=>PartySessionRegistry.Key(g.Key,g.Select(r=>r.Account))));
   PartySessionRegistry session;lock(sessions){if(!sessions.TryGetValue(key,out session)){session=new PartySessionRegistry();sessions.Add(key,session);}}
   return Analyze(rows,profile,current,match,now,session);
  }
  public static Dictionary<long,PartyInfo> Analyze(ScoutRow[] rows,Func<long,ScoutProfile> profile,IEnumerable<CurrentPartyEvidence> current,long match,DateTime now,PartySessionRegistry session){
   if(session==null)throw new ArgumentNullException("session");
   var result=rows.ToDictionary(r=>r.Account,r=>PartyInfo.Unknown());var available=rows.ToDictionary(r=>r.Account);
   Action<long[],PartyConfidence,string> assign=(members,confidence,reason)=>{if(members.Length<2)return;var info=new PartyInfo{Confidence=confidence,Members=members,Reason=reason};foreach(long id in members)result[id]=info;};
   // Only an explicit current-roster source can confirm a group. No current producer
   // supplies this evidence; history, friendship and same_party never enter here.
   var direct=(current??new CurrentPartyEvidence[0]).Where(e=>e.DirectCurrentRoster&&match>0&&e.MatchId==match&&e.Source.Length>0&&e.Group.Length>0&&e.Observed<=now&&(now-e.Observed).TotalSeconds<=30&&available.ContainsKey(e.Account)).ToArray();
   var conflicting=new HashSet<long>(direct.GroupBy(e=>e.Account).Where(g=>g.Select(e=>e.Group).Distinct().Count()>1).Select(g=>g.Key));
   foreach(var group in direct.Where(e=>!conflicting.Contains(e.Account)).GroupBy(e=>available[e.Account].Team+":"+e.Group).OrderBy(g=>g.Key))assign(group.Select(e=>e.Account).Distinct().OrderBy(x=>x).ToArray(),PartyConfidence.Confirmed,L.Choose("Direct current-roster evidence: ","Прямые сведения текущего состава: ")+group.First().Source);
   foreach(var team in rows.GroupBy(r=>r.Team).OrderBy(g=>g.Key)){
    var unused=new List<ScoutRow>(team.Where(r=>result[r.Account].Confidence==PartyConfidence.Unknown).OrderBy(r=>r.Account));
    while(unused.Count>0){var seed=unused[0];unused.RemoveAt(0);var group=new List<ScoutRow>{seed};var reasons=new List<string>();foreach(var candidate in unused.ToArray()){
      var evidence=group.Select(r=>LikelyEvidence(profile(r.Account),profile(candidate.Account),now)).ToArray();if(evidence.All(e=>e!=null)){group.Add(candidate);unused.Remove(candidate);reasons.AddRange(evidence);}
     }
     if(group.Count>1)assign(group.Select(r=>r.Account).ToArray(),PartyConfidence.Likely,L.Choose("Likely, not confirmed. ","Вероятно, не подтверждено. ")+string.Join("\n",reasons.Distinct()));
    }
   }
   // Allocate after detection in canonical order, independent of confidence/source order.
   lock(session){foreach(var info in result.Values.Where(p=>p.Confidence!=PartyConfidence.Unknown).Distinct().OrderBy(p=>available[p.Members[0]].Team).ThenBy(p=>p.Members.Min()))session.Decorate(info,available[info.Members[0]].Team);}
   return result;
  }
  public static string LikelyEvidence(ScoutProfile a,ScoutProfile b,DateTime now){
   if(!a.Loaded||!b.Loaded||a.Fetched>now||b.Fetched>now||(now-a.Fetched).TotalMinutes>30||(now-b.Fetched).TotalMinutes>30)return null;
   var left=ScoutStatistics.History(a).Take(20).ToArray();var right=ScoutStatistics.History(b).Take(20).ToDictionary(m=>m.Id);long min=(long)(now.AddDays(-14)-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
   var shared=left.Where(m=>m.Start>=min&&m.Team.HasValue&&right.ContainsKey(m.Id)&&right[m.Id].Team.HasValue&&m.Team==right[m.Id].Team).ToArray();
   if(shared.Length<3||shared.Select(m=>m.Start/86400).Distinct().Count()<2)return null;
   var historical=(a.Mates??new HistoricalMate[0]).FirstOrDefault(m=>m.Account==b.Account);var source=a;if(historical==null){historical=(b.Mates??new HistoricalMate[0]).FirstOrDefault(m=>m.Account==a.Account);source=b;}
   if(historical==null||source.MatesFetched>now||(now-source.MatesFetched).TotalMinutes>30||historical.MatchIds.Intersect(shared.Select(m=>m.Id)).Count()<3)return null;
   return a.Account+" + "+b.Account+": "+shared.Length+L.Choose(" recent same-team matches on "," недавних матчей в одной команде за ")+shared.Select(m=>m.Start/86400).Distinct().Count()+L.Choose(" days; API same_party corroborates historical friendship. These signals are correlated and cannot prove the current party."," дня; API same_party указывает на историческую дружбу. Признаки связаны и не доказывают текущую пати.");
  }
 }
 public sealed class ScoutDecoration {public int Hero;public PlayerInsight[] Tags=new PlayerInsight[0];public int Badge;public string RankPath="";public PartyInfo Party=PartyInfo.Unknown();public DateTime RankFetched;}
 public static class ScoutDecorations {
  static Dictionary<long,ScoutDecoration> snapshot=new Dictionary<long,ScoutDecoration>();
  public static int Revision{get;private set;}
  public static void Publish(Dictionary<long,ScoutDecoration> values){snapshot=values;Revision++;}
  public static ScoutDecoration Get(long account){ScoutDecoration d;return snapshot.TryGetValue(account,out d)?d:new ScoutDecoration();}
  public static bool VisibleParty(PartyInfo p,ScoutOptions o){return o.ShowParty&&p!=null&&(p.Confidence==PartyConfidence.Confirmed||p.Confidence==PartyConfidence.Likely&&o.ShowLikelyParty);}
 }
 public sealed class ScoutRankImages:IDisposable {
  readonly Dictionary<string,Image> images=new Dictionary<string,Image>();readonly Dictionary<string,DateTime> retry=new Dictionary<string,DateTime>();
  public Image Get(string path){if(string.IsNullOrEmpty(path))return null;Image value;if(images.TryGetValue(path,out value))return value;DateTime next;if(retry.TryGetValue(path,out next)&&DateTime.UtcNow<next)return null;retry[path]=DateTime.UtcNow.AddSeconds(5);try{if(System.IO.File.Exists(path))using(var raw=Image.FromFile(path)){value=new Bitmap(raw,48,48);images[path]=value;return value;}}catch{}return null;}
  public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();}
 }
}
