using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeadlockOverlay {
    // Public spectator broadcast only. Public broadcast data is handled independently from the local source.
    public sealed class LiveBroadcastConnector : IGameConnector {
        public event Action<string> Snapshot;
        public event Action<Dictionary<string,object>> SnapshotData;
        public const int PublishIntervalMs=50;
        readonly long matchId;
        readonly string session=Guid.NewGuid().ToString("N");
        readonly object gate=new object();
        readonly Dictionary<int,Dictionary<string,object>> rows=new Dictionary<int,Dictionary<string,object>>();
        readonly Dictionary<int,DateTime> updated=new Dictionary<int,DateTime>();
        readonly Dictionary<int,string> heroes=new Dictionary<int,string>();
        readonly ManualResetEvent stop=new ManualResetEvent(false);
        HttpWebRequest request;
        System.Threading.Timer timer;
        volatile bool disposed;
        bool healthy,logged,dirty;DateTime lastEmit=DateTime.MinValue;
        long sequence,tick;
        string status=L.T("Подключение к трансляции…");
        public const string Query="SELECT tick, entity_index, \"m_iTeamNum\" AS team_id, \"m_iszPlayerName\" AS player_name, \"m_PlayerDataGlobal__m_nHeroID\" AS hero_id, \"m_PlayerDataGlobal__m_iHeroDamage\" AS hero_damage, \"m_PlayerDataGlobal__m_iObjectiveDamage\" AS object_damage, \"m_PlayerDataGlobal__m_iHeroHealing\" AS hero_healing, \"m_PlayerDataGlobal__m_iPlayerKills\" AS kills, \"m_PlayerDataGlobal__m_iPlayerAssists\" AS assists, \"m_PlayerDataGlobal__m_iDeaths\" AS deaths, \"m_PlayerDataGlobal__m_iGoldNetWorth\" AS souls, \"m_PlayerDataGlobal__m_iGold\" AS unspent_souls FROM CCitadelPlayerController";
        public LiveBroadcastConnector(long id) {
            matchId=id;
            try {
                var list=Json.Serializer().DeserializeObject(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","sources.json"))) as object[];
                if(list!=null)foreach(var o in list){var d=Json.Obj(o);heroes[(int)(Json.Number(d,"id")??0)]=Json.Str(d,"name");}
            } catch(Exception e){Log.Write("Hero names",e);}
        }
        public void Start() { lock(gate){if(disposed||timer!=null)return;timer=new System.Threading.Timer(_=>Emit(),null,0,PublishIntervalMs);}Task.Run((Action)ReadLoop); }
        void ReadLoop() {
            try { RunReadLoop(); } finally { stop.Dispose(); }
        }
        void RunReadLoop() {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            int failures=0;
            while(!disposed) {
                if(LiveBackoff.DelayMs>0){lock(gate){status=L.Choose("Source rate limit · retry in ","Лимит источника · повтор через ")+(int)Math.Ceiling(LiveBackoff.DelayMs/60000.0)+L.Choose(" min"," мин");dirty=true;}}if(!LiveBackoff.Wait(stop))return;
                int retry=10000;
                try {
                    lock(gate){healthy=false;status=L.T("Подключение к матчу ")+matchId+"…";}
                    string query=Query.Replace(" FROM CCitadelPlayerController",", \"m_nOriginalLaneAssignment\" AS lane, \"m_steamID\" AS steam_id, \"m_unLobbyPlayerSlot\" AS lobby_slot FROM CCitadelPlayerController");
                    string url=BroadcastResolver.QueryUrl(matchId,query);
                    var req=(HttpWebRequest)WebRequest.Create(url);lock(gate){if(disposed)return;request=req;}
                    req.ServicePoint.ConnectionLimit=12;req.AllowReadStreamBuffering=false;req.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                    req.UserAgent="DeadlockOverlay/0.8";req.Accept="text/event-stream";req.Timeout=20000;req.ReadWriteTimeout=45000;
                    using(var response=(HttpWebResponse)req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())) {
                        string line,eventName="";
                        while(!disposed&&(line=reader.ReadLine())!=null) {
                            if(line.Length>131072)throw new IOException("Oversized stream record");
                            if(line.StartsWith("event:"))eventName=line.Substring(6).Trim();
                            else if(line.StartsWith("data:")) {
                                string data=line.Substring(5).Trim();
                                if(eventName=="status"){ApplyStatus(data);continue;}
                                if(eventName=="error") {
                                    var error=Json.Obj(data);long code=Json.Number(error,"status")??0;
                                    if(code==429){LiveBackoff.Observe(data);retry=LiveBackoff.DelayMs;}
                                    throw new IOException("API "+code+": "+Json.Str(error,"error"));
                                }
                                // SSE completion is a transport event, not evidence that the match ended.
                                if(eventName=="end")throw new IOException(L.T("Поток завершён · переподключение"));
                                if(ApplyRow(data,DateTime.UtcNow))failures=0;
                            } else if(line.Length==0)eventName="";
                        }
                    }
                    if(!disposed)throw new IOException(L.T("Поток прерван"));
                }catch(Exception e){
                    if(disposed)return;
                    var web=e as WebException;
                    if(web!=null&&web.Response!=null){try{using(var response=web.Response)using(var reader=new StreamReader(response.GetResponseStream())){var detail=reader.ReadToEnd();Log.Write("Broadcast API response: "+detail.Substring(0,Math.Min(800,detail.Length)));var http=response as HttpWebResponse;if(http!=null&&(int)http.StatusCode==429){LiveBackoff.Observe(detail);retry=LiveBackoff.DelayMs;}}}catch(Exception detailError){Log.Write("Broadcast response",detailError);}}
                    failures++; Log.Write("Live broadcast",e);
                    lock(gate){healthy=false;status=e is BroadcastWaiting?e.Message:LiveBackoff.DelayMs>0?L.Choose("Source rate limit · retry in ","Лимит источника · повтор через ")+(int)Math.Ceiling(LiveBackoff.DelayMs/60000.0)+L.Choose(" min"," мин"):L.T("Поток недоступен · повтор через ")+Math.Max(retry/1000,Math.Min(60,failures*10))+L.Choose(" s"," с");}
                    Emit();retry=ReconnectDelay(e,failures,retry);
                }
                if(stop.WaitOne(retry))return;
            }
        }
        public static int ReconnectDelay(Exception error,int failures,int minimum){return error is BroadcastWaiting?5000:Math.Max(minimum,Math.Min(60000,failures*10000));}
        public void ApplyStatus(string json) {
            string message=Json.Str(Json.Obj(json),"message");
            if(message.Length==0)return;
            string next=message.IndexOf("not ready",StringComparison.OrdinalIgnoreCase)>=0
                ?L.Choose("Match found · waiting for Valve broadcast","Матч найден · ожидаем трансляцию Valve")
                :L.Choose("Connecting · ","Подключение · ")+message.Substring(0,Math.Min(110,message.Length));
            lock(gate){if(disposed)return;if(status!=next)Log.Write("Broadcast status: match="+matchId+" "+message.Substring(0,Math.Min(200,message.Length)));status=next;dirty=true;}
        }
        public bool ApplyRow(string json,DateTime now) {
            var d=Json.Obj(Json.Serializer().DeserializeObject(json));
            long? entity=Json.Number(d,"entity_index"),team=Json.Number(d,"team_id"),rowTick=Json.Number(d,"tick");
            if(!entity.HasValue||entity>100000||!rowTick.HasValue||(team!=2&&team!=3))return false;
            lock(gate) {
                if(disposed)return false;
                int id=(int)entity.Value;
                if(!rows.ContainsKey(id)&&rows.Count>=12)return false;
                // Reconnect can replay a prefix. Do not roll counters back to older ticks.
                if(rows.ContainsKey(id)&&(Json.Number(rows[id],"tick")??0)>rowTick)return false;
                string name;int hero=(int)(Json.Number(d,"hero_id")??0);
                d["hero_name"]=heroes.TryGetValue(hero,out name)?name:"Hero "+hero;
                d["is_local"]=false;rows[id]=d;updated[id]=now;tick=Math.Max(tick,rowTick.Value);
                bool first=rows.Count==12&&!logged;
                dirty=true;healthy=true;status=L.T("Трансляция · данные получены");
                if(first){logged=true;Log.Write("Live broadcast connected: match="+matchId+" players="+rows.Count+" tick="+tick);}
                return true;
            }
        }
        public string Packet(DateTime now) {
            return Json.Serializer().Serialize(PacketData(now));
        }
        public Dictionary<string,object> PacketData(DateTime now) {
            lock(gate) {
                var match=new Dictionary<string,object>();var ages=new Dictionary<string,object>();match["match_id"]=matchId.ToString();
                int slot=0;foreach(var entry in rows.OrderBy(x=>x.Key)) {
                    string key="roster_"+slot++;match[key]=new Dictionary<string,object>(entry.Value);ages[key]=(long)Math.Max(0,(now-updated[entry.Key]).TotalMilliseconds);
                }
                return new Dictionary<string,object>{{"session",session},{"sequence",++sequence},{"healthy",healthy},{"spectator",true},{"status",status},{"ages",ages},
                    {"info",new Dictionary<string,object>{{"game_info",new Dictionary<string,object>{{"phase",rows.Count>0?"GameInProgress":"Init"}}},{"match_info",match}}}};
            }
        }
        void Emit(){try{Dictionary<string,object> packet;lock(gate){if(disposed)return;var now=DateTime.UtcNow;if(!dirty&&(now-lastEmit).TotalSeconds<1)return;dirty=false;lastEmit=now;packet=PacketData(now);}if(disposed)return;var direct=SnapshotData;if(direct!=null)direct(packet);var h=Snapshot;if(h!=null)h(Json.Serializer().Serialize(packet));}catch(Exception e){if(!disposed)Log.Write("Broadcast publication",e);}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;stop.Set();if(timer!=null)timer.Dispose();else stop.Dispose();var r=request;if(r!=null)r.Abort();}}
    }
}







