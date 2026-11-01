using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace DeadlockOverlay {
    public static class Json {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 131072, RecursionLimit = 24 }; }
        public static Dictionary<string, object> Obj(object o) {
            var s = o as string;
            if (s != null) { try { o = Serializer().DeserializeObject(s); } catch { return new Dictionary<string, object>(); } }
            return o as Dictionary<string, object> ?? new Dictionary<string, object>();
        }
        public static object Get(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? v : null; }
        public static string Str(Dictionary<string, object> d, string k) { return Convert.ToString(Get(d,k), CultureInfo.InvariantCulture); }
        public static bool Bool(Dictionary<string, object> d, string k) { return string.Equals(Str(d,k), "true", StringComparison.OrdinalIgnoreCase); }
        public static bool? Flag(Dictionary<string, object> d,string k) { bool b;return bool.TryParse(Str(d,k),out b)?(bool?)b:null; }
        public static long? Number(Dictionary<string, object> d, string k) { long n; return long.TryParse(Str(d,k), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0 && n <= 1000000000000L ? (long?)n : null; }
    }
    public sealed class Player {
        public int Slot, Team, Hero;
        public string Name, HeroName;
        public bool Local;
        public int? Lane;
        public int? LobbySlot;
        public int? HudOrder;
        public string SteamId;
        public string BridgeBuff="?";
        public bool? Rejuvenator, Rebirth;
        public long? Damage, ObjectDamage, Healing, Kills, Deaths, Assists, Souls, UnspentSouls;
        public DateTime Updated;
    }
    // One UI-thread-owned state. A full bridge snapshot atomically replaces its roster.
    public sealed class MatchState {
        public string MatchId = "", Phase = "Init", SourceStatus = L.T("Укажите ID просматриваемого матча"), Session = "";
        public bool Demo, Healthy, Spectator;
        public Advice Advice=new Advice();
        public DateTime Received = DateTime.MinValue;
        public long Sequence = -1;
        public long GameTick;
        public int ViewedHeroId;
        public int? WinningTeam;
        public long? DurationSeconds;
        public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public bool Active { get { return Phase == "GameInProgress"; } }
        public int? LocalTeam {
            get { var p = Players.Values.Where(x => x.Local).ToArray(); return p.Length == 1 ? (int?)p[0].Team : null; }
        }
        public bool Connected(DateTime now) { return (now - Received).TotalSeconds < 8; }
        public void Reset() { Players.Clear();WinningTeam=null;DurationSeconds=null; Phase="Init"; MatchId=""; Sequence=-1; Session=""; Healthy=false; Spectator=false; SourceStatus=L.T("Укажите ID просматриваемого матча"); Received=DateTime.MinValue; }
        public bool Apply(string json, DateTime now) {
            return Apply(Json.Obj(Json.Serializer().DeserializeObject(json)),now);
        }
        public bool Apply(Dictionary<string,object> d,DateTime now) {
            var session = Json.Str(d,"session"); var seq = Json.Number(d,"sequence");
            if (session.Length == 0 || !seq.HasValue) throw new FormatException("Missing session/sequence");
            if (session == Session && seq <= Sequence) return false;
            var info = Json.Obj(Json.Get(d,"info"));
            var game = Json.Obj(Json.Get(info,"game_info"));
            var match = Json.Obj(Json.Get(info,"match_info"));
            string phase = Json.Str(game,"phase");
            if(MatchId.Length>0&&Json.Str(match,"match_id").Length>0&&MatchId!=Json.Str(match,"match_id")){WinningTeam=null;DurationSeconds=null;}
            if (phase.Length == 0) phase = "Init";
            var fresh = new Dictionary<int,Player>();
            var ages = Json.Obj(Json.Get(d,"ages"));
            string steam = Json.Str(game,"steam_id");
            foreach (var entry in match) {
                int slot;
                if (!entry.Key.StartsWith("roster_",StringComparison.Ordinal) || !int.TryParse(entry.Key.Substring(7),out slot) || slot < 0 || slot > 11) continue;
                var p=Json.Obj(entry.Value); long? team=Json.Number(p,"team_id"), hero=Json.Number(p,"hero_id");
                if (team != 2 && team != 3) continue;
                long? age=Json.Number(ages,entry.Key);
                fresh[slot]=new Player { Slot=slot, Team=(int)team.Value, Hero=(int)Math.Min(hero ?? 0,100000),
                    Name=Limit(Json.Str(p,"player_name"),64), HeroName=Limit(Json.Str(p,"hero_name"),40),
                    SteamId=Json.Str(p,"steam_id"),Local=Json.Bool(p,"is_local") || (steam.Length > 3 && steam == Json.Str(p,"steam_id")),
                    Lane=(int?)Json.Number(p,"lane"),HudOrder=(int?)Json.Number(p,"hud_order"),LobbySlot=HudOrder.DecodeSlot(Json.Str(p,"lobby_slot")),BridgeBuff=BridgeEffects.Name(Json.Get(p,"modifiers")),Rejuvenator=Json.Flag(p,"rejuvenator"),Rebirth=Json.Flag(p,"rebirth"),
                    Damage=Json.Number(p,"hero_damage"), ObjectDamage=Json.Number(p,"object_damage"), Healing=Json.Number(p,"hero_healing"), Kills=Json.Number(p,"kills"),Deaths=Json.Number(p,"deaths"),Assists=Json.Number(p,"assists"),Souls=Json.Number(p,"souls"),UnspentSouls=Json.Number(p,"unspent_souls"),Updated=now.AddMilliseconds(-Math.Min(age ?? 100000,100000)) };
            }
            Session=session; Sequence=seq.Value; Received=now; Phase=phase;
            MatchId=Json.Str(match,"match_id"); Healthy=Json.Bool(d,"healthy");
            Spectator=Json.Bool(d,"spectator");
            SourceStatus=Limit(Json.Str(d,"status"),160);
            GameTick=match.Where(x=>x.Key.StartsWith("roster_")).Select(x=>Json.Number(Json.Obj(x.Value),"tick")??0).DefaultIfEmpty(0).Max();
            Players.Clear(); if (Active) foreach (var p in fresh) Players.Add(p.Key,p.Value);
            return true;
        }
        static string Limit(string s,int n) { return new string(s.Where(c=>!char.IsControl(c)).Take(n).ToArray()); }
    }
    public sealed class TeamStats {
        public Player[] Players;
        public long? Total, ObjectTotal;
        public long Maximum;
        public string Status;
    }
    public static class StatsProcessor {
        public static bool Available(MatchState state, Player p, DateTime now) {
            return state.Healthy && state.Connected(now) && (now-p.Updated).TotalSeconds < 15 && p.Damage.HasValue;
        }
        public static TeamStats Team(MatchState state, int team, bool sorted, DateTime now) {
            var rows=state.Players.Values.Where(p=>p.Team==team).OrderBy(p=>p.Slot).ToArray();
            if (sorted) rows=rows.OrderByDescending(p=>Available(state,p,now) ? p.Damage : null).ThenBy(p=>p.Slot).ToArray();
            bool full=rows.Length==6 && rows.All(p=>Available(state,p,now));
            return new TeamStats { Players=rows.Take(6).ToArray(), Total=full ? (long?)rows.Sum(p=>p.Damage.Value) : null,
                ObjectTotal=rows.Length==6&&rows.All(p=>state.Healthy&&state.Connected(now)&&(now-p.Updated).TotalSeconds<15&&p.ObjectDamage.HasValue)?(long?)rows.Sum(p=>p.ObjectDamage.Value):null,
                Maximum=Math.Max(1,rows.Where(p=>Available(state,p,now)).Select(p=>p.Damage.Value).DefaultIfEmpty(0).Max()),
                Status=!state.Connected(now) ? L.T("Связь потеряна") : !state.Healthy ? L.T("Источник недоступен") : !full ? L.T("Неполные / устаревшие данные") : state.Demo ? L.T("ДЕМО · имитация матча") : state.SourceStatus };
        }
    }
}


