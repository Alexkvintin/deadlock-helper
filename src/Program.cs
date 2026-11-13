using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DeadlockOverlay {
    static class Program {
        [STAThread] static void Main(string[] args) {
            if(args.Contains("--ui-review")){try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);UiReview.Run(args);return;}
            bool created;
            using(var mutex=new Mutex(true,"Local\\DeadlockOverlay.v01",out created)) {
                if(!created) { MessageBox.Show(L.T("Deadlock Helper уже запущен. Откройте настройки через значок в трее."));return; }
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException+=(s,e)=>Log.Write("UI error",e.Exception);
                AppDomain.CurrentDomain.UnhandledException+=(s,e)=>Log.Write("Fatal",e.ExceptionObject as Exception);
                try { using(var context=new OverlayApplication(args)) Application.Run(context); }
                catch(Exception e) { Log.Write("Startup",e); MessageBox.Show(e.Message,L.T("Deadlock Helper — ошибка")); }
            }
        }
    }
    sealed class HotkeyWindow : NativeWindow,IDisposable {
        public event Action Toggle;public event Action<int> TogglePart;
        public HotkeyWindow(){CreateHandle(new CreateParams());}
        public bool Register(int key){return Register(key,new int[6]);}
        public bool Register(int key,int[] parts){bool ok=true;for(int i=1;i<=7;i++)Native.UnregisterHotKey(Handle,i);ok=Native.RegisterHotKey(Handle,1,0x4000|0x2|0x4,(uint)key);for(int i=0;i<6;i++)if(parts[i]!=0)ok=Native.RegisterHotKey(Handle,i+2,0x4000|0x2|0x4,(uint)parts[i])&&ok;return ok;}
        protected override void WndProc(ref Message m){if(m.Msg==0x0312){int id=m.WParam.ToInt32();if(id==1&&Toggle!=null)Toggle();else if(id>=2&&id<=7&&TogglePart!=null)TogglePart(id-2);}base.WndProc(ref m);}
        public void Dispose(){for(int i=1;i<=7;i++)Native.UnregisterHotKey(Handle,i);DestroyHandle();}
    }
    sealed class OverlayApplication : ApplicationContext {
        readonly Settings settings;
        readonly MatchState state=new MatchState();
        readonly ItemAdvisor advisor=new ItemAdvisor();
        readonly HudAdvice hudAdvice=new HudAdvice();
        readonly HudOverlay hud;readonly HudOverlay[] mapWidgets=new HudOverlay[3];readonly LayoutHandle[] handles=new LayoutHandle[6];
        MapEvents mapEvents;
        readonly HeroImages images=new HeroImages();
        readonly WidgetManager widgets=new WidgetManager();
        readonly SettingsWindow window;
        readonly ScoutController scout;
        readonly MatchViewer viewer;
        readonly NotifyIcon tray;
        readonly HotkeyWindow hotkey=new HotkeyWindow();
        readonly System.Windows.Forms.Timer pulse=new System.Windows.Forms.Timer { Interval=50 };
        LiveBroadcastConnector real;
        long selectedMatch=-1;
        AutoMatchDetector detector;
        readonly OwnMatchDetector ownDetector=new OwnMatchDetector();
        readonly LocalGameSource localSource=new LocalGameSource();
        Dictionary<string,object> pendingLocal,latestLocal;
        bool localMode;DateTime localRead=DateTime.MinValue,localIdle=DateTime.MinValue;
        long? ownDetectedMatch;
        long? detectedMatch;
        string detectorError,autoError;
        readonly string localSteamId=ViewedHero.LocalSteamId(); string viewedName="";long viewedMatch;
        long clockMatch;int clockSeconds;DateTime clockRead=DateTime.MinValue;
        readonly DisplayClock displayClock=new DisplayClock();
        MatchSummary lastSummary;SummaryWindow summaryWindow;
        bool summaryIssued,matchCompleted,finalApplied;readonly FinalMatchMonitor finalMonitor=new FinalMatchMonitor();
        DemoConnector demo;
        readonly OverlayRenderer left,right;
        readonly object gate=new object();
        Dictionary<string,object> pendingReal;string pendingDemo;
        bool hidden,hotkeyOk,closing;
        string registeredKeys="";string appliedLanguage="";
        Rectangle lastGameBounds;
        bool cursorWasInteractive;DateTime nextMenuPaint=DateTime.MinValue;
        DateTime diagnostic=DateTime.UtcNow;
        readonly Stopwatch life=Stopwatch.StartNew();
        readonly int exitSeconds;
        public OverlayApplication(string[] args) {
            settings=SettingsManager.Load();L.Language=settings.Language;
            int matchArg=Array.IndexOf(args,"--match");long match;
            if(matchArg>=0&&matchArg+1<args.Length&&long.TryParse(args[matchArg+1],out match)&&match>0){settings.LiveMatchId=match;settings.AutoMatch=false;}
            if(settings.AutoMatch)settings.LiveMatchId=0;
            SettingsManager.Save(settings);
            ownDetector.Found+=id=>{lock(gate){if(!closing)ownDetectedMatch=id;}};
            ownDetector.Status+=message=>{lock(gate){if(!closing)detectorError=message;}};
            localSource.Snapshot+=packet=>{lock(gate){if(!closing)pendingLocal=packet;}};
            window=new SettingsWindow(settings,Changed,SetDemo);
            scout=new ScoutController(settings,window);
            var gameArea=Native.OverlayGameBounds();var gameScreen=gameArea.IsEmpty?null:Screen.FromRectangle(gameArea);
            var settingsScreen=gameScreen==null?Screen.AllScreens.FirstOrDefault(s=>!s.Primary):Screen.AllScreens.FirstOrDefault(s=>s.DeviceName!=gameScreen.DeviceName);
            if(settingsScreen!=null){window.StartPosition=FormStartPosition.Manual;var area=settingsScreen.WorkingArea;window.Location=new Point(area.Left+(area.Width-window.Width)/2,area.Top+(area.Height-window.Height)/2);}
            viewer=new MatchViewer(id=>{settings.AutoMatch=false;settings.LiveMatchId=id;SettingsManager.Save(settings);autoError=null;RestartLive();ShowViewer(id);});
            window.ViewMatchRequested+=ShowViewer;
            window.ReconnectRequested+=id=>{if(!state.Demo&&selectedMatch==id){autoError=null;RestartLive();}};
            window.EndSessionRequested+=EndSession;
            left=new OverlayRenderer(true,images);right=new OverlayRenderer(false,images);widgets.Add(left);widgets.Add(right);
            hud=new HudOverlay(hudAdvice);widgets.Add(hud);for(int i=0;i<3;i++){mapWidgets[i]=new HudOverlay(hudAdvice,i+3);widgets.Add(mapWidgets[i]);}for(int i=0;i<6;i++)handles[i]=new LayoutHandle(i);
            tray=new NotifyIcon { Text="Deadlock Helper by kvintin · v1.1.0-beta.1",Icon=Design.AppIcon,Visible=true };
            var menu=new ContextMenuStrip(); menu.Items.Add(L.T("Настройки"),null,(s,e)=>Open());menu.Items.Add(L.T("Последняя сводка"),null,(s,e)=>{if(lastSummary!=null)new SummaryWindow(lastSummary).Show();else Process.Start("explorer.exe",SettingsManager.Folder);}); menu.Items.Add(L.T("Скрыть / показать"),null,(s,e)=>{hidden=!hidden;Refresh();});menu.Items.Add(L.T("Выход"),null,(s,e)=>Application.Exit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Open();
            hotkey.Toggle+=()=>{hidden=!hidden;Refresh();};hotkey.TogglePart+=part=>{settings.TogglePart(part);SettingsManager.Save(settings);window.SyncVisibility();Refresh();};Changed();
            menu.Items.Insert(1,new ToolStripMenuItem(L.Choose("Match viewer","Просмотр матча"),null,(s,e)=>ShowViewer(selectedMatch)));
            menu.Items.Insert(2,new ToolStripMenuItem(L.Choose("End session and show report","Завершить сеанс и показать сводку"),null,(s,e)=>EndSession()));
            if(args.Contains("--demo")) window.SetDemoCheck(true);
            if(args.Contains("--diagnostics")) settings.Diagnostics=true;
            int at=Array.IndexOf(args,"--exit-after");if(at>=0 && at+1<args.Length)int.TryParse(args[at+1],out exitSeconds);
            pulse.Tick+=(s,e)=>Tick();pulse.Start();
            if(!args.Contains("--minimized")) window.Show();
            if(args.Contains("--viewer"))ShowViewer(settings.LiveMatchId);
            Application.ApplicationExit+=(s,e)=>Cleanup();
        }
        void Open() { window.Show();window.WindowState=FormWindowState.Normal;window.Activate(); }
        void ShowViewer(long id){viewer.SelectMatch(id);viewer.Show();viewer.WindowState=FormWindowState.Normal;Refresh();}
        void Changed() { ownDetector.Enabled=settings.AutoMatch&&!state.Demo;cursorWasInteractive=false;L.Language=settings.Language;if(tray!=null&&tray.ContextMenuStrip!=null)foreach(ToolStripItem item in tray.ContextMenuStrip.Items)item.Text=L.ToLanguage(item.Text);if(appliedLanguage!=settings.Language){appliedLanguage=settings.Language;foreach(Form form in Application.OpenForms)L.Apply(form);}string keys=settings.Hotkey+":"+string.Join(",",settings.PartHotkeys);if(registeredKeys!=keys){hotkeyOk=hotkey.Register(settings.Hotkey,settings.PartHotkeys);registeredKeys=keys;}
            if(!state.Demo&&detector==null){if(settings.AutoMatch)settings.LiveMatchId=0;var d=new AutoMatchDetector();detector=d;d.MatchChanged+=id=>{lock(gate){if(ReferenceEquals(detector,d))detectedMatch=id;}};d.Status+=message=>{lock(gate)detectorError=message;};d.ClockRead+=(id,seconds)=>{lock(gate){if(ReferenceEquals(detector,d)){clockMatch=id;clockSeconds=seconds;clockRead=DateTime.UtcNow;}}};d.ViewRead+=(id,text)=>{lock(gate){if(ReferenceEquals(detector,d)){viewedMatch=id;viewedName=text;}}};}
            if(state.Demo&&detector!=null){detector.Dispose();detector=null;lock(gate)detectedMatch=null;}
            if((selectedMatch!=settings.LiveMatchId||localMode&&!settings.AutoMatch)&&!state.Demo){FinishSummary(L.T("Переход к другому матчу / отключение"));RestartLive();} Refresh(); }
        void RestartLive() {
            localMode=false;latestLocal=null;localIdle=DateTime.MinValue;
            lock(gate){clockMatch=0;clockRead=DateTime.MinValue;}
            if(mapEvents!=null){mapEvents.Dispose();mapEvents=null;}
            if(real!=null){real.Dispose();real=null;}state.Reset();summaryIssued=false;matchCompleted=false;finalApplied=false;summaryWindow=null;selectedMatch=settings.LiveMatchId;
            lock(gate)pendingReal=null;
            if(selectedMatch>0){var connector=new LiveBroadcastConnector(selectedMatch);real=connector;connector.SnapshotData+=packet=>{lock(gate){if(!closing&&ReferenceEquals(real,connector))pendingReal=packet;}};connector.Start();mapEvents=new MapEvents(selectedMatch);mapEvents.Start();}
        }
        void SetDemo(bool on) {
            localMode=false;latestLocal=null;lock(gate)pendingLocal=null;
            if(mapEvents!=null){mapEvents.Dispose();mapEvents=null;}
            if(demo!=null){demo.Dispose();demo=null;} lock(gate){pendingDemo=null;pendingReal=null;} state.Reset(); state.Demo=on;
            if(real!=null){real.Dispose();real=null;}
            if(on){demo=new DemoConnector();demo.Snapshot+=json=>{lock(gate)pendingDemo=json;};demo.Start();}
            else RestartLive();
            Changed();Refresh();
        }
        void Tick() {
            localSource.Enabled=settings.AutoMatch&&!state.Demo;
            HandleLocal();
            ownDetector.Enabled=settings.AutoMatch&&!state.Demo&&!localMode;
            long? detected;string error;lock(gate){detected=detectedMatch??ownDetectedMatch;detectedMatch=null;ownDetectedMatch=null;error=detectorError;detectorError=null;}
            if(!state.Demo&&!localMode&&settings.AutoMatch&&detected.HasValue&&detected.Value!=selectedMatch){FinishSummary(detected.Value==0?L.T("Выход из матча обнаружен"):L.T("Переход к следующему матчу"));autoError=null;settings.LiveMatchId=detected.Value;SettingsManager.Save(settings);RestartLive();}
            if(error!=null&&selectedMatch<=0){state.SourceStatus=error;autoError=error;}
            Dictionary<string,object> packet;string demoPacket;lock(gate){packet=pendingReal;demoPacket=pendingDemo;pendingDemo=null;pendingReal=null;}
            if(!matchCompleted&&!localMode)try{if(state.Demo&&demoPacket!=null)state.Apply(demoPacket,DateTime.UtcNow);else if(!state.Demo&&packet!=null)state.Apply(packet,DateTime.UtcNow);}catch(Exception e){Log.Write("Invalid snapshot",e);state.Healthy=false;state.SourceStatus=L.T("Ошибка формата данных");}
            scout.ObserveState(state);
            var map=mapEvents!=null?mapEvents.Snapshot(DateTime.UtcNow,state.GameTick):new MapSnapshot();
            if(localMode&&latestLocal!=null){map=LocalSnapshot.Map(latestLocal);map.Fresh=(DateTime.UtcNow-localRead).TotalSeconds<2;if(map.Fresh&&!map.Paused&&map.Seconds.HasValue)map.Seconds+=(DateTime.UtcNow-localRead).TotalSeconds;}
            lock(gate){if(!localMode&&clockMatch==selectedMatch&&(DateTime.UtcNow-clockRead).TotalSeconds<12){map.Seconds=clockSeconds+(map.Paused?0:(DateTime.UtcNow-clockRead).TotalSeconds);map.ScreenClock=true;map.Fresh=true;}}
            map.Seconds=displayClock.Update(state.MatchId,map.Seconds,map.Paused,DateTime.UtcNow);
            var own=state.Players.Values.FirstOrDefault(p=>localSteamId.Length>0&&p.SteamId==localSteamId);state.ViewedHeroId=own==null?0:own.Hero;
            if(detector!=null){detector.Suspended=localMode||settings.EditLayout||!settings.ScreenRecognition||(!settings.AutoMatch&&viewer.Visible);detector.ReadViewEnabled=own==null&&settings.ShowItems;}
            lock(gate){if(own==null&&viewedMatch==selectedMatch){var viewed=ViewedHero.Match(state,viewedName);if(viewed!=null)state.ViewedHeroId=viewed.Hero;}}
            if(localMode&&latestLocal!=null)state.ViewedHeroId=(int)(Json.Number(latestLocal,"viewed_hero")??0);
            map.Ended|=matchCompleted;
            if(!localMode&&!finalApplied&&finalMonitor.Update(state,map)){
                if(!summaryIssued)FinishSummary(L.T("Матч завершён · итоговые данные"));
                else {lastSummary=MatchSummary.Capture(state,L.T("Матч завершён · итоговые данные"));try{lastSummary.Save();}catch(Exception e){Log.Write("Final summary save",e);}if(summaryWindow!=null&&!summaryWindow.IsDisposed)summaryWindow.UpdateSummary(lastSummary);}
                finalApplied=true;StopCompletedMatch();
            }else if(map.Ended&&!matchCompleted){FinishSummary(localMode?L.Choose("Match completed · local statistics","Матч завершён · локальная статистика"):L.T("Матч завершён · ожидаем итоговые данные"));StopCompletedMatch();}
            hud.Map=map;foreach(var widget in mapWidgets)widget.Map=map;if(!viewer.Visible)hudAdvice.Update(state,map,settings);Refresh();
            if(settings.Diagnostics && (DateTime.UtcNow-diagnostic).TotalSeconds>=30) {
                diagnostic=DateTime.UtcNow;using(var p=Process.GetCurrentProcess())Log.Write(string.Format("Resources mode={0} workingSetMB={1:F1} privateMB={2:F1} cpuSeconds={3:F3} handles={4} paints={5}",state.Demo?"demo":"live",p.WorkingSet64/1048576.0,p.PrivateMemorySize64/1048576.0,p.TotalProcessorTime.TotalSeconds,p.HandleCount,left.PaintCount+right.PaintCount));
            }
            if(exitSeconds>0 && life.Elapsed.TotalSeconds>=exitSeconds) Application.Exit();
        }
        void HandleLocal(){
            Dictionary<string,object> raw;lock(gate){raw=pendingLocal;pendingLocal=null;}
            if(!settings.AutoMatch||state.Demo||raw==null)return;
            scout.Observe(raw);
            string kind=Json.Str(raw,"kind");
            if(kind=="unavailable"){if(localMode){state.Healthy=false;state.SourceStatus=L.Choose("Local source unavailable · reconnecting","Локальный источник недоступен · переподключение");}return;}
            if(kind=="idle"||(kind=="snapshot"&&Json.Number(raw,"match_id")==0)){
                if(!localMode)return;if(localIdle==DateTime.MinValue)localIdle=DateTime.UtcNow;
                if((DateTime.UtcNow-localIdle).TotalSeconds<1)return;
                FinishSummary(L.Choose("Left match · last local data","Выход из матча · последние локальные данные"));
                state.Reset();latestLocal=null;selectedMatch=0;settings.LiveMatchId=0;matchCompleted=false;return;
            }
            if(localMode&&Json.Number(raw,"match_id")==selectedMatch&&LocalSnapshot.Ended(Json.Str(raw,"phase"))&&LocalSnapshot.Packet(raw)==null){
                FinishSummary(L.Choose("Match completed · last local statistics","Матч завершён · последние локальные показатели"));StopCompletedMatch();latestLocal=null;return;
            }
            var packet=LocalSnapshot.Packet(raw);if(packet==null)return;localIdle=DateTime.MinValue;
            long id=Json.Number(raw,"match_id").Value;
            if(!localMode||id!=selectedMatch){
                if(id!=selectedMatch)FinishSummary(L.Choose("Changed match","Переход к другому матчу"));
                if(real!=null){real.Dispose();real=null;}if(mapEvents!=null){mapEvents.Dispose();mapEvents=null;}
                lock(gate)pendingReal=null;state.Reset();summaryIssued=false;matchCompleted=false;finalApplied=false;summaryWindow=null;
                selectedMatch=id;settings.LiveMatchId=id;localMode=true;autoError=null;
                Log.Write("Local source connected: match="+id+" players="+(Json.Get(raw,"players") as object[]??new object[0]).Length);
            }
            latestLocal=raw;localRead=DateTime.UtcNow;
            if(!matchCompleted){state.Apply(packet,localRead);state.WinningTeam=(int?)Json.Number(raw,"winner");var seconds=LocalSnapshot.Number(raw,"seconds");if(seconds.HasValue)state.DurationSeconds=(long)Math.Max(0,seconds.Value);}
        }
        void FinishSummary(string reason){
            if(state.Demo||summaryIssued||state.Players.Count==0)return;
            summaryIssued=true;lastSummary=MatchSummary.Capture(state,reason);
            try{lastSummary.Save();}catch(Exception e){Log.Write("Summary save",e);}
            if(settings.ShowSummary){summaryWindow=new SummaryWindow(lastSummary);summaryWindow.Show();}
        }
        void StopCompletedMatch(){matchCompleted=true;state.Phase="PostGame";if(real!=null){real.Dispose();real=null;}if(mapEvents!=null){mapEvents.Dispose();mapEvents=null;}lock(gate)pendingReal=null;}
        void EndSession(){
            if(state.Demo||state.Players.Count==0)return;
            FinishSummary(L.Choose("Session ended · last received data","Сеанс завершён · последние полученные данные"));
            StopCompletedMatch();Refresh();
        }
        void Refresh() {
            if(closing)return;
            viewer.UpdateState(state,selectedMatch,finalApplied);
            // Limit layered surface updates to 4 Hz while the
            // player is using a menu; never freeze newly available widgets indefinitely.
            bool interactive=!state.Demo&&!settings.EditLayout&&Native.CursorInteraction();
            if(!viewer.Visible&&interactive&&cursorWasInteractive&&state.Active&&settings.Enabled&&!hidden&&(left.Visible||right.Visible)&&DateTime.UtcNow<nextMenuPaint)return;
            cursorWasInteractive=interactive;if(interactive)nextMenuPaint=DateTime.UtcNow.AddMilliseconds(250);
            Rectangle bounds=state.Demo?Screen.PrimaryScreen.Bounds:state.Active&&settings.Enabled&&!hidden?Native.OverlayGameBounds():Rectangle.Empty;
            if(!bounds.IsEmpty)lastGameBounds=bounds;else if(state.Active)bounds=lastGameBounds;
            if(settings.EditLayout)bounds=lastGameBounds.IsEmpty?Screen.PrimaryScreen.Bounds:lastGameBounds;bool show=!viewer.Visible&&(settings.EditLayout||(settings.Enabled&&!hidden&&state.Active&&!bounds.IsEmpty));
            // A transport outage is displayed briefly, then hidden because match lifetime is unknown.
            // Preserve the panels across Alt+Tab; stale values are rendered as unknown.
            widgets.Refresh(state,settings,bounds,show);Form[] forms={left,right,hud,mapWidgets[0],mapWidgets[1],mapWidgets[2]};for(int i=0;i<6;i++)handles[i].Update(settings,bounds,forms[i].Bounds);
            string text=state.Demo?L.T("ДЕМО · данные сгенерированы"):settings.AutoMatch&&selectedMatch<=0&&autoError!=null?autoError:settings.AutoMatch&&settings.LiveMatchId==0?L.T("Автоопределение: откройте матч в Deadlock"):state.SourceStatus;
            if(state.Active&&!state.LocalTeam.HasValue&&!state.Spectator)text+=L.T("\nОжидание определения вашей команды");
            else text+="\n"+(hidden?L.T("Скрыто горячей клавишей"):state.Active?L.T("Матч: ")+state.MatchId:L.T("Ожидание начала матча"));
            if(!hotkeyOk)text=L.T("Горячая клавиша занята — выберите другую.\n")+text;
            window.SetStatus(text);
        }
        void Cleanup() { if(closing)return;closing=true;scout.Dispose();localSource.Dispose();ownDetector.Dispose();foreach(var handle in handles)handle.Dispose();hudAdvice.Dispose();if(mapEvents!=null)mapEvents.Dispose();advisor.Dispose();pulse.Stop();pulse.Dispose();if(detector!=null)detector.Dispose();if(real!=null)real.Dispose();if(demo!=null)demo.Dispose();hotkey.Dispose();tray.Visible=false;var menu=tray.ContextMenuStrip;tray.Dispose();menu.Dispose();widgets.Dispose();images.Dispose();viewer.Dispose();window.Dispose(); }
        protected override void Dispose(bool disposing) { if(disposing)Cleanup();base.Dispose(disposing); }
    }
}















