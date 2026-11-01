using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace DeadlockOverlay {
    public sealed class HelperPick { public string Name,Image,Why;public long Id;public double Score;public int Tier; }
    public sealed class HelperSource {
        public const string Origin="https://deadlockitembuilder.com";
        Dictionary<string,object> core,picks;
        Dictionary<string,Dictionary<string,object>> catalog;
        public static string Slug(string name){return Regex.Replace((name??"").ToLowerInvariant().Replace("&","and"),"[^a-z0-9]+","-").Trim('-');}
        public static string Phase(double seconds){return seconds<720?"early":seconds<1500?"mid":"late";}
        static Dictionary<string,object> Obj(Dictionary<string,object> d,string key){return Json.Obj(Json.Get(d,key));}
        public static HelperSource Parse(object core,object picks,object items){
            var s=new HelperSource{core=Json.Obj(core),picks=Json.Obj(picks),catalog=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase)};
            if(Obj(s.picks,"heroes").Count==0||Obj(s.core,"persona").Count==0)throw new FormatException("Counter Helper format changed");
            foreach(var item in items as object[]??new object[0]){var d=Json.Obj(item);if(Json.Str(d,"type")=="upgrade"&&Json.Bool(d,"shopable")){s.catalog[Json.Str(d,"name")]=d;if(Json.Str(d,"class_name")=="upgrade_magic_shock")s.catalog["Tank Buster"]=d;}}
            return s;
        }
        public static HelperSource Load(bool online){
            string folder=Path.Combine(SettingsManager.Folder,"counter-helper");Directory.CreateDirectory(folder);
            string[] paths={"/data/db.json","/data/db-helper.json","https://api.deadlock-api.com/v1/assets/items?language=english"};string[] files={"core.json","helper.json","items.json"};var data=new object[3];
            var serializer=new JavaScriptSerializer{MaxJsonLength=16777216,RecursionLimit=64};
            if(online)Parallel.For(0,3,i=>data[i]=ItemAdvisor.Fetch(paths[i].StartsWith("https")?paths[i]:Origin+paths[i]));
            else for(int i=0;i<3;i++)data[i]=serializer.DeserializeObject(File.ReadAllText(Path.Combine(folder,files[i])));
            var result=Parse(data[0],data[1],data[2]);
            if(online)for(int i=0;i<3;i++)File.WriteAllText(Path.Combine(folder,files[i]),serializer.Serialize(data[i]));
            return result;
        }
        public HelperPick[] Rank(string enemy,string you,string phase){
            var guide=Obj(Obj(picks,"heroes"),enemy);var top=(Json.Get(guide,"top") as object[]??new object[0]).Select(Convert.ToString).Distinct().ToArray();
            var tiers=Obj(guide,"recommendationTiers");var stages=Obj(guide,"stages");var persona=Obj(core,"persona");var capabilities=Obj(Obj(persona,"caps"),Slug(you));
            var result=new List<HelperPick>();
            foreach(bool alt in new[]{false,true}){var group=top.Where(n=>(Json.Str(tiers,n)=="alternative")==alt).ToArray();for(int i=0;i<group.Length;i++){
                string name=group[i],stage=Json.Str(stages,name);if(stage==""){double avg;var item=Obj(Obj(picks,"items"),name);if(double.TryParse(Json.Str(item,"avg"),NumberStyles.Float,CultureInfo.InvariantCulture,out avg))stage=avg<=12?"early":avg<=25?"mid":"late";}
                if(phase=="early"&&stage!="early")continue;
                Dictionary<string,object> asset;if(!catalog.TryGetValue(name,out asset))continue;
                var requirement=Obj(Obj(persona,"req"),Slug(name));var fit=Obj(capabilities,Json.Str(requirement,"cap"));string rating=Json.Str(fit,"r");double adjustment=rating=="strong"?.8:rating=="conditional"?.1:rating=="weak"?-.2:rating=="unavailable"?-.6:0;
                double score=1-.4*i/Math.Max(1,group.Length)+(stage==phase?.65:phase=="late"&&stage=="mid"?.12:0)+adjustment;
                result.Add(new HelperPick{Name=name,Id=Json.Number(asset,"id")??0,Image=Json.Str(asset,"shop_image"),Tier=alt?1:0,Score=score,Why=Json.Str(fit,"why")});
            }}
            return result.OrderBy(x=>x.Tier).ThenByDescending(x=>Math.Round(x.Score,10)).ThenBy(x=>x.Name,StringComparer.InvariantCulture).Take(2).ToArray();
        }
    }
}
