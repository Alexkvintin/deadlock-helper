using System;
using System.Collections.Generic;
using System.Linq;
namespace DeadlockOverlay {
 public sealed class ScoutOptions {
  public bool Enabled=true,Network=true,AutoOpen=true,ShowParty=true,ShowLikelyParty=true,OverlayParty=true;
  public int PartyIntensity=65;
  public string Monitor="";
  public int Sample=20,SmurfPercent=70,Leaves=3,LowPercentile=25,LowGames=12;
  public string DisabledTags="";
  public void Validate(){Sample=Math.Max(20,Math.Min(30,Sample));SmurfPercent=Math.Max(50,Math.Min(99,SmurfPercent));Leaves=Math.Max(1,Math.Min(20,Leaves));LowPercentile=Math.Max(1,Math.Min(40,LowPercentile));LowGames=Math.Max(10,Math.Min(20,LowGames));PartyIntensity=Math.Max(15,Math.Min(100,PartyIntensity));}
 }
 public enum ScoutEvidence { Unavailable,Observed,Calculated,Inferred }
 public sealed class ScoutMatch {
  public long Id,Start;public int Hero,Mode,GameMode=1;public int? Outcome,Duration,Kills,Deaths,Assists,Badge,Team,Result;public long? Souls;public int? Abandoned,Delta;
  public bool Completed{get{return Id>0&&Duration>0&&(Outcome>=1&&Outcome<=4||!Outcome.HasValue&&Result>=0&&Result<=1&&Team>=0&&Team<=1);}}
  public bool? Won{get{if(Outcome.HasValue)return Outcome==1?(bool?)true:Outcome==2?(bool?)false:null;return Result.HasValue&&Team.HasValue&&Result>=0&&Result<=1&&Team>=0&&Team<=1?(bool?)(Result==Team):null;}}
  public static ScoutMatch Parse(object value){var d=Json.Obj(value);return new ScoutMatch{Id=Json.Number(d,"match_id")??0,Start=Json.Number(d,"start_time")??0,Hero=(int)(Json.Number(d,"hero_id")??0),Mode=(int)(Json.Number(d,"match_mode")??0),GameMode=(int)(Json.Number(d,"game_mode")??0),Outcome=(int?)Json.Number(d,"player_match_outcome"),Duration=(int?)Json.Number(d,"match_duration_s"),Kills=(int?)Json.Number(d,"player_kills"),Deaths=(int?)Json.Number(d,"player_deaths"),Assists=(int?)Json.Number(d,"player_assists"),Souls=Json.Number(d,"net_worth"),Badge=(int?)Json.Number(d,"ranked_display_badge"),Team=(int?)Json.Number(d,"player_team"),Result=(int?)Json.Number(d,"match_result"),Abandoned=(int?)Json.Number(d,"abandoned_time_s"),Delta=Signed(d,"ranked_delta")};}
  static int? Signed(Dictionary<string,object>d,string k){int n;return int.TryParse(Json.Str(d,k),out n)?(int?)n:null;}
 }
 public sealed class ScoutProfile {
  public int Schema=2,RawHistoryCount;public long Account;public int Badge;public ScoutMatch[] History=new ScoutMatch[0];public DateTime Fetched,RankFetched;public long RankMatch,RankTime;public string Error="";public bool Loaded;
  public Dictionary<string,Dictionary<string,object>> Heroes=new Dictionary<string,Dictionary<string,object>>();
  public HistoricalMate[] Mates=new HistoricalMate[0];public DateTime MatesFetched;
  public ScoutProfile Copy(){return (ScoutProfile)MemberwiseClone();}
 }
 public sealed class ScoutRow {public long Account;public int Team,Hero,Slot;public string Name="";}
 public sealed class PlayerInsight {public string Key,Label,Reason;public ScoutEvidence Evidence=ScoutEvidence.Calculated;}
 public static class ScoutStatistics {
  public static ScoutMatch[] History(ScoutProfile p){return (p.History??new ScoutMatch[0]).Where(m=>m!=null&&m.Completed&&m.GameMode==1&&(m.Mode==1||m.Mode==4)).OrderByDescending(m=>m.Start).ThenByDescending(m=>m.Id).GroupBy(m=>m.Id).Select(g=>g.First()).ToArray();}
  public static string Value(object v){return v==null?"—":Convert.ToString(v,System.Globalization.CultureInfo.CurrentCulture);}
  public static string Rate(IEnumerable<ScoutMatch> matches){var a=matches.Where(m=>m.Completed&&m.Won.HasValue).GroupBy(m=>m.Id).Select(g=>g.First()).ToArray();return a.Length==0?"—":(100.0*a.Count(m=>m.Won==true)/a.Length).ToString("0")+"% · n="+a.Length;}
  public static int MainHero(ScoutProfile p){var a=History(p).Take(20).Where(m=>m.Hero>0).GroupBy(m=>m.Hero).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).FirstOrDefault();return a==null?0:a.Key;}
 }
 public static class PlayerInsightEngine {
  public static PlayerInsight[] Ordered(IEnumerable<PlayerInsight> tags){string[] order={"main","new","high","low","streak","smurf","leaver"};return tags.OrderBy(t=>{int i=Array.IndexOf(order,t.Key);return i<0?100:i;}).ToArray();}
  public static PlayerInsight[] Analyze(ScoutProfile p,int hero,ScoutOptions o){
   var tags=new List<PlayerInsight>();var h=ScoutStatistics.History(p);
   Action<string,string,string,string> add=(key,en,ru,reason)=>{if(!(o.DisabledTags??"").Split(',').Contains(key))tags.Add(new PlayerInsight{Key=key,Label=L.Choose(en,ru),Reason=reason});};
   var ranked=h.Where(m=>m.Mode==4).Take(o.Sample).ToArray();var scored=ranked.Where(m=>m.Won.HasValue).ToArray();
   if(scored.Length>=20&&100.0*scored.Count(m=>m.Won==true)/scored.Length>o.SmurfPercent)add("smurf","SMURF","СМУРФ",L.Choose("Statistical indicator, not proof of an alternate account. Ranked WR: ","Статистический индикатор, не доказательство второго аккаунта. Рейтинговый WR: ")+ScoutStatistics.Rate(scored));
   var recent=h.Take(20).ToArray();int leaves=recent.Count(m=>m.Abandoned>0);
   if(leaves>=o.Leaves)add("leaver","LEAVER","РУИНЕР",L.Choose("Confirmed early departures: ","Подтверждённые досрочные выходы: ")+leaves+"/"+recent.Length);
   if(recent.Length==20){var main=recent.Where(m=>m.Hero>0).GroupBy(m=>m.Hero).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).FirstOrDefault();if(main!=null&&hero>0&&main.Key==hero&&main.Count()>=14)add("main","MAIN","МЕЙНЕР",LocalSnapshot.HeroName(main.Key)+" · "+main.Count()+"/20");}
   if(p.Loaded&&h.Length>0&&hero>0&&h.All(m=>m.Hero>0)&&!h.Any(m=>m.Hero==hero))add("new","NEW TO HERO","НОВИЧОК",L.Choose("No games on this hero in the available normal-mode history, not lifetime. Sample: ","Нет игр на герое в доступной истории обычного режима, не за всю карьеру. Выборка: ")+h.Length);
   int wins=h.TakeWhile(m=>m.Won==true).Count();if(wins>=6)add("streak","WIN STREAK ×"+wins,"ВИНСТРИК ×"+wins,L.Choose("Consecutive normal-mode wins (ranked and unranked): ","Победы подряд в обычном режиме (рейтинг и нерейтинг): ")+wins);
   return tags.ToArray();
  }
  public static long AccountId(string steam){ulong n;if(!ulong.TryParse(steam,out n))return 0;const ulong baseId=76561197960265728UL;if(n>baseId)n-=baseId;return n>0&&n<=uint.MaxValue?(long)n:0;}
  public static string Rate(IEnumerable<ScoutMatch> matches){return ScoutStatistics.Rate(matches);}
 }
}
