using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeadlockOverlay {
 // Match metadata has one roster. Consumers share the transport, not their cancellation tokens.
 public sealed class ScoutMetadataCache {
  sealed class Entry {
   public DateTime Expires,Used;
   public Dictionary<long,ScoutMatchDetail> Players;
  }
  sealed class Flight {
   public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
   public Task<Entry> Work;
   public int Waiters;
   public bool Completed,Abandoned;
  }
  readonly object gate=new object();
  readonly SemaphoreSlim transport=new SemaphoreSlim(1,1);
  readonly Dictionary<long,Entry> entries=new Dictionary<long,Entry>();
  readonly Dictionary<long,Flight> flights=new Dictionary<long,Flight>();
  readonly int capacity;
  public ScoutMetadataCache(int capacity=16){if(capacity<1)throw new ArgumentOutOfRangeException("capacity");this.capacity=capacity;}

  static ScoutMatchDetail Select(Entry entry,long account,int hero){
   ScoutMatchDetail detail;
   return entry!=null&&entry.Players.TryGetValue(account,out detail)&&detail.Hero==hero?detail:null;
  }
  public ScoutMatchDetail Resolve(long match,long account,int hero,Func<CancellationToken,object> fetch,CancellationToken token){
   return ResolveAsync(match,account,hero,fetch,token).GetAwaiter().GetResult();
  }
  public async Task<ScoutMatchDetail> ResolveAsync(long match,long account,int hero,Func<CancellationToken,object> fetch,CancellationToken token){
   token.ThrowIfCancellationRequested();if(match<=0||account<=0||hero<=0)return null;
   Flight flight;
   lock(gate){
    token.ThrowIfCancellationRequested();var now=DateTime.UtcNow;Entry cached;
    foreach(var expired in entries.Where(p=>p.Value.Expires<=now).Select(p=>p.Key).ToArray())entries.Remove(expired);
    if(entries.TryGetValue(match,out cached)){cached.Used=now;return Select(cached,account,hero);}
    if(!flights.TryGetValue(match,out flight)){
     flight=new Flight();flights[match]=flight;var created=flight;
     flight.Work=Task.Run(()=>FetchMatch(match,created,fetch));
    }
    flight.Waiters++;
   }
   return await AwaitFlight(match,account,hero,flight,token).ConfigureAwait(false);
  }
  // Transfer a consumer to a new selection without starting a request on a cache miss.
  public Task<ScoutMatchDetail> JoinExisting(long match,long account,int hero,CancellationToken token){
   Flight flight;
   lock(gate){Entry entry;if(entries.TryGetValue(match,out entry)&&entry.Expires>DateTime.UtcNow){entry.Used=DateTime.UtcNow;return Task.FromResult(Select(entry,account,hero));}
    if(!flights.TryGetValue(match,out flight))return null;flight.Waiters++;
   }
   return AwaitFlight(match,account,hero,flight,token);
  }
  async Task<ScoutMatchDetail> AwaitFlight(long match,long account,int hero,Flight flight,CancellationToken token){
   try{
    var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using(token.Register(()=>cancelled.TrySetResult(true))){
     await Task.WhenAny(flight.Work,cancelled.Task).ConfigureAwait(false);
     token.ThrowIfCancellationRequested();
     return Select(await flight.Work.ConfigureAwait(false),account,hero);
    }
   }finally{Release(match,flight);}
  }
  void Release(long match,Flight flight){
   bool cancel=false;
   lock(gate){
    if(--flight.Waiters==0&&!flight.Completed){
     flight.Abandoned=true;Flight current;if(flights.TryGetValue(match,out current)&&ReferenceEquals(current,flight))flights.Remove(match);
     cancel=true;
    }
   }
   if(cancel){
    try{flight.Cancel.Cancel();}finally{flight.Work.ContinueWith(task=>{if(task.IsFaulted){var ignored=task.Exception;}flight.Cancel.Dispose();},TaskScheduler.Default);}
   }
  }
  Entry FetchMatch(long match,Flight flight,Func<CancellationToken,object> fetch){
   var token=flight.Cancel.Token;Entry entry=null;
   try{
    transport.Wait(token);
    try{
     token.ThrowIfCancellationRequested();var raw=fetch(token);token.ThrowIfCancellationRequested();var info=Json.Obj(Json.Get(Json.Obj(raw),"match_info"));
     if(Json.Number(info,"match_id")!=match)return null;
     var players=(Json.Get(info,"players") as object[]??new object[0]).Select(Json.Obj).ToArray();
     // Reject ambiguous accounts rather than attaching one player's stats to another.
     var ids=players.Select(p=>Json.Number(p,"account_id")).Where(id=>id>0).Select(id=>id.Value).GroupBy(id=>id).Where(g=>g.Count()==1).Select(g=>g.Key).Take(24);
     var details=new Dictionary<long,ScoutMatchDetail>();
     foreach(var id in ids){token.ThrowIfCancellationRequested();var detail=ScoutMatchDetail.Parse(raw,match,id);if(detail.PayloadVersion==ScoutMatchDetail.CurrentVersion&&(detail.Available||detail.HasRecordedData))details[id]=detail;}
     if(details.Count==0)return null;
     var now=DateTime.UtcNow;
     entry=new Entry{Players=details,Used=now,Expires=now.AddMinutes(details.Values.All(d=>d.Available)?15:1)};
     return entry;
    }finally{transport.Release();}
   }finally{
    bool dispose;
    lock(gate){
     flight.Completed=true;Flight current;if(flights.TryGetValue(match,out current)&&ReferenceEquals(current,flight))flights.Remove(match);
     if(!flight.Abandoned&&entry!=null){while(entries.Count>=capacity)entries.Remove(entries.OrderBy(p=>p.Value.Used).First().Key);entries[match]=entry;}
     dispose=!flight.Abandoned;
    }
    if(dispose)flight.Cancel.Dispose();
   }
  }
 }
}
