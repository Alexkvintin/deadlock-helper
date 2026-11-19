using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeadlockOverlay;

class ScoutSwitchTests {
 static int checks;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 static void Wait(Func<bool> condition,string message){Check(SpinWait.SpinUntil(condition,5000),message);}
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
 static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
 static object Call(object target,string name,params object[] values){return target.GetType().GetMethod(name,Private).Invoke(target,values);}
 static ScoutMatch History(long match,int hero){return new ScoutMatch{Id=match,Hero=hero,Mode=4,Outcome=1,Duration=1800,Start=1700000000};}
 static object Response(long match,bool partial=false,bool missingField=false){return Json.Serializer().DeserializeObject("{\"match_info\":{\"match_id\":"+match+",\"duration_s\":1800,\"players\":["+string.Join(",",Enumerable.Range(1,12).Select(i=>"{\"account_id\":"+i+",\"hero_id\":"+(i+10)+",\"stats\":[{\"time_stamp_s\":"+(partial?600:1800)+(missingField?"":",\"player_damage\":"+(i*1000))+",\"teammate_healing\":0}]}"))+"]}}");}
 static void SharedRequests(){
  var cache=new ScoutMetadataCache();int requests=0;bool transportCancelled=false;
  using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var firstCancel=new CancellationTokenSource()){
   Func<CancellationToken,object> fetch=token=>{Interlocked.Increment(ref requests);entered.Set();try{release.Wait(token);return Response(42);}catch(OperationCanceledException){transportCancelled=true;throw;}};
   var first=cache.ResolveAsync(42,1,11,fetch,firstCancel.Token);Check(entered.Wait(5000),"first shared request entered");
   var second=cache.ResolveAsync(42,2,12,fetch,CancellationToken.None);firstCancel.Cancel();Wait(()=>first.IsCompleted,"first consumer cancelled promptly");
   Check(first.IsCanceled&&!transportCancelled&&requests==1&&!second.IsCompleted,"one consumer cannot cancel the other's transport");
   release.Set();Check(second.Wait(5000)&&second.Result.Account==2&&second.Result.Damage==2000&&requests==1,"second consumer receives only its player data from one request");
   Check(cache.Resolve(42,1,11,fetch,CancellationToken.None).Damage==1000&&requests==1,"reopen uses completed shared cache");
  }
  using(var entered=new ManualResetEventSlim())using(var aborted=new ManualResetEventSlim())using(var a=new CancellationTokenSource())using(var b=new CancellationTokenSource()){
   Func<CancellationToken,object> fetch=token=>{entered.Set();try{token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();return null;}finally{aborted.Set();}};
   var first=cache.ResolveAsync(43,1,11,fetch,a.Token);Check(entered.Wait(5000),"cancellable transport entered");var second=cache.ResolveAsync(43,2,12,fetch,b.Token);a.Cancel();b.Cancel();
   Wait(()=>first.IsCompleted&&second.IsCompleted,"both consumer tasks cancelled");Check(first.IsCanceled&&second.IsCanceled&&aborted.Wait(5000),"last consumer cancels actual transport");
   Check(cache.Resolve(43,1,11,token=>Response(43),CancellationToken.None).Available,"cancelled flight does not poison retry");
  }
  int active=0,peak=0,queuedCalls=0;
  using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var cancel=new CancellationTokenSource()){
   Func<CancellationToken,object> blocked=token=>{var n=Interlocked.Increment(ref active);peak=Math.Max(peak,n);entered.Set();try{release.Wait(token);return Response(44);}finally{Interlocked.Decrement(ref active);}};
   var first=cache.ResolveAsync(44,1,11,blocked,CancellationToken.None);Check(entered.Wait(5000),"global transport occupied");
   var queued=cache.ResolveAsync(45,1,11,token=>{Interlocked.Increment(ref queuedCalls);return Response(45);},cancel.Token);cancel.Cancel();Wait(()=>queued.IsCompleted,"queued cancellation completes before occupied transport");release.Set();
   Check(first.Wait(5000)&&queued.IsCanceled&&queuedCalls==0&&peak==1,"cancelled queued work never calls API");
  }
  int failures=0;
  using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   Func<CancellationToken,object> fail=token=>{Interlocked.Increment(ref failures);entered.Set();release.Wait(token);throw new IOException("fixture API failure");};
   var a=cache.ResolveAsync(46,1,11,fail,CancellationToken.None);Check(entered.Wait(5000),"failure request entered");var b=cache.ResolveAsync(46,2,12,fail,CancellationToken.None);release.Set();Wait(()=>a.IsCompleted&&b.IsCompleted,"both error consumers terminate");
   Check(a.IsFaulted&&b.IsFaulted&&failures==1,"one failed transport shared by both consumers");var observed=a.Exception;observed=b.Exception;
   Check(cache.Resolve(46,1,11,token=>Response(46,true),CancellationToken.None).HasRecordedData,"failed request can retry with partial data");var partial=cache.Resolve(46,2,12,token=>{throw new Exception("duplicate partial request");},CancellationToken.None);Check(!partial.Available&&partial.Account==2&&!partial.Damage.HasValue,"partial shared data never presented as final totals");
  }
 }
 static void Switching(string folder){
  var rows=Enumerable.Range(1,12).Select(i=>new ScoutRow{Account=i,Hero=i+10,Name="Player "+i,Team=i<=6?2:3,Slot=i-1}).ToArray();int calls=0;bool transportCancelled=false;
  using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var roster=new ScoutService(folder))using(var detail=new ScoutDetailService(folder,new ScoutMetadataCache(),(url,token)=>{if(!url.EndsWith("/metadata"))return null;Interlocked.Increment(ref calls);entered.Set();try{release.Wait(token);return Response(42);}catch(OperationCanceledException){transportCancelled=true;throw;}},0))using(var view=new ScoutView{Service=roster,Rows=rows,Options=new ScoutOptions{Network=true}}){
   var profiles=(Dictionary<long,ScoutProfile>)Field(roster,"profiles");foreach(var row in rows)profiles[row.Account]=new ScoutProfile{Account=row.Account,Loaded=true,History=new[]{History(42,row.Hero)},Fetched=DateTime.UtcNow};
   Set(view,"details",detail);detail.Changed+=(Action)Delegate.CreateDelegate(typeof(Action),view,view.GetType().GetMethod("DetailChanged",Private));view.Size=new Size(1100,800);view.CreateControl();Set(view,"offset",23);
   view.OpenProfile(rows[0]);Check(entered.Wait(5000)&&detail.IsLoading(1),"first profile loading indicator active");var elapsed=Stopwatch.StartNew();
   foreach(var row in rows.Skip(1)){
    view.OpenProfile(row);Check((long)Field(view,"selected")==row.Account&&detail.IsLoading(row.Account),"immediate identity and loading for player "+row.Account);
    Check(Call(view,"Detail",42L)==null,"no previous detail shown for player "+row.Account);
    Check(((List<KeyValuePair<RectangleF,Action>>)Field(view,"profileActions")).Count==0,"old profile actions cleared");
    using(var bitmap=new Bitmap(view.Width,view.Height))view.DrawToBitmap(bitmap,new Rectangle(Point.Empty,view.Size));
   }
   Check(elapsed.ElapsedMilliseconds<3000,"12 profile renders responsive while transport is deliberately blocked");view.OpenProfile(rows[11]);Check(detail.IsLoading(12)&&!detail.IsLoading(1),"reopening active selection retains loading state");
   Check(!transportCancelled&&calls==1,"overlapping selection retained original shared request");release.Set();Wait(()=>!detail.IsLoading(12),"latest profile finished");Application.DoEvents();
   var shown=(ScoutMatchDetail)Call(view,"Detail",42L);Check(shown!=null&&shown.Account==12&&shown.Hero==22&&shown.Damage==12000&&calls==1,"latest profile only, one metadata download across all twelve");
   Check(detail.Get(1,42)==null,"late first selection never published into its profile after cancellation");
   Call(view,"BackToTeams");Check((int)Field(view,"offset")==23&&!detail.IsLoading(12)&&Call(view,"Detail",42L)==null,"back to team view restores scroll and clears detail context");
   view.OpenProfile(rows[11]);shown=(ScoutMatchDetail)Call(view,"Detail",42L);Check(shown!=null&&shown.Damage==12000,"cached detail immediately visible on reopening");Wait(()=>!detail.IsLoading(12),"cached reopen completes");Check(calls==1,"cached reopen does not request metadata again");
   foreach(var row in rows){view.OpenProfile(row);Wait(()=>!detail.IsLoading(row.Account),"sequential cached player "+row.Account+" completes");var result=(ScoutMatchDetail)Call(view,"Detail",42L);Check(result!=null&&result.Account==row.Account&&result.Hero==row.Hero&&result.Damage==row.Account*1000,"sequential player "+row.Account+" has independent totals");}
   Check(calls==1,"all twelve sequential profiles reuse the same metadata response");
   profiles[12]=new ScoutProfile{Account=12,Loaded=true,History=new[]{History(42,99)},Fetched=DateTime.UtcNow};view.RefreshProfile();Check(Call(view,"Detail",42L)==null,"changed hero identity invalidates previous detail");Wait(()=>!detail.IsLoading(12),"changed hero resolution concludes");Check(Call(view,"Detail",42L)==null||!((ScoutMatchDetail)Call(view,"Detail",42L)).Available,"mismatched hero does not borrow cached totals");
  }
 }
 static void UnrelatedSwitch(string folder){
  int calls=0;using(var entered=new ManualResetEventSlim())using(var aborted=new ManualResetEventSlim())using(var service=new ScoutDetailService(folder,new ScoutMetadataCache(),(url,token)=>{
   if(!url.EndsWith("/metadata"))return null;Interlocked.Increment(ref calls);if(url.Contains("/200/")){entered.Set();token.WaitHandle.WaitOne();aborted.Set();return Response(200);}return Response(201);
  },0)){
   service.Open(1,11,new[]{History(200,11)},true);Check(entered.Wait(5000),"unrelated first match entered");service.Open(2,12,new[]{History(201,12)},true);
   Check(aborted.Wait(5000),"unneeded match transport cancelled on switch");Wait(()=>!service.IsLoading(2),"unrelated next profile completes");
   var selected=service.Get(2,201);Check(service.Get(1,200)==null&&selected!=null&&selected.Damage==2000&&calls==2,"late response ignored, unrelated next match loaded exactly once");
  }
 }
 static void Preserve(string folder){
  foreach(bool partial in new[]{false,true}){
   var match=partial?101:100;var path=Path.Combine(folder,"1-"+match+".json");var prior=ScoutMatchDetail.Parse(Response(match),match,1);prior.Fetched=DateTime.UtcNow.AddDays(-9);File.WriteAllText(path,Json.Serializer().Serialize(prior));var before=File.ReadAllText(path);int calls=0;
   using(var service=new ScoutDetailService(folder,new ScoutMetadataCache(),(url,token)=>{if(!url.EndsWith("/metadata"))return null;Interlocked.Increment(ref calls);if(!partial)throw new IOException("fixture API unavailable");return Response(match,false,true);},0)){
    service.Open(1,11,new[]{History(match,11)},true);Wait(()=>!service.IsLoading(1),"refresh completes on "+(partial?"incomplete final fields":"API error"));var detail=service.Get(1,match);
    Check(detail!=null&&detail.Available&&detail.Damage==1000&&File.ReadAllText(path)==before,"good disk cache survives "+(partial?"partial response":"API error"));
    service.Open(1,11,new[]{History(match,11)},true);Wait(()=>!service.IsLoading(1),"cached fallback reopen completes");Check(calls==1&&service.Get(1,match).Damage==1000,"failed refresh not repeated immediately");
   }
  }
 }
 [STAThread] static void Main(){UiReview.Active=true;L.Language="en";Application.EnableVisualStyles();SharedRequests();string folder=Path.Combine(Path.GetTempPath(),"DeadlockScoutSwitch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{Switching(folder);UnrelatedSwitch(folder);Preserve(folder);}finally{Directory.Delete(folder,true);}Console.WriteLine("Scout switching checks passed: "+checks);}
}
