using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeadlockOverlay {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct CURSORINFO { public int Size,Flags; public IntPtr Cursor; public POINT Position; }
        [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO info);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        public static bool CursorInteraction() {
            var info=new CURSORINFO { Size=Marshal.SizeOf(typeof(CURSORINFO)) };
            // Fail closed: never leave composited panels above an interactive game cursor.
            return !GetCursorInfo(ref info)||(info.Flags&1)!=0||(GetAsyncKeyState(0x09)&0x8000)!=0;
        }
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref POINT p);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
        public static Rectangle GameBounds() {
            IntPtr h=GetForegroundWindow(); uint id; GetWindowThreadProcessId(h,out id);
            try { using(var p=Process.GetProcessById((int)id)) { if(!string.Equals(p.ProcessName,"deadlock",StringComparison.OrdinalIgnoreCase) && !string.Equals(p.ProcessName,"project8",StringComparison.OrdinalIgnoreCase)) return Rectangle.Empty; } }
            catch { return Rectangle.Empty; }
            RECT r; POINT pt=new POINT();
            if(!GetClientRect(h,out r)||!ClientToScreen(h,ref pt)) return Rectangle.Empty;
            return new Rectangle(pt.X,pt.Y,r.Right-r.Left,r.Bottom-r.Top);
        }
        static DateTime nextWindowLookup;static IntPtr gameWindow;
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        // Overlay placement may follow a background game. OCR still uses foreground-only GameBounds.
        public static Rectangle OverlayGameBounds(){
            if(DateTime.UtcNow>=nextWindowLookup){nextWindowLookup=DateTime.UtcNow.AddSeconds(2);gameWindow=IntPtr.Zero;
                foreach(string name in new[]{"deadlock","project8"})foreach(var p in Process.GetProcessesByName(name))using(p){try{if(p.MainWindowHandle!=IntPtr.Zero)gameWindow=p.MainWindowHandle;}catch{}}
            }
            if(gameWindow==IntPtr.Zero||!IsWindow(gameWindow)||IsIconic(gameWindow))return Rectangle.Empty;
            RECT r;POINT pt=new POINT();if(!GetClientRect(gameWindow,out r)||!ClientToScreen(gameWindow,ref pt)||r.Right-r.Left<320||r.Bottom-r.Top<250)return Rectangle.Empty;
            return new Rectangle(pt.X,pt.Y,r.Right-r.Left,r.Bottom-r.Top);
        }
    }
    public interface IOverlayWidget : IDisposable { void Refresh(MatchState state, Settings settings, Rectangle bounds, bool show); }
    public sealed class WidgetManager : IDisposable {
        readonly List<IOverlayWidget> widgets=new List<IOverlayWidget>();
        public void Add(IOverlayWidget widget) { widgets.Add(widget); }
        public void Refresh(MatchState state,Settings settings,Rectangle bounds,bool show) { foreach(var w in widgets) w.Refresh(state,settings,bounds,show); }
        public void Dispose() { foreach(var w in widgets) w.Dispose(); }
    }
    public sealed class HeroImages : IDisposable {
        readonly Dictionary<int,Image> cache=new Dictionary<int,Image>();
        int revision;
        public Image Get(int id) {
            if(revision!=AssetLibrary.Revision){Dispose();revision=AssetLibrary.Revision;}
            Image image; if(cache.TryGetValue(id,out image)) return image;
            string path=AssetLibrary.Resolve(id+".png");
            try { if(File.Exists(path)) using(var source=Image.FromFile(path)) image=new Bitmap(source,48,48); } catch(Exception e) { Log.Write("Hero icon",e); }
            cache[id]=image; return image;
        }
        public void Dispose() { foreach(var i in cache.Values) if(i!=null) i.Dispose(); cache.Clear(); }
    }
    public sealed class OverlayRenderer : AlphaWindow,IOverlayWidget {
        readonly bool friendly;
        readonly HeroImages images;
        readonly ArtCache art=new ArtCache();
        readonly Font heading=new Font("Segoe UI",9,FontStyle.Bold,GraphicsUnit.Pixel);
        readonly Font normal=new Font("Segoe UI Semibold",13,FontStyle.Bold,GraphicsUnit.Pixel);
        readonly Font small=new Font("Segoe UI",11,FontStyle.Bold,GraphicsUnit.Pixel);
        readonly Font totalFont=new Font("Segoe UI",20,FontStyle.Bold,GraphicsUnit.Pixel);
        readonly StringFormat ellipsis=new StringFormat { Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap };
        readonly StringFormat right=new StringFormat { Alignment=StringAlignment.Far, FormatFlags=StringFormatFlags.NoWrap };
        Settings settings;
        TeamStats stats;
        bool[] available=new bool[0];
        bool[] objectAvailable=new bool[0];
        bool spectator;int displayedTeam;
        bool[] healingAvailable=new bool[0];
        long? healingTotal;
        string signature="";
        Advice advice=new Advice();
        int logicalHeight=490;
        public long PaintCount { get; private set; }
        public OverlayRenderer(bool friendly,HeroImages images) {
            this.friendly=friendly; this.images=images; Text=friendly?"Deadlock · Your team":"Deadlock · Opponents";
            StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; BackColor=Theme.Background;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
            AutoScaleMode=AutoScaleMode.None;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x00080000|0x00000020|0x08000000|0x00000080; return p; } }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x0084) { m.Result=new IntPtr(-1); return; }
            if(m.Msg==0x0021) { m.Result=new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        public void Refresh(MatchState state,Settings s,Rectangle bounds,bool show) {
            if(!show || (!state.LocalTeam.HasValue&&!state.Spectator&&!s.EditLayout) || bounds.Width<320 || bounds.Height<250) { if(Visible) Hide(); return; }
            settings=s;spectator=state.Spectator||s.EditLayout;advice=state.Advice;logicalHeight=490; int leftTeam=s.EditLayout?2:HudOverlay.LeftTeam(state);int team=friendly?leftTeam:5-leftTeam;displayedTeam=team;
            if(!s.ShowHudPart(team-2)&&!s.EditLayout){if(Visible)Hide();return;}
            DateTime now=DateTime.UtcNow;
            stats=StatsProcessor.Team(state,team,s.Sort,now);
            available=stats.Players.Select(p=>StatsProcessor.Available(state,p,now)).ToArray();
            objectAvailable=stats.Players.Select(p=>state.Healthy&&state.Connected(now)&&(now-p.Updated).TotalSeconds<15&&p.ObjectDamage.HasValue).ToArray();
            healingAvailable=stats.Players.Select(p=>state.Healthy&&state.Connected(now)&&(now-p.Updated).TotalSeconds<15&&p.Healing.HasValue).ToArray();
            healingTotal=stats.Players.Length==6&&healingAvailable.All(valid=>valid)?(long?)stats.Players.Sum(p=>p.Healing.Value):null;
            var placement=s.Placement(team-2);double scale=DisplayLayout.PanelScale(bounds,placement.Scale);
            Size size=new Size((int)(250*scale),(int)(logicalHeight*scale));
            double x=placement.X, y=placement.Y;
            Rectangle desired=new Rectangle(bounds.Left+(int)((bounds.Width-size.Width)*x),bounds.Top+(int)((bounds.Height-size.Height)*y),size.Width,size.Height);
            desired=WidgetLayout.Bounds(s,bounds,friendly?0:1,leftTeam);bool geometry=Bounds!=desired;if(geometry)Bounds=desired;
            BackgroundOpacity=placement.Opacity;
            string sig=Json.Serializer().Serialize(new { L.Language,stats.Total,stats.ObjectTotal,stats.Status,s.Numbers,s.Bars,s.Compact,s.ShowHealing,healingTotal,scale,spectator,BackgroundOpacity, players=stats.Players.Select((p,i)=>new {p.Name,p.Hero,p.HeroName,p.Damage,p.ObjectDamage,p.Healing,p.UnspentSouls,p.Local,valid=available[i],objectValid=objectAvailable[i],healValid=healingAvailable[i]}).ToArray() });
            sig+=friendly&&s.ShowItems?Json.Serializer().Serialize(advice):"";
            if(signature!=sig || geometry) { signature=sig; Present(); }
            if(!PreviewOnly&&!Visible) Show();
        }
        protected override void DrawOverlay(Graphics g) {
            PaintCount++; if(stats==null || settings==null) return;
            g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(ClientSize.Width/250f,ClientSize.Height/(float)logicalHeight);Theme.Fill(g,Surface(Theme.Background),new RectangleF(1,1,248,logicalHeight-2),14);
            g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit; g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            Color accent=displayedTeam==2?Color.FromArgb(245,185,75):Color.FromArgb(115,167,255);
            using(var heroInk=new SolidBrush(Theme.HeroDamage))using(var objectInk=new SolidBrush(Theme.StructureDamage))using(var healInk=new SolidBrush(Theme.Healing))using(var a=new SolidBrush(accent)) using(var white=new SolidBrush(Color.FromArgb(237,240,245))) using(var muted=new SolidBrush(Color.FromArgb(146,156,170))) using(var track=new SolidBrush(Color.FromArgb(43,48,58))) {
                Theme.Fill(g,Surface(Theme.Card),new RectangleF(6,6,238,75),12);
                Design.Frame(g,new RectangleF(2,2,246,486),Theme.Gold);
                var emblem=art.Get("team-emblem-"+displayedTeam);
                if(emblem!=null)Design.Contain(g,emblem,new RectangleF(108,8,34,20),accent);
                if(settings.ShowHealing){
                    OverlayText.Draw(g,L.T("ГЕРОИ"),small,heroInk,14,27);OverlayText.Draw(g,L.T("СТРОЕНИЯ"),small,objectInk,92,27);OverlayText.Draw(g,L.T("ЛЕЧЕНИЕ"),small,healInk,175,27);
                    OverlayText.Draw(g,stats.Total.HasValue?Format(stats.Total.Value,settings.Compact):"—",normal,heroInk,new RectangleF(12,43,65,20),right);
                    OverlayText.Draw(g,stats.ObjectTotal.HasValue?Format(stats.ObjectTotal.Value,settings.Compact):"—",normal,objectInk,new RectangleF(89,43,65,20),right);
                    OverlayText.Draw(g,healingTotal.HasValue?Format(healingTotal.Value,settings.Compact):"—",normal,healInk,new RectangleF(171,43,65,20),right);
                }else{
                    OverlayText.Draw(g,L.T("ГЕРОИ"),small,heroInk,14,27);OverlayText.Draw(g,L.T("СТРОЕНИЯ"),small,objectInk,132,27);
                    OverlayText.Draw(g,stats.Total.HasValue?Format(stats.Total.Value,settings.Compact):"—",totalFont,heroInk,new RectangleF(12,37,108,26),right);
                    OverlayText.Draw(g,stats.ObjectTotal.HasValue?Format(stats.ObjectTotal.Value,settings.Compact):"—",totalFont,objectInk,new RectangleF(128,37,108,26),right);
                }
                OverlayText.Draw(g,L.ToLanguage(stats.Status),small,muted,new RectangleF(14,66,224,14),ellipsis);
                g.FillRectangle(track,14,82,222,1);
                for(int i=0;i<6;i++) {
                    int y=94+i*64;
                    if(i>=stats.Players.Length) { OverlayText.Draw(g,L.T("Ожидание игрока…"),normal,muted,52,y+5); continue; }
                    Player p=stats.Players[i]; var icon=images.Get(p.Hero);
                    Theme.Fill(g,Surface(Theme.Card),new RectangleF(8,y-5,234,60),9);
                    using(var border=new Pen(Color.FromArgb(42,62,65)))g.DrawLine(border,14,y+56,236,y+56);
                    g.FillRectangle(track,14,y,29,29);
                    if(icon!=null) g.DrawImage(icon,new Rectangle(14,y,29,29));
                    else OverlayText.Draw(g,"?",normal,muted,23,y+6);
                    OverlayText.Draw(g,p.Name+(p.Local?" •":""),normal,p.Local?a:white,new RectangleF(52,y-1,184,17),ellipsis);
                    Theme.DrawHeroBalance(g,p.HeroName,p.UnspentSouls,available[i],small,52,y+15,184);
                    if(settings.ShowHealing){
                        OverlayText.Draw(g,settings.Numbers?(available[i]?Format(p.Damage.Value,settings.Compact):"—"):"",normal,heroInk,new RectangleF(52,y+32,57,17),right);
                        OverlayText.Draw(g,settings.Numbers?(objectAvailable[i]?Format(p.ObjectDamage.Value,settings.Compact):"—"):"",normal,objectInk,new RectangleF(114,y+32,57,17),right);
                        OverlayText.Draw(g,settings.Numbers?(healingAvailable[i]?Format(p.Healing.Value,settings.Compact):"—"):"",normal,healInk,new RectangleF(177,y+32,59,17),right);
                    }else{
                        OverlayText.Draw(g,settings.Numbers?(available[i]?Format(p.Damage.Value,settings.Compact):"—"):"",normal,heroInk,new RectangleF(114,y+32,57,17),right);
                        OverlayText.Draw(g,settings.Numbers?(objectAvailable[i]?Format(p.ObjectDamage.Value,settings.Compact):"—"):"",normal,objectInk,new RectangleF(177,y+32,59,17),right);
                    }
                    if(settings.Bars) { g.FillRectangle(track,52,y+51,184,2); if(available[i]) g.FillRectangle(heroInk,52,y+51,(float)(184.0*p.Damage.Value/stats.Maximum),2); }
                }
                if(logicalHeight>490){
                    Theme.Fill(g,Surface(Theme.Card),new RectangleF(8,426,234,logicalHeight-434),12);
                    OverlayText.Draw(g,advice.Title,heading,a,new RectangleF(16,435,220,14),ellipsis);
                    OverlayText.Draw(g,advice.Context,small,muted,new RectangleF(16,451,218,28));
                    for(int i=0;i<advice.Items.Length;i++){var item=advice.Items[i];int y=483+i*24;OverlayText.Draw(g,item.Name,small,white,new RectangleF(16,y,140,20),ellipsis);OverlayText.Draw(g,item.WinRate.ToString("0.0")+"% · "+Format(item.Matches,true),small,a,new RectangleF(154,y,80,20),right);}
                    OverlayText.Draw(g,advice.Source,small,muted,16,logicalHeight-27);
                }
            }
        }
        public static string Format(long n,bool compact) { return compact&&n>=10000 ? (n/1000.0).ToString("0.0",CultureInfo.InvariantCulture)+"k" : n.ToString("N0",CultureInfo.GetCultureInfo(L.Language=="ru"?"ru-RU":"en-US")); }
        protected override void Dispose(bool disposing) { if(disposing) { art.Dispose();heading.Dispose(); normal.Dispose(); small.Dispose(); totalFont.Dispose(); ellipsis.Dispose(); right.Dispose(); } base.Dispose(disposing); }
    }
}






