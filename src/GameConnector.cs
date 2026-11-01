using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeadlockOverlay {
    public interface IGameConnector : IDisposable { event Action<string> Snapshot; void Start(); }
    // Loopback-only, bounded HTTP/1.1 endpoint. No URL ACL, service, administrator or firewall rule.
    public sealed class LocalSnapshotConnector : IGameConnector {
        public const int Port=18761;
        public event Action<string> Snapshot;
        readonly string token;
        TcpListener listener;
        volatile bool stopped;
        readonly SemaphoreSlim clients=new SemaphoreSlim(4);
        public LocalSnapshotConnector(string token) { this.token=token; }
        public void Start() { listener=new TcpListener(IPAddress.Loopback,Port); listener.Start(8); Accept(); }
        async void Accept() {
            try { while(!stopped) {
                var c=await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                if(!clients.Wait(0)) { c.Close(); continue; }
                ThreadPool.QueueUserWorkItem(_=>Handle(c));
            } } catch(Exception e) { if(!stopped) Log.Write("Bridge accept",e); }
        }
        void Handle(TcpClient client) {
            using(client) try {
                client.ReceiveTimeout=1500; client.SendTimeout=1500;
                using(var stream=client.GetStream()) {
                    var header=new List<byte>(); int b;
                    while(header.Count<8192 && (b=stream.ReadByte())!=-1) {
                        header.Add((byte)b); int n=header.Count;
                        if(n>=4 && header[n-4]==13 && header[n-3]==10 && header[n-2]==13 && header[n-1]==10) break;
                    }
                    string h=Encoding.ASCII.GetString(header.ToArray());
                    if(!h.EndsWith("\r\n\r\n")) { Reply(stream,400); return; }
                    string[] lines=h.Split(new[]{"\r\n"},StringSplitOptions.None);
                    if(lines[0]!="POST /events HTTP/1.1") { Reply(stream,404); return; }
                    int length=-1; bool bad=false;
                    foreach(string line in lines) {
                        if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) {
                            int v; if(length!=-1 || !int.TryParse(line.Substring(15).Trim(),out v)) bad=true; else length=v;
                        }
                        if(line.StartsWith("Transfer-Encoding:",StringComparison.OrdinalIgnoreCase)) bad=true;
                    }
                    if(bad || length<1 || length>131072) { Reply(stream,413); return; }
                    byte[] data=new byte[length]; int read=0;
                    while(read<length) { int n=stream.Read(data,read,length-read); if(n==0) throw new IOException("Truncated body"); read+=n; }
                    string json=new UTF8Encoding(false,true).GetString(data);
                    var obj=Json.Obj(Json.Serializer().DeserializeObject(json));
                    if(!Equal(Json.Str(obj,"token"),token)) { Reply(stream,403); return; }
                    var handler=Snapshot; if(handler!=null) handler(json);
                    Reply(stream,200);
                }
            } catch(Exception e) { if(!stopped && !(e is IOException)) Log.Write("Rejected bridge message",e); }
            finally { clients.Release(); }
        }
        static bool Equal(string a,string b) { if(a.Length!=b.Length) return false; int d=0; for(int i=0;i<a.Length;i++) d|=a[i]^b[i]; return d==0; }
        static void Reply(Stream s,int code) {
            byte[] b=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Rejected")+"\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); s.Write(b,0,b.Length);
        }
        public void Dispose() { stopped=true; if(listener!=null) listener.Stop(); }
    }
    public sealed class DemoConnector : IGameConnector {
        public event Action<string> Snapshot;
        System.Threading.Timer timer;
        long sequence;
        readonly string session=Guid.NewGuid().ToString("N");
        public void Start() { timer=new System.Threading.Timer(_=>Emit(),null,0,1000); }
        void Emit() { var h=Snapshot; if(h!=null) h(Create(session,Interlocked.Increment(ref sequence))); }
        public static string Create(string session,long seq) {
            string[] names={"Infernus","Seven","Vindicta","Lady Geist","Abrams","Wraith","McGinnis","Paradox","Dynamo","Kelvin","Haze","Warden"};
            int[] heroes={1,2,3,4,6,7,8,10,11,12,13,25};
            var match=new Dictionary<string,object>(); var ages=new Dictionary<string,object>();
            match["match_id"]="demo-6v6";
            for(int i=0;i<12;i++) {
                match["roster_"+i]=new { player_name=i==2?"You / Демо":"Player "+(i+1), hero_name=names[i], hero_id=heroes[i], team_id=i<6?2:3, is_local=i==2, hero_damage=1234L+i*917+seq*(73+i*13), object_damage=i*137+seq*17,hero_healing=i*97+seq*7,kills=i%7,deaths=i%4,assists=2+i,souls=8000+i*555+seq*80 };
                ages["roster_"+i]=0;
            }
            return Json.Serializer().Serialize(new { session=session, sequence=seq, healthy=true, status="ДЕМО", ages=ages, info=new { game_info=new { phase="GameInProgress" }, match_info=match } });
        }
        public void Dispose() { if(timer!=null) timer.Dispose(); }
    }
}
