using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace DeadlockOverlay {
    public static class BridgeEffects {
        // Subclass IDs from /assets/misc-entities; each pickup's modifier has this subclass_name.
        static readonly Dictionary<ulong,string> Names=new Dictionary<ulong,string>{{201785745,"Оружие"},{828222066,"Живучесть"},{15572055,"Магия"},{754654000,"Скорость"}};
        public static string Name(object data){var list=data as object[];if(list==null)return "?";var found=new HashSet<string>();foreach(var o in list){string hex=Convert.ToString(o);if(hex.Length!=16)continue;ulong id=0;bool valid=true;for(int i=0;i<8;i++){byte b;if(!byte.TryParse(hex.Substring(i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out b)){valid=false;break;}id|=(ulong)b<<(8*i);}string name;if(valid&&Names.TryGetValue(id,out name))found.Add(name);}return found.Count>0?string.Join(" + ",found.OrderBy(x=>x)):"?";}
    }
}
