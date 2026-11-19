using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DeadlockOverlay {
 // Metadata contains an entire match, so one response can serve several selected profiles.
 // Keep only bounded, condensed statistics in memory; the existing per-profile disk cache is unchanged.
 public sealed class ScoutMetadataCache {
  sealed class Entry {
   public DateTime Expires,Used;
   public Dictionary<long,ScoutMatchDetail> Players;
  }
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  readonly Dictionary<long,Entry> entries=new Dictionary<long,Entry>();
  readonly int capacity;
  public ScoutMetadataCache(int capacity=16){if(capacity<1)throw new ArgumentOutOfRangeException("capacity");this.capacity=capacity;}

  public ScoutMatchDetail Resolve(long match,long account,int hero,Func<object> fetch,CancellationToken token){
   if(match<=0||account<=0||hero<=0)return null;
   gate.Wait(token);
   try{
    token.ThrowIfCancellationRequested();var now=DateTime.UtcNow;Entry entry;
    foreach(var expired in entries.Where(p=>p.Value.Expires<=now).Select(p=>p.Key).ToArray())entries.Remove(expired);
    if(!entries.TryGetValue(match,out entry)){
     var raw=fetch();token.ThrowIfCancellationRequested();var info=Json.Obj(Json.Get(Json.Obj(raw),"match_info"));
     if(Json.Number(info,"match_id")!=match)return null;
     var players=(Json.Get(info,"players") as object[]??new object[0]).Select(Json.Obj).ToArray();
     // Reject ambiguous accounts rather than attaching one player's stats to another.
     var ids=players.Select(p=>Json.Number(p,"account_id")).Where(id=>id>0).Select(id=>id.Value).GroupBy(id=>id).Where(g=>g.Count()==1).Select(g=>g.Key).Take(24);
     var details=new Dictionary<long,ScoutMatchDetail>();
     foreach(var id in ids){token.ThrowIfCancellationRequested();var detail=ScoutMatchDetail.Parse(raw,match,id);if(detail.PayloadVersion==ScoutMatchDetail.CurrentVersion&&(detail.Available||detail.HasRecordedData))details[id]=detail;}
     if(details.Count==0)return null;
     now=DateTime.UtcNow;
     entry=new Entry{Players=details,Used=now,Expires=now.AddMinutes(details.Values.All(d=>d.Available)?15:1)};
     while(entries.Count>=capacity)entries.Remove(entries.OrderBy(p=>p.Value.Used).First().Key);
     entries[match]=entry;
    }
    entry.Used=DateTime.UtcNow;ScoutMatchDetail selected;
    return entry.Players.TryGetValue(account,out selected)&&selected.Hero==hero?selected:null;
   }finally{gate.Release();}
  }
 }
}
