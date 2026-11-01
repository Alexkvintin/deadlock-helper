using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeadlockOverlay {
    public sealed class ItemSuggestion {
        public long Id,Matches,Wins,Losses;
        public string Name;
        public double WinRate {get{return Wins+Losses>0?100.0*Wins/(Wins+Losses):0;}}
    }
    public sealed class Advice {
        public string Title=L.T("ПРЕДМЕТЫ"),Context=L.T("Выберите своего героя в настройках"),Source=L.T("Deadlock API · последние 30 дней");
        public ItemSuggestion[] Items=new ItemSuggestion[0];
    }
    // One background worker; published results are immutable. Never blocks the game/UI thread.
    public sealed class ItemAdvisor : IDisposable {
        readonly object gate=new object();
        bool disposed,busy;
        string key="";
        DateTime next=DateTime.MinValue;
        Advice lane=new Advice(),general=new Advice();
        long? duration;
        bool approximate;
        public Advice Current=new Advice();
        public static string Url(int hero,IEnumerable<int> enemies,bool laning){
            return "https://api.deadlock-api.com/v1/analytics/item-stats?hero_ids="+hero+"&enemy_hero_ids="+string.Join(",",enemies.Distinct().OrderBy(x=>x))+"&same_lane_filter="+(laning?"true":"false")+"&min_matches=30"+(laning?"&max_bought_at_s=480":"");
        }
        public static object Fetch(string url){
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=20000;req.ReadWriteTimeout=20000;req.UserAgent="DeadlockOverlay/0.4";
            using(var response=req.GetResponse())using(var reader=new System.IO.StreamReader(response.GetResponseStream()))return new JavaScriptSerializer{MaxJsonLength=16777216,RecursionLimit=64}.DeserializeObject(reader.ReadToEnd());
        }
        public static ItemSuggestion[] Parse(object data,Dictionary<long,string> names){
            var list=data as object[]??new object[0];
            return list.Select(Json.Obj).Select(d=>new ItemSuggestion{Id=Json.Number(d,"item_id")??0,Matches=Json.Number(d,"matches")??0,Wins=Json.Number(d,"wins")??0,Losses=Json.Number(d,"losses")??0})
                .Where(x=>x.Matches>=30&&x.Wins+x.Losses>0&&names.ContainsKey(x.Id)).OrderByDescending(x=>x.Matches).ThenByDescending(x=>x.WinRate).Take(3)
                .Select(x=>{x.Name=names[x.Id];return x;}).ToArray();
        }
        public void Update(MatchState state,Settings settings){
            if(!settings.ShowItems)return;
            if(!state.Active||state.Demo){Current=new Advice{Context=state.Demo?L.T("Рекомендации доступны в реальном матче"):L.T("Ожидание матча")};return;}
            var player=state.Players.Values.FirstOrDefault(x=>x.Local)??state.Players.Values.FirstOrDefault(x=>x.Hero==settings.AdvisorHero);
            if(player==null){Current=new Advice();return;}
            var enemies=state.Players.Values.Where(x=>x.Team!=player.Team).ToArray();
            if(enemies.Length!=6){Current=new Advice{Context=L.T("Ожидание полного состава")};return;}
            var laners=player.Lane.HasValue?enemies.Where(x=>x.Lane==player.Lane).ToArray():new Player[0];
            string match=state.MatchId,newKey=match+":"+player.Hero+"/"+player.Lane+":"+string.Join(",",enemies.Select(x=>x.Hero+"/"+x.Lane));
            lock(gate){
                if(key!=newKey){key=newKey;lane=new Advice{Context=L.T("Загрузка покупок для линии…")};general=new Advice{Context=L.T("Обновление статистики…")};duration=null;next=DateTime.MinValue;}
                var selected=settings.AdvisorPhase==1?lane:settings.AdvisorPhase==2?general:duration.HasValue?(duration<480?lane:general):new Advice{Context=L.T("Время матча недоступно. Выберите фазу в настройках.")};
                Current=new Advice{Title=selected.Title,Context=selected.Context,Items=selected.Items,Source=selected.Source+(settings.AdvisorPhase==0&&approximate?L.T(" · ≈ фаза"):"")};
                if(busy||DateTime.UtcNow<next)return;
                busy=true;next=DateTime.UtcNow.AddSeconds(60);
            }
            int hero=player.Hero;string heroName=player.HeroName;bool load=general.Items.Length==0;
            Task.Run(()=>{
                try{
                    long? seconds=null;bool estimated=false;
                    try{var active=Fetch("https://api.deadlock-api.com/v1/matches/active") as object[];var found=(active??new object[0]).Select(Json.Obj).FirstOrDefault(d=>Json.Str(d,"match_id")==match);if(found!=null){seconds=Json.Number(found,"duration_s");long? start=Json.Number(found,"start_time");if(!seconds.HasValue&&start.HasValue){long age=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds-start.Value;if(age>=0&&age<14400){seconds=age;estimated=true;}}}}catch(Exception e){Log.Write("Advisor clock",e);}
                    Advice l=null,g=null;
                    if(load){
                        var names=new Dictionary<long,string>();var assets=Fetch("https://api.deadlock-api.com/v1/assets/items?language=russian") as object[];
                        foreach(var item in assets??new object[0]){var d=Json.Obj(item);if(Json.Str(d,"type")=="upgrade"&&Json.Bool(d,"shopable"))names[Json.Number(d,"id")??0]=Json.Str(d,"name");}
                        g=new Advice{Title=L.T("ПОСЛЕ ЛИНИИ · ")+heroName,Context=L.T("Против: ")+string.Join(", ",enemies.Select(x=>x.HeroName)),Items=Parse(Fetch(Url(hero,enemies.Select(x=>x.Hero),false)),names)};
                        l=laners.Length==2?new Advice{Title=L.T("ЛИНИЯ · ДО 8:00"),Context=L.T("Против: ")+string.Join(" + ",laners.Select(x=>x.HeroName)),Items=Parse(Fetch(Url(hero,laners.Select(x=>x.Hero),true)),names)}:new Advice{Title=L.T("ЛИНИЯ"),Context=L.T("Не определены ровно два соперника на линии")};
                        string stamp=L.T("Deadlock API · 30 дней · ")+DateTime.Now.ToString("HH:mm");g.Source=stamp;l.Source=stamp;
                        if(g.Items.Length==0)g.Context=L.T("Недостаточно выборки (минимум 30 покупок)");
                        if(laners.Length==2&&l.Items.Length==0)l.Context=L.T("Мало данных по этой линии (минимум 30 покупок)");
                    }
                    lock(gate){if(!disposed&&key==newKey){duration=seconds;approximate=estimated;if(g!=null){general=g;lane=l;}}}
                }catch(Exception e){Log.Write("Item recommendations",e);lock(gate){if(key==newKey){general=new Advice{Context=L.T("Источник недоступен · повтор через минуту")};lane=general;}}}
                finally{lock(gate)busy=false;}
            });
        }
        public void Dispose(){lock(gate)disposed=true;}
    }
}

