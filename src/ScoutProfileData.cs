using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace DeadlockOverlay {
 public sealed class ReviewSample {public int Second;public long? Souls,Damage,Objectives,Healing;}
 public sealed class ReviewPurchase {public int Second;public long Item;public int? Sold;}
 public sealed class ReviewEvent {public int Second,Hero;public string Kind="";}
 public sealed class ScoutMatchDetail {
  public ReviewEvent[] Events=new ReviewEvent[0];
  public int PayloadVersion;public ReviewSample[] Timeline=new ReviewSample[0];public ReviewPurchase[] Purchases=new ReviewPurchase[0];
  public long Match,Account;public int Hero,Seconds,LastSecond;public DateTime Fetched;
  public long? Damage,Objectives,Healing,TeamHealing;public bool Available;
  public static ScoutMatchDetail Parse(object raw,long match,long account){
   var info=Json.Obj(Json.Get(Json.Obj(raw),"match_info"));var result=new ScoutMatchDetail{Match=match,Account=account,Fetched=DateTime.UtcNow};
   if(Json.Number(info,"match_id")!=match)return result;
   var player=(Json.Get(info,"players") as object[]??new object[0]).Select(Json.Obj).FirstOrDefault(p=>Json.Number(p,"account_id")==account);if(player==null)return result;
   result.Hero=(int)(Json.Number(player,"hero_id")??0);result.Seconds=(int)(Json.Number(info,"duration_s")??0);
   result.PayloadVersion=3;
   var roster=(Json.Get(info,"players") as object[]??new object[0]).Select(Json.Obj).ToArray();
   result.Events=(Json.Get(player,"death_details") as object[]??new object[0]).Select(Json.Obj).Where(d=>Json.Number(d,"game_time_s")>=0&&Json.Number(d,"game_time_s")<=result.Seconds).Select(d=>{var killer=roster.FirstOrDefault(p=>Json.Number(p,"player_slot")==Json.Number(d,"killer_player_slot"));return new ReviewEvent{Second=(int)Json.Number(d,"game_time_s").Value,Kind="death",Hero=killer==null?0:(int)(Json.Number(killer,"hero_id")??0)};}).OrderBy(d=>d.Second).Take(128).ToArray();
   result.Timeline=(Json.Get(player,"stats") as object[]??new object[0]).Select(Json.Obj).Where(s=>Json.Number(s,"time_stamp_s")>=0&&Json.Number(s,"time_stamp_s")<=result.Seconds+30).Select(s=>new ReviewSample{Second=(int)Json.Number(s,"time_stamp_s").Value,Souls=Json.Number(s,"net_worth"),Damage=Json.Number(s,"player_damage"),Objectives=Json.Number(s,"boss_damage"),Healing=Json.Number(s,"teammate_healing")}).OrderBy(s=>s.Second).GroupBy(s=>s.Second).Select(g=>g.Last()).Take(600).ToArray();
   result.Purchases=(Json.Get(player,"items") as object[]??new object[0]).Select(Json.Obj).Where(s=>Json.Number(s,"game_time_s")>=0&&Json.Number(s,"game_time_s")<=result.Seconds&&Json.Number(s,"item_id")>0).Select(s=>new ReviewPurchase{Second=(int)Json.Number(s,"game_time_s").Value,Item=Json.Number(s,"item_id").Value,Sold=(int?)Json.Number(s,"sold_time_s")}).OrderBy(s=>s.Second).Take(256).ToArray();
   var last=(Json.Get(player,"stats") as object[]??new object[0]).Select(Json.Obj).Where(s=>Json.Number(s,"time_stamp_s").HasValue).OrderBy(s=>Json.Number(s,"time_stamp_s")).LastOrDefault();if(last==null)return result;
   result.LastSecond=(int)(Json.Number(last,"time_stamp_s")??0);
   // An early snapshot must not masquerade as an end-of-match total.
   result.Available=result.Seconds>0&&result.LastSecond>=result.Seconds-30&&result.LastSecond<=result.Seconds+30;
   if(result.Available){result.Damage=Json.Number(last,"player_damage");result.Objectives=Json.Number(last,"boss_damage");result.Healing=Json.Number(last,"player_healing");result.TeamHealing=Json.Number(last,"teammate_healing");}return result;
  }
 }
 public static class ScoutProfileData {
  public static CultureInfo Culture{get{return CultureInfo.GetCultureInfo(L.Language=="ru"?"ru-RU":"en-US");}}
  public static string Missing{get{return L.Choose("No data","Нет данных");}}
  public static string N(double? n){if(!n.HasValue)return Missing;return Math.Abs(n.Value)>=10000?(n.Value/1000).ToString("0.0",Culture)+"k":n.Value.ToString(n.Value==Math.Round(n.Value)?"N0":"N1",Culture).Replace('\u00a0',' ');}
  public static double? Mean(IEnumerable<double?> values){var a=values.Where(v=>v.HasValue).Select(v=>v.Value).ToArray();return a.Length==0?(double?)null:a.Average();}
  public static double? Ratio(ScoutMatch m){return m.Kills.HasValue&&m.Deaths.HasValue&&m.Assists.HasValue?(double?)(m.Kills.Value+m.Assists.Value)/Math.Max(1,m.Deaths.Value):null;}
  public static string Rate(IEnumerable<ScoutMatch> h){var a=h.Where(m=>m.Won.HasValue).ToArray();return a.Length==0?Missing:(100.0*a.Count(m=>m.Won==true)/a.Length).ToString("0.0",Culture)+"%";}
  public static string Date(long seconds){try{return seconds>0?new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime().ToString("dd MMM · HH:mm",Culture):Missing;}catch{return Missing;}}
  public static string Duration(int? seconds){return seconds.HasValue&&seconds>0?(seconds.Value/60).ToString("00")+":"+(seconds.Value%60).ToString("00"):Missing;}
  public static string Tip(string key){switch(key){
   case "main":return L.Choose("MAIN — frequently plays the current hero.","Мейнер — часто играет на текущем герое.");
   case "smurf":return L.Choose("SMURF — high ranked win rate; a statistical hint.","Смурф — высокий процент побед в рейтинге; статистический признак.");
   case "streak":return L.Choose("WIN STREAK — consecutive victories.","Винстрик — серия побед подряд.");
   case "leaver":return L.Choose("LEAVER — repeated recorded early departures.","Руинер — частые зафиксированные досрочные выходы.");
   case "new":return L.Choose("NEW TO HERO — no games on this hero in the sample.","Новичок — нет игр на этом герое в выборке.");
   case "low":return L.Choose("LOW SKILL — low average performance.","Лоускилл — низкая средняя эффективность.");
   case "high":return L.Choose("HIGH SKILL — high average performance.","Хайскилл — высокая средняя эффективность.");
   case "party":return L.Choose("PARTY — confirmed current group.","Пати — подтверждённая текущая группа.");
   case "likely":return L.Choose("PARTY? — inferred from shared match history.","Предполагаемая пати — по истории совместных игр.");default:return "";}}
  public static double? RankStep(int? badge){return badge.HasValue&&badge/10>=1&&badge/10<=11&&badge%10>=1&&badge%10<=6?(double?)((badge.Value/10-1)*6+badge.Value%10-1):null;}
  public static int StepBadge(double step){int n=(int)Math.Round(step);return (n/6+1)*10+n%6+1;}
  public static string Tactics(ScoutMatch[] recent,int hero,Func<long,ScoutMatchDetail> detail,bool? ally=null){
   var h=recent.Where(m=>hero>0&&m.Hero==hero).ToArray();
   if(h.Length<5)return L.Choose("Too few recent games on the current hero for tactical conclusions: ","Мало недавних игр на текущем герое для тактических выводов: ")+h.Length+"/"+recent.Length+". "+L.Choose("Overall statistics are not substituted.","Общая статистика не подставляется.");
   var lines=new List<string>{LocalSnapshot.HeroName(hero)+": "+h.Length+"/"+recent.Length+L.Choose(" recent games. These observations describe past games, not future actions."," недавних игр. Наблюдения описывают прошлые игры, а не будущие действия.")};
   if(recent.Length==20&&h.Length>=14)lines.Add(ally==true?L.Choose("Practiced hero: coordinate around their familiar abilities; this is not a guarantee of performance.","Привычный герой: согласуйте действия вокруг его способностей; это не гарантия результата."):L.Choose("Hero specialist. Against: respect demonstrated familiarity; do not assume unfamiliarity with abilities. Ally: this is a practiced pick, not a guarantee of performance.","Специалист по герою. Против: учитывайте опыт владения способностями. Союзник: привычный выбор, но не гарантия результата."));
   var valid=h.Where(m=>m.Duration>0&&m.Deaths.HasValue).ToArray();
   if(valid.Length>=5)lines.Add(L.Choose("Recorded deaths per 10 minutes: ","Зафиксированные смерти за 10 минут: ")+N(valid.Sum(m=>m.Deaths.Value)*600.0/valid.Sum(m=>m.Duration.Value))+" (n="+valid.Length+"). "+L.Choose("This alone does not establish aggressive or cautious play.","Само по себе это не доказывает агрессивный или осторожный стиль."));
   var support=h.Select(m=>detail(m.Id)).Where(d=>d!=null&&d.Available&&d.Hero==hero&&d.TeamHealing.HasValue&&d.Seconds>0).ToArray();
   if(support.Length>=5&&support.Count(d=>d.TeamHealing>0)>=Math.Ceiling(support.Length*.6))lines.Add(L.Choose("Repeated ally healing: ","Регулярное лечение союзников: ")+N(support.Sum(d=>d.TeamHealing.Value)*60.0/support.Sum(d=>d.Seconds))+L.Choose("/min. Against: check the current healing build before choosing anti-heal. Ally: consider staying within healing reach when the current build supports it.","/мин. Против: проверьте текущую сборку на лечение перед выбором антихила. Союзник: учитывайте радиус лечения, если текущая сборка его поддерживает."));
   var detailed=h.Select(m=>detail(m.Id)).Where(d=>d!=null&&d.Available&&d.Hero==hero&&d.Seconds>0&&d.Damage.HasValue&&d.Objectives.HasValue).ToArray();
   if(detailed.Length>=5)lines.Add(L.Choose("Recorded per minute: ","Записано за минуту: ")+N(detailed.Sum(d=>d.Damage.Value)*60.0/detailed.Sum(d=>d.Seconds))+L.Choose(" hero damage; "," урона героям; ")+N(detailed.Sum(d=>d.Objectives.Value)*60.0/detailed.Sum(d=>d.Seconds))+L.Choose(" objective damage. This describes contribution, not initiation or positioning."," урона строениям. Это вклад, а не доказательство инициации или позиционирования."));
   var economy=h.Where(m=>m.Souls.HasValue&&m.Duration>0).ToArray();if(economy.Length>=10){double newer=economy.Take(5).Average(m=>m.Souls.Value*60.0/m.Duration.Value),older=economy.Skip(5).Take(5).Average(m=>m.Souls.Value*60.0/m.Duration.Value);lines.Add(L.Choose("Net worth/min, latest five vs previous five: ","Ценность героя/мин, последние пять против предыдущих пяти: ")+N(newer)+" / "+N(older)+L.Choose(". Check their current items before committing to a fight; past economy does not establish their present advantage.",". Проверьте текущие предметы перед боем; прошлая экономика не доказывает преимущество сейчас."));}
   return string.Join(" ",lines);
  }
  public static double? Metric(ScoutMatch m,ScoutMatchDetail detail,int metric){switch(metric){case 0:return Ratio(m);case 1:return m.Souls;case 2:return m.Won.HasValue?(double?)(m.Won.Value?1:0):null;case 3:return RankStep(m.Badge);case 4:return detail!=null&&detail.Available?detail.Damage:null;case 5:return detail!=null&&detail.Available?detail.Objectives:null;case 6:return detail!=null&&detail.Available?detail.Healing:null;default:return null;}}
  public static string PlayStyle(ScoutMatch[] h,Func<long,ScoutMatchDetail> detail){
   if(h.Length<5)return L.Choose("At least five completed matches are needed to describe this sample.","Для описания выборки нужны хотя бы пять завершённых матчей.");
   var main=h.Where(m=>m.Hero>0).GroupBy(m=>m.Hero).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).FirstOrDefault();var text=new List<string>();
   if(main!=null&&h.Length==20&&main.Count()>=14)text.Add(L.Choose("Hero specialist: a consistent main pick.","Специализация на герое: стабильно выбирает одного мейна."));
   if(main!=null)text.Add(L.Choose("Most played: ","Чаще всего: ")+LocalSnapshot.HeroName(main.Key)+" ("+main.Count()+"/"+h.Length+").");
   var valid=h.Where(m=>m.Duration>0&&m.Deaths.HasValue&&m.Kills.HasValue&&m.Assists.HasValue).ToArray();
   if(valid.Length>=5){double minutes=valid.Sum(m=>m.Duration.Value)/60.0;text.Add(L.Choose("Per 10 minutes: ","За 10 минут: ")+N(valid.Sum(m=>m.Kills.Value+m.Assists.Value)*10/minutes)+L.Choose(" kills + assists, "," убийств + помощи, ")+N(valid.Sum(m=>m.Deaths.Value)*10/minutes)+L.Choose(" deaths", " смертей")+" ("+valid.Length+L.Choose(" games)."," игр)."));}
   var enriched=h.Select(m=>detail(m.Id)).Where(d=>d!=null&&d.Available&&d.Seconds>0&&d.Damage.HasValue&&d.TeamHealing.HasValue).ToArray();
   if(enriched.Length>=5){double minutes=enriched.Sum(d=>d.Seconds)/60.0;text.Add(L.Choose("Recorded contribution: ","Зафиксированный вклад: ")+N(enriched.Sum(d=>d.Damage.Value)/minutes)+L.Choose(" hero damage/min; "," урона героям/мин; ")+N(enriched.Sum(d=>d.TeamHealing.Value)/minutes)+L.Choose(" ally healing/min", " лечения союзников/мин")+" ("+enriched.Length+L.Choose(" games)."," игр)."));}
   text.Add(L.Choose("Describes this sample; no rank-matched baseline is available for strong/weak play-style labels.","Описание этой выборки; без сравнения с аналогичным рангом сильные и слабые стороны не присваиваются."));return string.Join(" ",text);
  }
 }
}
