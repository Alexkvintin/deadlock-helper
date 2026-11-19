using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using DeadlockOverlay;
class ScoutProfileTests {
 static int count;static void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
 static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Set(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
 static void Call(object target,string name,params object[] args){target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 [STAThread] static void Main(){Application.EnableVisualStyles();UiReview.Active=true;
  foreach(string lang in new[]{"en","ru"}){L.Language=lang;foreach(string key in new[]{"main","smurf","streak","leaver","new","low","high","party","likely"}){string tip=ScoutProfileData.Tip(key);Check(tip.Length>5&&tip.Length<120&&!tip.Contains("\n"),"short standalone tooltip "+lang+" "+key);}Check(ScoutProfileData.N(null)==ScoutProfileData.Missing,"missing number "+lang);Check(ScoutProfileData.Rate(new ScoutMatch[0])==ScoutProfileData.Missing,"missing win rate "+lang);}
  L.Language="ru";Check(ScoutProfileData.N(42600)=="42,6k","Russian compact format");L.Language="en";Check(ScoutProfileData.N(42600)=="42.6k","English compact format");
  var m=new ScoutMatch{Id=1,Hero=1,Duration=1800,Mode=4,Outcome=1,Kills=10,Deaths=0,Assists=6};Check(ScoutProfileData.Ratio(m)==16,"zero deaths ratio is finite");m.Assists=null;Check(!ScoutProfileData.Ratio(m).HasValue,"missing assist breaks ratio");Check(!ScoutProfileData.Metric(m,null,1).HasValue,"missing souls gap");Check(!ScoutProfileData.Metric(m,null,4).HasValue,"missing metadata gap");m.Outcome=3;Check(!ScoutProfileData.Metric(m,null,2).HasValue,"penalty is not defeat");m.Badge=0;Check(!ScoutProfileData.Metric(m,null,3).HasValue,"unranked is not a zero rank point");
  Check(ScoutProfileData.RankStep(21)-ScoutProfileData.RankStep(16)==1,"adjacent tiers have one division step");Check(!ScoutProfileData.RankStep(17).HasValue,"invalid division is missing");Check(ScoutProfileData.StepBadge(ScoutProfileData.RankStep(106).Value)==106,"rank round trip");
  var json=Json.Serializer();json.MaxJsonLength=24000000;json.RecursionLimit=100;
  object raw=json.DeserializeObject("{\"match_info\":{\"match_id\":3,\"duration_s\":1800,\"players\":[{\"account_id\":7,\"hero_id\":1,\"stats\":[{\"time_stamp_s\":1800,\"player_damage\":0,\"boss_damage\":500},{\"time_stamp_s\":900,\"player_damage\":200}]}]}}");
  var d=ScoutMatchDetail.Parse(raw,3,7);Check(d.Available&&d.Damage==0&&d.Objectives==500,"actual zero and latest timestamp preserved");Check(!d.Healing.HasValue,"missing healing remains null");Check(!ScoutMatchDetail.Parse(raw,4,7).Available,"wrong match rejected");Check(!ScoutMatchDetail.Parse(raw,3,8).Available,"wrong account rejected");
  Check(d.PayloadVersion==ScoutMatchDetail.CurrentVersion&&d.Timeline.Length==2&&d.Timeline[0].Second==900,"review timeline chronological and versioned");
  Check(d.Timeline[1].Damage==0&&!d.Timeline[1].Healing.HasValue,"timeline zero preserved and ally healing not invented");
  Check(d.Purchases.Length==0,"missing purchase events stay empty");
  var purchases=json.DeserializeObject("{\"match_info\":{\"match_id\":3,\"duration_s\":1800,\"players\":[{\"account_id\":7,\"hero_id\":1,\"items\":[{\"game_time_s\":0,\"item_id\":4294967290},{\"game_time_s\":400,\"item_id\":12,\"sold_time_s\":900},{\"game_time_s\":1900,\"item_id\":13}],\"stats\":[{\"time_stamp_s\":1800}]}]}}");
  var pd=ScoutMatchDetail.Parse(purchases,3,7);Check(pd.Purchases.Length==2&&pd.Purchases[0].Second==0&&pd.Purchases[0].Item==4294967290L,"purchase time zero and unsigned item ID preserved; future excluded");Check(pd.Purchases[1].Sold==900,"recorded sale time retained");
  raw=json.DeserializeObject("{\"match_info\":{\"match_id\":3,\"duration_s\":1800,\"players\":[{\"account_id\":7,\"hero_id\":1,\"stats\":[{\"time_stamp_s\":600,\"player_damage\":200}]}]}}");Check(!ScoutMatchDetail.Parse(raw,3,7).Available,"early sample not final total");
  if(File.Exists("artifacts/scout/metadata.json")){var real=json.DeserializeObject(File.ReadAllText("artifacts/scout/metadata.json"));var entry=ScoutMatchDetail.Parse(real,134924960,909224572);Check(entry.Available&&entry.Damage==36336&&entry.Healing==11748&&entry.TeamHealing==0,"real metadata matches observed totals");}
  string cache=Path.Combine(Path.GetTempPath(),"DeadlockHelper-ProfileTests-"+Guid.NewGuid());Directory.CreateDirectory(cache);using(var detail=new ScoutDetailService(cache)){detail.Open(7,1,new[]{m},true);detail.Stop();Thread.Sleep(450);Check(detail.Requests==0,"cancelled selection never requests network");detail.Open(7,1,new[]{m},false);Thread.Sleep(450);Check(detail.Requests==0,"offline profile never requests network");}
  var history=Enumerable.Range(1,20).Select(i=>new ScoutMatch{Id=i,Start=1700000000+i,Hero=1,Duration=1800,Mode=4,Outcome=i%2==0?1:2,Kills=5,Deaths=3,Assists=7,Souls=24000}).ToArray();var p=new ScoutProfile{Account=7,Loaded=true,History=history,Fetched=DateTime.UtcNow};Check(ScoutProfileData.PlayStyle(history,id=>null).Contains("20"),"style has sample size");Check(!ScoutProfileData.PlayStyle(history,id=>null).Contains("Aggressive"),"no personality inference");
  using(var service=new ScoutService(cache))using(var view=new ScoutView{Service=service,Rows=new[]{new ScoutRow{Account=7,Name="Test player",Hero=1,Team=2}},Options=new ScoutOptions{Network=false}}){((Dictionary<long,ScoutProfile>)Field(service,"profiles"))[7]=p;
   view.Size=new Size(1100,900);view.CreateControl();Set(view,"offset",70);view.OpenProfile(view.Rows[0]);Check((int)Field(view,"teamOffset")==70,"team scroll retained on open");Call(view,"BackToTeams");Check((int)Field(view,"offset")==70,"team scroll restored on return");
   foreach(int dpi in new[]{96,120,144,168,192})foreach(var size in new[]{new Size(1280,720),new Size(1366,768),new Size(1600,900),new Size(1920,1080),new Size(2560,1440)}){
    view.SetReviewDpi(dpi);view.Size=new Size(size.Width-(int)(185*dpi/96f),size.Height-50);view.OpenProfile(view.Rows[0]);foreach(int tab in new[]{0,1,2}){Set(view,"profileTab",tab);using(var bmp=new Bitmap(view.Width,view.Height))view.DrawToBitmap(bmp,new Rectangle(Point.Empty,view.Size));Check((int)Field(view,"extent")>=0,"responsive profile "+dpi+" "+size+" "+tab);}
   }
   view.SetReviewDpi(96);view.Size=new Size(1100,900);Call(view,"BackToTeams");Set(view,"offset",0);using(var bmp=new Bitmap(view.Width,view.Height))view.DrawToBitmap(bmp,new Rectangle(Point.Empty,view.Size));var tips=(List<KeyValuePair<RectangleF,string>>)Field(view,"tips");Check(tips.Any(t=>t.Value==ScoutProfileData.Tip("main")),"crown has own short tooltip");Check(tips.All(t=>!t.Value.Contains("Updated:")),"no technical card tooltip");var mainTip=tips.First(t=>t.Value==ScoutProfileData.Tip("main"));Call(view,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,(int)mainTip.Key.X+2,(int)mainTip.Key.Y+2,0));Check((string)Field(view,"tip")==ScoutProfileData.Tip("main"),"main hit target only sets main text");Call(view,"OnMouseLeave",EventArgs.Empty);Check(!((ToolTip)Field(view,"tooltip")).Active&&(string)Field(view,"tip")=="","leaving disables pending tooltips");
  }
  Console.WriteLine("Scout profile checks passed: "+count);
 }
}
