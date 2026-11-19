using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace DeadlockOverlay {
    public sealed class HelperPick { public string Name,Image,Why,Condition;public long Id;public double Score;public int Tier;public int? Cost; }
    public sealed class HelperSource {
        public const string Origin="https://deadlockitembuilder.com";
        Dictionary<string,object> core,picks;
        Dictionary<string,Dictionary<string,object>> catalog;
        int[] costs=new int[0];
        public static string Slug(string name){return Regex.Replace((name??"").ToLowerInvariant().Replace("&","and"),"[^a-z0-9]+","-").Trim('-');}
        public static string Phase(double seconds){return seconds<720?"early":seconds<1500?"mid":"late";}
        static Dictionary<string,object> Obj(Dictionary<string,object> d,string key){return Json.Obj(Json.Get(d,key));}
        public static HelperSource Parse(object core,object picks,object items){
            var s=new HelperSource{core=Json.Obj(core),picks=Json.Obj(picks),catalog=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase)};
            if(Obj(s.picks,"heroes").Count==0||Obj(s.core,"persona").Count==0)throw new FormatException("Counter Helper format changed");
            foreach(var item in items as object[]??new object[0]){var d=Json.Obj(item);if(Json.Str(d,"type")=="upgrade"&&Json.Bool(d,"shopable")){s.catalog[Json.Str(d,"name")]=d;if(Json.Str(d,"class_name")=="upgrade_magic_shock")s.catalog["Tank Buster"]=d;}}
            s.costs=s.catalog.Values.Select(d=>Json.Number(d,"cost")).Where(n=>n>0&&n<=int.MaxValue).Select(n=>(int)n.Value).Distinct().OrderBy(n=>n).ToArray();return s;
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
        public string AffordabilityKey(long? souls){return souls.HasValue?costs.Count(c=>c<=souls.Value).ToString():"unknown";}
        static string Plain(string text){return Regex.Replace(text??"","<[^>]+>","").Trim();}
        HelperPick Pick(string name,Dictionary<string,object> asset,int tier,double score,string fallback){var item=Obj(Obj(picks,"items"),name);string reason=Plain(Json.Str(item,"short"));if(reason.Length==0)reason=Plain(Json.Str(Obj(asset,"description"),"desc"));if(reason.Length==0)reason=Plain(fallback);var cost=Json.Number(asset,"cost");return new HelperPick{Name=name,Id=Json.Number(asset,"id")??0,Image=Json.Str(asset,"shop_image"),Tier=tier,Score=score,Why=reason,Cost=cost>0&&cost<=int.MaxValue?(int?)cost:null};}
        public HelperPick[] Rank(string enemy,string you,string phase,ISet<long> owned=null,long? souls=null,int maximum=2){
            var guide=Obj(Obj(picks,"heroes"),enemy);var top=(Json.Get(guide,"top") as object[]??new object[0]).Select(Convert.ToString).Distinct().ToArray();
            var tiers=Obj(guide,"recommendationTiers");var stages=Obj(guide,"stages");var persona=Obj(core,"persona");var capabilities=Obj(Obj(persona,"caps"),Slug(you));
            var result=new List<HelperPick>();
            foreach(bool alt in new[]{false,true}){var group=top.Where(n=>(Json.Str(tiers,n)=="alternative")==alt).ToArray();for(int i=0;i<group.Length;i++){
                string name=group[i],stage=Json.Str(stages,name);if(stage==""){double avg;var item=Obj(Obj(picks,"items"),name);if(double.TryParse(Json.Str(item,"avg"),NumberStyles.Float,CultureInfo.InvariantCulture,out avg))stage=avg<=12?"early":avg<=25?"mid":"late";}
                if(phase=="early"&&stage!="early")continue;
                Dictionary<string,object> asset;if(!catalog.TryGetValue(name,out asset))continue;
                var requirement=Obj(Obj(persona,"req"),Slug(name));var fit=Obj(capabilities,Json.Str(requirement,"cap"));string rating=Json.Str(fit,"r");double adjustment=rating=="strong"?.8:rating=="conditional"?.1:rating=="weak"?-.2:rating=="unavailable"?-.6:0;
                double score=1-.4*i/Math.Max(1,group.Length)+(stage==phase?.65:phase=="late"&&stage=="mid"?.12:0)+adjustment;
                var pick=Pick(name,asset,alt?1:0,score,Json.Str(fit,"why"));if(pick.Id>0&&(owned==null||!owned.Contains(pick.Id)))result.Add(pick);
            }}
            return result.OrderBy(x=>x.Tier).ThenByDescending(x=>souls.HasValue&&x.Cost.HasValue&&x.Cost<=souls.Value).ThenByDescending(x=>Math.Round(x.Score,10)).ThenBy(x=>x.Name,StringComparer.InvariantCulture).Take(maximum).ToArray();
        }
        public HelperPick[] Situational(string enemy,string you,string phase,ISet<long> owned,long? souls){
            var guide=Obj(Obj(picks,"heroes"),enemy);var situations=Json.Get(guide,"sits") as object[]??new object[0];var ranked=Rank(enemy,you,phase,owned,souls,100).GroupBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);var result=new List<HelperPick>();
            foreach(var raw in situations){var fields=raw as object[];if(fields==null||fields.Length<3)continue;string name=Convert.ToString(fields[1]);HelperPick pick;if(!ranked.TryGetValue(name,out pick))continue;pick.Condition=Plain(Convert.ToString(fields[0]));if(pick.Condition.Length==0)continue;string reason=Plain(Convert.ToString(fields[2]));if(reason.Length>0)pick.Why=reason;result.Add(pick);}
            return result.OrderByDescending(p=>souls.HasValue&&p.Cost.HasValue&&p.Cost<=souls.Value).ThenByDescending(p=>p.Score).Take(2).ToArray();
        }
    }
}
