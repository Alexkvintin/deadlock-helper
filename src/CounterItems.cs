using System;
using System.Collections.Generic;
using System.Linq;
namespace DeadlockOverlay {
    public static class CounterItems {
        // Only effects that interfere with an opponent; no generic healing or damage boosts.
        static readonly HashSet<string> Allowed=new HashSet<string>{"upgrade_healbane","upgrade_toxic_bullets","upgrade_targeted_silence","upgrade_proc_silence","upgrade_withering_whip","upgrade_greater_withering_whip","upgrade_target_stun","upgrade_reduce_debuff_duration","upgrade_debuff_reducer","upgrade_metal_skin","upgrade_return_fire","upgrade_counterspell","upgrade_unstoppable","upgrade_glitch","upgrade_suppressor","upgrade_spirit_sap","upgrade_containment"};
        public static bool Eligible(string name){return Allowed.Contains(name);}
        public static ItemSuggestion[] Rank(object data,Dictionary<long,string> names){
            return (data as object[]??new object[0]).Select(Json.Obj).Select(d=>new ItemSuggestion{Id=Json.Number(d,"item_id")??0,Matches=Json.Number(d,"matches")??0,Wins=Json.Number(d,"wins")??0,Losses=Json.Number(d,"losses")??0})
            .Where(x=>names.ContainsKey(x.Id)&&x.Matches>=100&&x.Wins+x.Losses>=100)
            .OrderByDescending(x=>LowerBound(x.Wins,x.Losses)).ThenByDescending(x=>x.Matches).Take(2).Select(x=>{x.Name=names[x.Id];return x;}).ToArray();
        }
        public static double LowerBound(long wins,long losses){double n=wins+losses;if(n<=0)return 0;double p=wins/n,z=1.96;return(p+z*z/(2*n)-z*Math.Sqrt((p*(1-p)+z*z/(4*n))/n))/(1+z*z/n);}
    }
}
