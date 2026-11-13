using System;
using System.Linq;
using System.Collections.Generic;
namespace DeadlockOverlay {
 public sealed class PerformanceMatch {
  public long MatchId,Account;public int Hero,Rank,Mode,Version;public DateTime Played;public double Minutes;
  public double? Kills,Deaths,Assists,HeroDamage,ObjectiveDamage,Support,Economy;
  public double[] Metrics(){if(Minutes<=0||!Kills.HasValue||!Deaths.HasValue||!Assists.HasValue||!HeroDamage.HasValue||!ObjectiveDamage.HasValue||!Support.HasValue||!Economy.HasValue)return null;
   var values=new[]{(Kills.Value+Assists.Value)/Math.Max(1,Deaths.Value),-Deaths.Value/Minutes,HeroDamage.Value/Minutes,ObjectiveDamage.Value/Minutes,Support.Value/Minutes,Economy.Value/Minutes};return values.Any(v=>double.IsNaN(v)||double.IsInfinity(v))?null:values;}
 }
 public sealed class PerformanceResult {public double? Score;public string Category="Unavailable",Reason="";public double[] Components;public int Matches;}
 public static class PerformanceEngine {
  // Empirical per-match percentile composite. Never compare a player's mean
  // directly to a single-match quantile distribution. Calibration is a separate gate.
  public static PerformanceResult Evaluate(PerformanceMatch[] recent,PerformanceMatch[] baseline,bool supportRole,bool calibrated){
   var result=new PerformanceResult();if(!calibrated){result.Reason=L.Choose("Model not calibrated on a held-out sample; no player rating is assigned.","Модель не откалибрована на отдельной выборке; оценка игроку не присваивается.");return result;}
   var matches=recent.OrderByDescending(m=>m.Played).GroupBy(m=>m.MatchId).Select(g=>g.First()).Take(20).ToArray();if(matches.Length<20){result.Reason="Requires 20 completed matches";return result;}
   var scores=new List<double[]>();foreach(var match in matches){var values=match.Metrics();if(values==null||match.Rank<10||match.Version<=0){result.Reason="Missing hero/rank/version or a required performance metric";return result;}
    var peers=baseline.Where(p=>p.Account!=match.Account&&p.Hero==match.Hero&&p.Rank>=10&&Math.Abs(p.Rank/10-match.Rank/10)<=1&&p.Mode==match.Mode&&p.Version==match.Version&&p.Played<match.Played&&(match.Played-p.Played).TotalDays<=30&&p.Metrics()!=null).GroupBy(p=>p.MatchId+":"+p.Account).Select(g=>g.First()).ToArray();
    if(peers.Select(p=>p.MatchId).Distinct().Count()<40||peers.Select(p=>p.Account).Distinct().Count()<20){result.Reason="Insufficient independent prior same-hero/rank/patch matches";return result;}
    var pct=new double[6];for(int i=0;i<6;i++){int column=i;var a=peers.Select(p=>p.Metrics()[column]).ToArray();pct[i]=100.0*(a.Count(v=>v<values[i])+.5*a.Count(v=>v==values[i]))/a.Length;}
    scores.Add(new[]{(pct[0]+pct[1])/2,pct[2],pct[3],pct[4],pct[5]});
   }
   var components=Enumerable.Range(0,5).Select(i=>scores.Average(s=>s[i])).ToArray();double[] weights=supportRole?new[]{.25,.15,.15,.35,.10}:new[]{.35,.25,.15,.15,.10};double score=Enumerable.Range(0,5).Sum(i=>weights[i]*components[i]);result.Score=score;result.Matches=20;result.Components=components;result.Category=score<30?"LOW SKILL":score<70?"NORMAL":score<85?"STRONG":"HIGH SKILL";result.Reason="20 matches; empirical peer percentiles; weights "+string.Join("/",weights.Select(w=>(w*100).ToString("0")));return result;
  }
 }
}
