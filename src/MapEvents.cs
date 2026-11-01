using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeadlockOverlay {
    public sealed class MapSnapshot {
        public double? Seconds,NextBoss;
        public bool ScreenClock;
        public bool Paused,Fresh;
        public bool Ended,StreetBrawl;
        public double? RiftOpensAt;public double RiftEarliest=580,RiftLatest=700;
        public string RiftValue {get{
            if(!Fresh||!Seconds.HasValue)return "--:--";
            double t=Seconds.Value;
            if(RiftOpensAt.HasValue&&t<=RiftOpensAt.Value+120)return t<RiftOpensAt.Value?Time(RiftOpensAt.Value-t):L.Choose("Active","На карте");
            if(t>=RiftLatest)return "--:--";
            return "≈ "+Time((RiftEarliest+RiftLatest)/2-t);
        }}
        public string RiftCaption {get{
            if(!Fresh||!Seconds.HasValue)return L.Choose("Waiting for clock","Ожидание времени");
            if(RiftOpensAt.HasValue&&Seconds.Value<=RiftOpensAt.Value+120)return Seconds.Value<RiftOpensAt.Value?L.Choose("Opening in","До открытия"):L.Choose("Rift is open","Рифт открыт");
            return Seconds.Value<RiftLatest?L.Choose("Estimate · ±1 min","Прогноз · ±1 мин"):L.Choose("Waiting for signal","Ожидание сигнала");
        }}
        public string RiftText {get{return RiftCaption+"\n"+RiftValue;}}
        public string Rift="unknown";
        public long Tick;
        public string CrystalText {get{return Fresh&&Seconds.HasValue?(ScreenClock?"":"≈ ")+Time(300-Seconds.Value%300):"—";}}
        public string BossText {get{return Fresh&&Seconds.HasValue&&NextBoss.HasValue?(NextBoss.Value>Seconds.Value?(ScreenClock?"":"≈ ")+Time(NextBoss.Value-Seconds.Value):L.T("На карте")):"—";}}
        public static string Time(double seconds){int n=(int)Math.Ceiling(Math.Max(0,seconds));return (n/60).ToString("00")+":"+(n%60).ToString("00");}
    }
    // The SQL stream carries server ticks. Current Deadlock builds use 64 Hz (API parser).
    public sealed class MapEvents : IDisposable {
        const string Rules="SELECT tick, \"m_pGameRules__m_flGameStartTime\" AS start_time, \"m_pGameRules__m_tNextMidBossSpawnTime\" AS next_boss, \"m_pGameRules__m_nTotalPausedTicks\" AS paused_ticks, \"m_pGameRules__m_bGamePaused\" AS paused, \"m_pGameRules__m_iWinningTeam\" AS winner, \"m_pGameRules__m_eGameMode\" AS mode FROM CCitadelGameRulesProxy";
        readonly object gate=new object();readonly ManualResetEvent stop=new ManualResetEvent(false);
        readonly List<HttpWebRequest> requests=new List<HttpWebRequest>();readonly long match;
        bool disposed,paused,ended,brawl;long tick;double? start,nextBoss,seconds;DateTime received=DateTime.MinValue,riftUntil=DateTime.MinValue;string rift="unknown";
        double? riftOpens;double riftEarliest=580,riftLatest=700;long pausedTicks,lastRiftTick=-1;
        public MapEvents(long match){this.match=match;}
        public void Start(){Task.Run(()=>Read(Rules,false));Task.Run(()=>Read("SELECT * FROM HudGameAnnouncementEvent",true));Task.Run(()=>Read("SELECT * FROM GameOverEvent",true));}
        static double? Num(Dictionary<string,object> d,string name){double n;return double.TryParse(Json.Str(d,name),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?(double?)n:null;}
        public void Apply(Dictionary<string,object> d,bool announcement,DateTime now){lock(gate){
            if(announcement&&Json.Get(d,"winning_team")!=null){var winning=Json.Number(d,"winning_team");if(winning==2||winning==3)ended=true;return;}
            if(announcement){var text=Json.Serializer().Serialize(d).ToLowerInvariant();if(!text.Contains("rift")&&!text.Contains("koth"))return;
                long eventTick=Json.Number(d,"tick")??tick;if(eventTick<=lastRiftTick)return;
                double? eventTime=start.HasValue?(eventTick-pausedTicks)/64.0-start.Value:seconds;
                if(text.Contains("expired")||text.Contains("completed")||text.Contains("timeranout")){lastRiftTick=eventTick;rift="unknown";riftUntil=now;
                    // When joining after the warning, estimate from completion rather than keep the first-spawn window forever.
                    if(!riftOpens.HasValue&&eventTime.HasValue){riftEarliest=eventTime.Value+280;riftLatest=eventTime.Value+400;}
                    riftOpens=null;return;}
                // The global warning is 20 s before opening; it is NOT the earlier grey-world effect.
                if(!text.Contains("koth_warning")||text.Contains("time_warning"))return;
                lastRiftTick=eventTick;
                rift=text.Contains("green")?"green":text.Contains("yellow")?"yellow":"grey";
                if(eventTime.HasValue){riftOpens=eventTime.Value+20;riftEarliest=riftOpens.Value+280;riftLatest=riftOpens.Value+400;}
                riftUntil=now.AddSeconds(140);return;}
            long? t=Json.Number(d,"tick");if(!t.HasValue||t<tick)return;
            start=Num(d,"start_time")??start;nextBoss=Num(d,"next_boss")??nextBoss;if(Json.Flag(d,"paused").HasValue)paused=Json.Bool(d,"paused");tick=t.Value;
            var winner=Json.Number(d,"winner");if(winner==2||winner==3)ended=true;string mode=Json.Str(d,"mode");if(mode.Length>0)brawl=mode=="4"||mode=="04000000"||mode=="0400000000000000";
            pausedTicks=Json.Number(d,"paused_ticks")??pausedTicks;seconds=start.HasValue?Math.Max(0,(tick-pausedTicks)/64.0-start.Value):(double?)null;received=now;
        }}
        public MapSnapshot Snapshot(DateTime now,long sourceTick=0){lock(gate){bool fresh=seconds.HasValue&&((now-received).TotalSeconds<20||sourceTick>=tick);double? current=seconds;if(current.HasValue&&!paused&&sourceTick>tick)current+=(sourceTick-tick)/64.0;return new MapSnapshot{Seconds=current,NextBoss=nextBoss,Paused=paused,Fresh=fresh,Tick=tick,Ended=ended,StreetBrawl=brawl,Rift=now<riftUntil?rift:"unknown",RiftOpensAt=riftOpens,RiftEarliest=riftEarliest,RiftLatest=riftLatest};}}
        void Read(string query,bool announcement){int failures=0;while(!disposed){bool waiting=false;if(!LiveBackoff.Wait(stop))return;try{
            var req=(HttpWebRequest)WebRequest.Create(BroadcastResolver.QueryUrl(match,query));req.ServicePoint.ConnectionLimit=12;req.Timeout=45000;req.ReadWriteTimeout=45000;req.UserAgent="DeadlockOverlay/0.6";
            lock(gate){if(disposed)return;requests.Add(req);}
            try{using(var response=req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){string line,kind="";while(!disposed&&(line=reader.ReadLine())!=null){if(line.StartsWith("event:"))kind=line.Substring(6).Trim();else if(line.StartsWith("data:")){if(kind=="end")throw new IOException(L.T("Поток таймеров завершён · переподключение"));if(kind=="error")throw new IOException(line);Apply(Json.Obj(line.Substring(5).Trim()),announcement,DateTime.UtcNow);failures=0;}else if(line.Length==0)kind="";}}}finally{lock(gate)requests.Remove(req);}
        }catch(Exception e){waiting=e is BroadcastWaiting;if(!disposed){var web=e as WebException;var response=web==null?null:web.Response as HttpWebResponse;if(response!=null&&(int)response.StatusCode==429){using(response)using(var reader=new StreamReader(response.GetResponseStream()))LiveBackoff.Observe(reader.ReadToEnd());}Log.Write("Map events",e);}}if(stop.WaitOne(waiting?5000:Math.Min(60000,3000*(1<<Math.Min(failures++,4)))))return;}}
        public void Dispose(){lock(gate){disposed=true;stop.Set();foreach(var request in requests)request.Abort();}}
    }
    public static class HudOrder {
        public static int? DecodeSlot(string value){int n;if(int.TryParse(value,out n)&&n>=1&&n<=12)return n;if(value!=null&&value.Length==16&&int.TryParse(value.Substring(0,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n)&&n>=1&&n<=12&&value.Substring(2)=="00000000000000")return n;return null;}
    }
}




