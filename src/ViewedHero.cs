using System;
using System.Linq;
using System.Text;
using Microsoft.Win32;
namespace DeadlockOverlay {
    public static class ViewedHero {
        public static string LocalSteamId(){try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess")){long id=Convert.ToInt64(k==null?null:k.GetValue("ActiveUser"));return id>0?(76561197960265728L+id).ToString():"";}}catch{return "";}}
        static string Normalize(string s){return new string((s??"").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());}
        public static Player Match(MatchState state,string text){
            string read=Normalize(text);if(read.Length<3)return null;
            var complete=state.Players.Values.Where(p=>Normalize(p.Name)==read).ToArray();if(complete.Length==1)return complete[0];
            var exact=state.Players.Values.Where(p=>Normalize(p.Name).Length>=3&&read.Contains(Normalize(p.Name))).ToArray();if(exact.Length==1)return exact[0];
            return null;
        }
    }
}
