using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace DeadlockOverlay {
    public static class Patrons {
        public static string Name(int team){return team==2?"Hidden King":team==3?"Archmother":L.T("Неизвестно");}
    }
    public sealed class ImpactRow {
        public Player Player;
        public double? Score;
        public string Grade;
        public double Damage, Objectives, Support, Combat, Economy;
    }
    public sealed class MatchSummary {
        public string MatchId,Reason;
        public DateTime Captured;
        public Player[] Players;
        public bool Fresh;
        public int? WinningTeam;
        public long? DurationSeconds;
        public string SavedPath;
        public static MatchSummary Capture(MatchState state,string reason){
            return new MatchSummary {MatchId=state.MatchId,Reason=reason,WinningTeam=state.WinningTeam,DurationSeconds=state.DurationSeconds,Captured=DateTime.UtcNow,Fresh=state.Healthy&&state.Connected(DateTime.UtcNow)&&state.Players.Values.All(p=>(DateTime.UtcNow-p.Updated).TotalSeconds<15),
                Players=state.Players.Values.Select(p=>new Player{SteamId=p.SteamId,Slot=p.Slot,Team=p.Team,Hero=p.Hero,Name=p.Name,HeroName=p.HeroName,Damage=p.Damage,ObjectDamage=p.ObjectDamage,Healing=p.Healing,Kills=p.Kills,Deaths=p.Deaths,Assists=p.Assists,Souls=p.Souls,UnspentSouls=p.UnspentSouls,Updated=p.Updated}).ToArray()};
        }
        public ImpactRow[] Rank(){return ImpactProcessor.Rank(Players);}
        public void Save(){
            string folder=Path.Combine(SettingsManager.Folder,"summaries");Directory.CreateDirectory(folder);
            string stem=MatchId+"-"+Captured.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
            SavedPath=Path.Combine(folder,stem+".html");
            File.WriteAllText(SavedPath,Html(),Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder,stem+".json"),Json.Serializer().Serialize(this),Encoding.UTF8);
        }
        static string E(string s){return WebUtility.HtmlEncode(s??"");}
        public static string N(long? n){return n.HasValue?n.Value.ToString("N0",CultureInfo.GetCultureInfo("ru-RU")):"—";}
        public string Html(){return SummaryView.Html(this);}
    }
    public static class ImpactProcessor {
        public static string Explanation{get{return L.T("Индекс 0–100: 30% урон героям, 25% строения, 15% лечение, 20% боевое участие, 10% экономика. Каждый показатель делится на максимум среди 12 игроков. Боевое участие = (убийства + 0.7 × помощи) / (1 + 0.25 × смерти). Категория с нулём у всех исключается, веса остальных нормируются. Неполные данные не заменяются нулями: общий рейтинг недоступен. Оценки: S ≥80, A ≥65, B ≥50, C ≥35, D <35. Это сравнительная оценка вклада: она не учитывает роль, MMR, контроль, позиционирование и решающие моменты матча.");}}
        public static ImpactRow[] Rank(Player[] players){
            var result=players.Select(p=>new ImpactRow{Player=p,Grade="—"}).ToArray();
            if(players.Length!=12||players.Count(p=>p.Team==2)!=6||players.Count(p=>p.Team==3)!=6||players.Any(p=>!p.Damage.HasValue||!p.ObjectDamage.HasValue||!p.Healing.HasValue||!p.Kills.HasValue||!p.Deaths.HasValue||!p.Assists.HasValue||!p.Souls.HasValue))return result;
            double damage=players.Max(p=>p.Damage.Value),objects=players.Max(p=>p.ObjectDamage.Value),healing=players.Max(p=>p.Healing.Value),combat=players.Max(p=>Fight(p)),economy=players.Max(p=>p.Souls.Value);
            double weight=(damage>0?.30:0)+(objects>0?.25:0)+(healing>0?.15:0)+(combat>0?.20:0)+(economy>0?.10:0);
            if(weight==0)return result;
            foreach(var r in result){var p=r.Player;r.Damage=damage>0?p.Damage.Value/damage:0;r.Objectives=objects>0?p.ObjectDamage.Value/objects:0;r.Support=healing>0?p.Healing.Value/healing:0;r.Combat=combat>0?Fight(p)/combat:0;r.Economy=economy>0?p.Souls.Value/economy:0;
                r.Score=100*(.30*r.Damage+.25*r.Objectives+.15*r.Support+.20*r.Combat+.10*r.Economy)/weight;r.Grade=r.Score>=80?"S":r.Score>=65?"A":r.Score>=50?"B":r.Score>=35?"C":"D";}
            return result.OrderByDescending(r=>r.Score).ThenBy(r=>r.Player.Slot).ToArray();
        }
        static double Fight(Player p){return(p.Kills.Value+.7*p.Assists.Value)/(1+.25*p.Deaths.Value);}
    }
}


