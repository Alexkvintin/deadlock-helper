using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace DeadlockOverlay {
    public sealed class Settings {
        public ScoutOptions Scout=new ScoutOptions();
        public bool Enabled=true, Sort=true, Numbers=true, Bars=true, Compact=true, Autostart=false, Diagnostics=false;
        public double Scale=1, Opacity=0.88, LeftX=0.015, LeftY=0.30, RightX=0.985, RightY=0.30;
        public int Hotkey=119; // Ctrl+Shift+F8
        public int[] PartHotkeys=new int[6];
        public bool ShowRanks=true;
        public bool ShowHiddenKing=true,ShowArchmother=true,AvoidOverlap=true;
        public void TogglePart(int part){if(part==0)ShowHiddenKing=!ShowHiddenKing;else if(part==1)ShowArchmother=!ShowArchmother;else if(part==2)ShowItems=!ShowItems;else if(part==3)ShowCrystals=!ShowCrystals;else if(part==4)ShowMidBoss=!ShowMidBoss;else if(part==5)ShowRift=!ShowRift;}
        public int AdvisorHero=0,AdvisorPhase=0;
        public bool ShowItems=true;
        public bool ScreenRecognition=true;
        public string Language="en";
        public bool ShowRift=true,ShowCrystals=true,ShowMidBoss=true;
        public bool ShowHudPart(int part){return part==0?ShowHiddenKing:part==1?ShowArchmother:part==2?ShowItems:part==3?ShowCrystals:part==4?ShowMidBoss:part==5?ShowRift:true;}
        public long LiveMatchId=0;
        public bool AutoMatch=true, ShowHealing=false, ShowSummary=true;
        public string Token="";
        [System.Web.Script.Serialization.ScriptIgnore] public bool EditLayout;
        public WidgetPlacement[] Layout;
        public WidgetPlacement Placement(int index){if(Layout==null||Layout.Length!=6)Layout=new[]{new WidgetPlacement(LeftX,LeftY,Scale,Opacity),new WidgetPlacement(RightX,RightY,Scale,Opacity),new WidgetPlacement(.5,.15,1,1),new WidgetPlacement(.20,.15,1,1),new WidgetPlacement(.80,.15,1,1),new WidgetPlacement(.5,.145,1,1)};if(Layout[index]==null)Layout[index]=new WidgetPlacement(.5,.2,1,1);return Layout[index];}
        public void Validate() {
            if(Scout==null)Scout=new ScoutOptions();Scout.Validate();
            if(Language!="ru")Language="en";
            Scale=Clamp(Scale,0.6,1.8,1); Opacity=Clamp(Opacity,0.25,1,0.88);
            LeftX=Clamp(LeftX,0,1,0.015); LeftY=Clamp(LeftY,0,1,0.3);
            RightX=Clamp(RightX,0,1,0.985); RightY=Clamp(RightY,0,1,0.3);
            if(Hotkey<112 || Hotkey>123) Hotkey=119;
            if(PartHotkeys==null||PartHotkeys.Length!=6)PartHotkeys=new int[6];
            var used=new System.Collections.Generic.HashSet<int>{Hotkey};for(int i=0;i<6;i++)if(PartHotkeys[i]!=0&&(PartHotkeys[i]<112||PartHotkeys[i]>123||!used.Add(PartHotkeys[i])))PartHotkeys[i]=0;
            for(int i=0;i<6;i++){var p=Placement(i);p.X=Clamp(p.X,0,1,.5);p.Y=Clamp(p.Y,0,1,.15);p.Scale=Clamp(p.Scale,.5,2,1);p.Opacity=Clamp(p.Opacity,.2,1,.88);}
            if(Token==null || Token.Length!=64) { var b=new byte[32]; using(var r=RandomNumberGenerator.Create()) r.GetBytes(b); Token=BitConverter.ToString(b).Replace("-",""); }
        }
        static double Clamp(double n,double lo,double hi,double fallback) { return double.IsNaN(n)||double.IsInfinity(n) ? fallback : Math.Max(lo,Math.Min(hi,n)); }
    }
    public static class SettingsManager {
        public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeadlockOverlay");
        public static readonly string FilePath=Path.Combine(Folder,"settings.json");
        public static Settings Load() {
            Directory.CreateDirectory(Folder); Settings s=null;
            try { if(File.Exists(FilePath)) s=Json.Serializer().Deserialize<Settings>(File.ReadAllText(FilePath)); }
            catch(Exception e) { Log.Write("Settings load",e); }
            s=s??new Settings(); s.Validate(); return s;
        }
        public static void Save(Settings s) {
            s.Validate(); Directory.CreateDirectory(Folder);
            string temp=FilePath+".tmp"; File.WriteAllText(temp,Json.Serializer().Serialize(s));
            if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
        }
        public static void SetAutostart(bool enabled) {
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if(enabled) key.SetValue("DeadlockOverlay","\""+System.Windows.Forms.Application.ExecutablePath+"\" --minimized");
                else key.DeleteValue("DeadlockOverlay",false);
            }
        }

    }
    public static class Log {
        static readonly object Gate=new object();
        public static void Write(string message,Exception e=null) {
            try { lock(Gate) {
                Directory.CreateDirectory(SettingsManager.Folder); string p=Path.Combine(SettingsManager.Folder,"errors.log");
                if(File.Exists(p) && new FileInfo(p).Length>512*1024) { File.Copy(p,p+".1",true); File.Delete(p); }
                File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+message+(e==null?"":" "+e.GetType().Name+": "+e.Message)+Environment.NewLine);
            } } catch { }
        }
    }
}
