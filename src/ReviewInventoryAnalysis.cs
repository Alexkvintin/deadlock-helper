using System;
using System.Linq;
using System.Collections.Generic;

namespace DeadlockOverlay {
 public sealed class RecordedItemEvent {public int Second;public bool Sale;public ReviewPurchase Record;public ReviewItem Item;}
 public sealed class ReviewInventoryAnalysis {
  public RecordedItemEvent[] Events=new RecordedItemEvent[0];
  public Dictionary<string,int> UniqueShopCategories=new Dictionary<string,int>();
  public int KnownSales,Unclassified,UniqueShopItems;
  public bool FinalInventoryVerified{get{return false;}}
  public static ReviewInventoryAnalysis Build(ScoutMatchDetail detail,Func<long,ReviewItem> catalogue){
   var result=new ReviewInventoryAnalysis();if(detail==null||detail.Seconds<=0)return result;
   var entries=new List<RecordedItemEvent>();var records=(detail.Purchases??new ReviewPurchase[0]).Where(p=>p!=null&&p.Item>0&&p.Second>=0&&p.Second<=detail.Seconds).ToArray();var unique=new Dictionary<long,ReviewItem>();
   foreach(var record in records){var item=catalogue(record.Item);if(item!=null&&!item.Shop)continue; // known ability records are not shop purchases
    if(item==null)result.Unclassified++;else unique[record.Item]=item;
    entries.Add(new RecordedItemEvent{Second=record.Second,Record=record,Item=item});var sale=ScoutMatchDetail.RecordedSale(record.Sold,record.Second,detail.Seconds);
    if(sale.HasValue){entries.Add(new RecordedItemEvent{Second=sale.Value,Sale=true,Record=record,Item=item});result.KnownSales++;}
   }
   result.Events=entries.OrderBy(e=>e.Second).ThenBy(e=>e.Sale?1:0).ToArray();result.UniqueShopItems=unique.Count;result.UniqueShopCategories=unique.Values.GroupBy(i=>i.Kind??"").ToDictionary(g=>g.Key,g=>g.Count());return result;
  }
 }
}
