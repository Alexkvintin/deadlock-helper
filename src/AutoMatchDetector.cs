using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Tesseract;

namespace DeadlockOverlay {
    // Recognition is restricted to the visible bottom-right match label. Nothing is uploaded.
    public sealed class AutoMatchDetector : IDisposable {
        readonly System.Threading.Timer timer;
        TesseractEngine engine,viewEngine;
        int busy,disposed;
        public volatile bool Suspended,ReadViewEnabled=true;
        public event Action<long> MatchChanged;
        public event Action<string> Status;
        public event Action<long,int> ClockRead;
        public event Action<long,string> ViewRead;
        public readonly DetectionTracker Tracker=new DetectionTracker();
        DateTime nextScan=DateTime.MinValue;
        string lastStatus="";
        void Report(string text){if(text==lastStatus)return;lastStatus=text;var handler=Status;if(handler!=null)handler(text);}
        public AutoMatchDetector(){timer=new System.Threading.Timer(_=>Scan(),null,200,750);}
        public static long ParseId(string text) {
            // Require the match-label separator and an isolated plausible ID, never just arbitrary HUD digits.
            var match=Regex.Match(text??"",@"(?:MATCH|MAT[CS]H|MAT4|МАТЧ|МАТС|МАТН|МАТШ|МАТСН|МАТЧІ|МАTЧ|MATY|MATU|MATH)\s*[:;.]?\s*([1-9](?:[ \t]*[0-9]){7,9})(?![ \t]*[0-9])",RegexOptions.IgnoreCase);
            long id;return match.Success&&long.TryParse(Regex.Replace(match.Groups[1].Value,@"\s", ""),out id)?id:0;
        }
        void Scan(){
            if(Interlocked.Exchange(ref busy,1)!=0)return;
            try {
                if(Volatile.Read(ref disposed)!=0||Suspended)return;
                var bounds=Native.GameBounds();
                if(bounds.IsEmpty){
                    bool running=false;foreach(var p in Process.GetProcessesByName("deadlock")){running=true;p.Dispose();}foreach(var p in Process.GetProcessesByName("project8")){running=true;p.Dispose();}
                    if(!running){Observe(0,true);Report(L.Choose("Waiting for Deadlock","Ожидание запуска Deadlock"));}else {Tracker.Interrupt();Report(L.Choose("Bring Deadlock to the foreground to read the match ID","Откройте окно Deadlock для чтения номера матча"));}
                    return;
                }
                if(bounds.Width<640||bounds.Height<400)return;
                // Spectator mode can leave the cursor visible permanently. Only the small
                // footer/hero/clock are sampled at the slower 4-second cadence then.
                bool interactive=Native.CursorInteraction();
                if(DateTime.UtcNow<nextScan){if(!interactive&&Tracker.Current>0)ReadView(bounds,Tracker.Current);return;}
                nextScan=DateTime.UtcNow.AddMilliseconds(interactive?4000:Tracker.Current>0?3000:750);
                if(engine==null){Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT","1");engine=new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tessdata"),"eng+rus+ukr",EngineMode.LstmOnly);engine.SetVariable("debug_file","NUL");}
                int width=Math.Min(bounds.Width,(int)(bounds.Width*0.22)),height=Math.Max(38,(int)(bounds.Height*0.035));
                using(var capture=new Bitmap(width,height,PixelFormat.Format24bppRgb)) {
                    using(var g=Graphics.FromImage(capture))g.CopyFromScreen(bounds.Right-width,bounds.Bottom-height,0,0,capture.Size);
                    // Verify focus didn't change during capture; do not process another application's pixels.
                    if(Native.GameBounds()!=bounds){Tracker.Interrupt();return;}
                    using(var scaled=new Bitmap(width*3,height*3,PixelFormat.Format24bppRgb)) {
                        using(var g=Graphics.FromImage(scaled)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(capture,new Rectangle(0,0,scaled.Width,scaled.Height));}
                        using(var memory=new MemoryStream()) {
                            scaled.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
                            using(var pix=Pix.LoadFromMemory(memory.ToArray()))using(var page=engine.Process(pix,PageSegMode.SparseText)) {
                                string text=page.GetText();long id=ParseId(text);
                                Observe(id,false);
                                Report(id>0?L.Choose("Match ID detected: ","Распознан номер матча: ")+id:L.Choose("Waiting for a visible match ID in the bottom-right corner","Ожидаем видимый номер матча в правом нижнем углу"));
                                if(id>0){page.Dispose();ReadClock(bounds,id,interactive);ReadView(bounds,id,interactive);}
                            }
                        }
                    }
                }
            }catch(Exception e){var h=Status;if(h!=null)h(L.T("Автоопределение недоступно: ")+e.Message);Log.Write("Auto match detection",e);}
            finally{if(Volatile.Read(ref disposed)!=0&&engine!=null){engine.Dispose();engine=null;if(viewEngine!=null){viewEngine.Dispose();viewEngine=null;}}Volatile.Write(ref busy,0);}
        }
        void Observe(long id,bool closed){long? change=Tracker.Observe(id,closed);if(change.HasValue){Log.Write("Automatic match detection: "+change.Value);var h=MatchChanged;if(h!=null)h(change.Value);}}
        void ReadView(Rectangle bounds,long id,bool allowCursor=false){
            if(Suspended||!ReadViewEnabled||(!allowCursor&&Native.CursorInteraction())||Native.GameBounds()!=bounds)return;
            int width=(int)(bounds.Width*.125),height=(int)(bounds.Height*.029);
            using(var shot=new Bitmap(width,height,PixelFormat.Format24bppRgb)){
                using(var g=Graphics.FromImage(shot))g.CopyFromScreen(bounds.Right-width,bounds.Top+(int)(bounds.Height*.036),0,0,shot.Size);
                if(Native.GameBounds()!=bounds||(!allowCursor&&Native.CursorInteraction()))return;
                // Isolate the pale spectator label from the moving scene behind it.
                for(int y=0;y<shot.Height;y++)for(int x=0;x<shot.Width;x++){var c=shot.GetPixel(x,y);shot.SetPixel(x,y,c.R>195&&c.G>185&&c.B>155&&Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<65?Color.Black:Color.White);}
                using(var large=new Bitmap(width*3,height*3))using(var memory=new MemoryStream()){
                    using(var g=Graphics.FromImage(large))g.DrawImage(shot,new Rectangle(0,0,large.Width,large.Height));large.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
                    if(viewEngine==null)viewEngine=new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tessdata"),"eng",EngineMode.LstmOnly);
                    using(var pix=Pix.LoadFromMemory(memory.ToArray())){string read="";float confidence;
                        using(var page=viewEngine.Process(pix,PageSegMode.SingleLine)){confidence=page.GetMeanConfidence();if(confidence>.55)read=page.GetText();}
                        if(confidence<.8)using(var page=engine.Process(pix,PageSegMode.SingleLine)){if(page.GetMeanConfidence()>.55)read+="\n"+page.GetText();}
                        var h=ViewRead;if(h!=null)h(id,read);
                    }
                }
            }
        }
        void ReadClock(Rectangle bounds,long id,bool allowCursor=false){
            if(Suspended||(!allowCursor&&Native.CursorInteraction())||Native.GameBounds()!=bounds)return;
            int width=Math.Max(64,(int)(bounds.Width*.045)),height=Math.Max(24,(int)(bounds.Height*.028));
            using(var shot=new Bitmap(width,height,PixelFormat.Format24bppRgb)){
                using(var g=Graphics.FromImage(shot))g.CopyFromScreen(bounds.Left+(bounds.Width-width)/2,bounds.Top+(int)(bounds.Height*.019),0,0,shot.Size);
                if(Native.GameBounds()!=bounds||(!allowCursor&&Native.CursorInteraction()))return;
                using(var large=new Bitmap(width*3,height*3))using(var memory=new MemoryStream()){
                    using(var g=Graphics.FromImage(large))g.DrawImage(shot,new Rectangle(0,0,large.Width,large.Height));large.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
                    engine.SetVariable("tessedit_char_whitelist","0123456789:");
                    try{using(var pix=Pix.LoadFromMemory(memory.ToArray()))using(var page=engine.Process(pix,PageSegMode.SingleLine)){var m=Regex.Match(page.GetText().Trim(),@"^([0-9]{1,3}):([0-5][0-9])$");if(m.Success&&page.GetMeanConfidence()>.65){int seconds=int.Parse(m.Groups[1].Value)*60+int.Parse(m.Groups[2].Value);var h=ClockRead;if(seconds<14400&&h!=null)h(id,seconds);}}}
                    finally{engine.SetVariable("tessedit_char_whitelist","");}
                }
            }
        }
        public void Dispose(){Interlocked.Exchange(ref disposed,1);timer.Dispose();if(Interlocked.CompareExchange(ref busy,1,0)==0){if(engine!=null){engine.Dispose();engine=null;if(viewEngine!=null){viewEngine.Dispose();viewEngine=null;}}Volatile.Write(ref busy,0);}}
    }
    public sealed class DetectionTracker {
        public long Current {get;private set;}
        long candidate;int confirmations,missing;
        public void Interrupt(){confirmations=0;missing=0;candidate=0;}
        public long? Observe(long id,bool closed){
            if(closed){Interrupt();if(Current!=0){Current=0;return 0;}return null;}
            // Hidden HUD / pause is not a match-end signal. Keep the confirmed session.
            if(id==0){confirmations=0;candidate=0;missing++;return null;}
            missing=0;if(id==Current){confirmations=0;return null;}
            if(candidate!=id){candidate=id;confirmations=1;return null;}
            if(++confirmations>=2){Current=id;confirmations=0;return id;}return null;
        }
    }
}




