using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using DeadlockOverlay;

class ReviewDataIntegrityTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static ScoutMatchDetail Parse(string player,string duration="1800",string other=""){
  return ScoutMatchDetail.Parse(Json.Serializer().DeserializeObject("{\"match_info\":{\"match_id\":42,\"duration_s\":"+duration+",\"players\":[{\"account_id\":7,\"hero_id\":15,"+player+"}"+other+"]}}"),42,7);
 }
 [STAThread] static void Main(){
  L.Language="en";UiReview.Active=true;Application.EnableVisualStyles();
  var d=Parse("\"stats\":[{\"time_stamp_s\":1800,\"player_damage\":10},{\"time_stamp_s\":1800,\"player_damage\":20,\"teammate_healing\":0},{\"time_stamp_s\":999999999999,\"player_damage\":999},{\"time_stamp_s\":900,\"player_damage\":5}]");
  Check(d.Available&&d.Damage==20&&d.TeamHealing==0,"invalid timestamp cannot mask valid final; duplicate snapshot uses same final row");
  Check(d.PayloadVersion==ScoutMatchDetail.CurrentVersion&&d.Timeline.Length==2&&d.Timeline.Last().Damage==d.Damage,"chart and final totals agree");
  Check(!d.Healing.HasValue,"missing is distinct from zero");
  Check(!Parse("\"stats\":[]","999999999999").Available,"out-of-range duration rejected");
  var partial=Parse("\"stats\":[{\"time_stamp_s\":120,\"player_damage\":42617},{\"time_stamp_s\":900,\"player_damage\":42611}]");
  Check(!partial.Available&&partial.HasRecordedData&&!partial.Damage.HasValue,"partial history never becomes final total");
  Check(ReviewAnalysis.Progress(partial).Contains("Partial history")&&ReviewAnalysis.Progress(partial).Contains("changed from"),"partial data and corrected downward values described accurately");
  var items=Parse("\"items\":[{\"item_id\":4294967295,\"game_time_s\":0,\"sold_time_s\":0},{\"item_id\":2,\"game_time_s\":100,\"sold_time_s\":99},{\"item_id\":3,\"game_time_s\":100,\"sold_time_s\":1810},{\"item_id\":4,\"game_time_s\":100,\"sold_time_s\":900,\"upgrade_id\":1234},{\"item_id\":4294967296,\"game_time_s\":100}]");
  Check(items.Purchases.Length==4&&items.Purchases[0].Item==uint.MaxValue,"unsigned item ID and time zero; invalid ID rejected");
  Check(items.Purchases.Take(3).All(p=>!p.Sold.HasValue)&&items.Purchases[3].Sold==900,"zero, pre-purchase and post-match sales rejected");
  Check(items.Purchases[3].UpgradeId==1234&&!items.Available&&items.HasRecordedData,"raw upgrade retained without claiming final inventory");
  var deaths=Parse("\"death_details\":[{\"game_time_s\":1},{\"game_time_s\":2,\"killer_player_slot\":0},{\"game_time_s\":3,\"killer_player_slot\":9}]","1800",",{\"account_id\":8,\"player_slot\":0,\"hero_id\":21},{\"account_id\":9,\"player_slot\":9,\"hero_id\":22},{\"account_id\":10,\"player_slot\":9,\"hero_id\":23}");
  Check(deaths.Events.Select(e=>e.Hero).SequenceEqual(new[]{0,21,0}),"missing, zero and ambiguous killer slots distinguished");
  Check(!Parse("\"stats\":[]","1800",",{\"account_id\":7,\"hero_id\":21}").HasRecordedData,"ambiguous account rejected");
  var metadata=new ScoutMetadataCache(1);int fetched=0;
  Func<long,object> response=id=>Json.Serializer().DeserializeObject("{\"match_info\":{\"match_id\":"+id+",\"duration_s\":1800,\"players\":[{\"account_id\":7,\"hero_id\":15,\"stats\":[{\"time_stamp_s\":1800,\"player_damage\":10}]},{\"account_id\":8,\"hero_id\":21,\"stats\":[{\"time_stamp_s\":1800,\"player_damage\":30}]}]}}");
  Func<long,Func<object>> fetch=id=>()=>{Interlocked.Increment(ref fetched);return response(id);};
  var first=System.Threading.Tasks.Task.Run(()=>metadata.Resolve(42,7,15,fetch(42),CancellationToken.None));
  var second=System.Threading.Tasks.Task.Run(()=>metadata.Resolve(42,8,21,fetch(42),CancellationToken.None));
  System.Threading.Tasks.Task.WaitAll(first,second);
  Check(fetched==1&&first.Result.Damage==10&&second.Result.Damage==30,"concurrent selections share one match request and retain correct account statistics");
  Check(metadata.Resolve(42,8,15,fetch(42),CancellationToken.None)==null&&fetched==1,"wrong hero never inherits another profile");
  Check(metadata.Resolve(43,7,15,fetch(42),CancellationToken.None)==null,"unrelated match response rejected");
  Check(metadata.Resolve(43,7,15,fetch(43),CancellationToken.None).Damage==10,"wrong response not cached");
  Check(metadata.Resolve(42,7,15,fetch(42),CancellationToken.None).Damage==10&&fetched==4,"bounded cache evicts old entries");
  using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool cancelled=false;try{metadata.Resolve(44,7,15,fetch(44),cancellation.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&fetched==4,"cancelled request never fetches metadata");}
  // A historical cache remains useful offline even past its online refresh interval.
  string folder=Path.Combine(Path.GetTempPath(),"DeadlockReviewIntegrity-"+Guid.NewGuid());Directory.CreateDirectory(folder);
  try{
   d.Fetched=DateTime.UtcNow.AddDays(-30);File.WriteAllText(Path.Combine(folder,"7-42.json"),Json.Serializer().Serialize(d));
   using(var ready=new ManualResetEventSlim())using(var service=new ScoutDetailService(folder)){
    service.Changed+=()=>ready.Set();service.Open(7,15,new[]{new ScoutMatch{Id=42,Hero=15}},false);
    Check(ready.Wait(5000)&&service.Get(7,42)!=null&&service.Get(7,42).Damage==20,"aged historical result available offline");Check(service.Requests==0,"offline cache does not request network");
   }
   var players=Enumerable.Range(0,12).Select(i=>new Player{Slot=i,Team=i<6?2:3,Hero=15,Name="Player "+i,HeroName="Bebop",Damage=1000,ObjectDamage=1,Healing=1,Souls=1000,Kills=1,Deaths=1,Assists=1}).ToArray();
   var summary=new MatchSummary{MatchId="42",Players=players,DurationSeconds=1800,WinningTeam=2};var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   int syntheticHero=int.MaxValue;while(File.Exists(AssetLibrary.Resolve(syntheticHero+".png")))syntheticHero--;
   // Use a unique synthetic portrait in the runtime cache, never copyrighted game art.
   foreach(var p in players)p.Hero=syntheticHero;players[0].Name="<test & player>";
   string html=summary.Html();Check(html.Contains("class='portrait'")&&html.Contains("<svg")&&!html.Contains("<table")&&html.Contains("&lt;test &amp; player&gt;"),"offline HTML placeholders, charts and escaping");
   Directory.CreateDirectory(AssetLibrary.Folder);string icon=Path.Combine(AssetLibrary.Folder,syntheticHero+".png");bool created=false;
   try{
    using(var bmp=new Bitmap(8,8))using(var file=new FileStream(icon,FileMode.CreateNew,FileAccess.Write)){created=true;bmp.SetPixel(0,0,Color.Teal);bmp.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
    Check(summary.Html().Contains("data:image/png;base64,"),"HTML embeds a valid runtime-cached portrait");
    File.WriteAllText(icon,"not a portrait");Check(!summary.Html().Contains("data:image/png;base64,")&&summary.Html().Contains("class='portrait'"),"corrupt portrait falls back without losing report");
   }finally{if(created&&File.Exists(icon))File.Delete(icon);}
   players[0].Team=3;Check(summary.Html().Contains("Hidden King · —"),"malformed roster cannot present complete team totals");players[0].Team=2;
   foreach(var p in players)p.Hero=15;
   using(var service=new ScoutDetailService(folder))using(var view=new PlayerDetailCanvas(summary,players[0])){
    ((Dictionary<string,ScoutMatchDetail>)typeof(ScoutDetailService).GetField("matches",flags).GetValue(service))["7-42"]=partial;
    typeof(PlayerDetailCanvas).GetField("recorded",flags).SetValue(view,service);typeof(PlayerDetailCanvas).GetField("recordedAccount",flags).SetValue(view,7L);typeof(PlayerDetailCanvas).GetField("recordedMatch",flags).SetValue(view,42L);
    view.Size=new Size(1100,900);view.SetReviewDpi(96);view.CreateControl();typeof(PlayerDetailCanvas).GetField("selected",flags).SetValue(view,1);
    using(var bmp=new Bitmap(view.Width,view.Height))view.DrawToBitmap(bmp,new Rectangle(Point.Empty,view.Size));
    var tips=(List<KeyValuePair<RectangleF,string>>)typeof(PlayerDetailCanvas).GetField("tips",flags).GetValue(view);
    Check(tips.Count(t=>t.Value.Contains("Hero damage · "))==2,"partial chart renders real points, not estimates");
    Check(tips.Any(t=>t.Value=="Hero damage · 2:00 · 42,617"),"snapshot tooltip reports exact unrounded value");
   }
  }finally{Directory.Delete(folder,true);}
  Console.WriteLine("Review data integrity checks passed: "+checks);
 }
}
