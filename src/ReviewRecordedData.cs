using System;
using System.IO;
using System.Linq;
using System.Drawing;
namespace DeadlockOverlay {
 public sealed partial class PlayerDetailCanvas {
  ScoutDetailService recorded;long recordedMatch,recordedAccount;
  void LoadRecorded(){
   if(recorded!=null){if(selected==2)EnsureItems();LayoutRecorded();return;}
   recordedAccount=PlayerInsightEngine.AccountId(player.SteamId);
   if(recordedAccount<=0||!long.TryParse(summary.MatchId,out recordedMatch)||recordedMatch<=0)return;
   recorded=new ScoutDetailService(Path.Combine(SettingsManager.Folder,"scout-cache","profiles"));
   recorded.Changed+=()=>{try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed){if(selected==2)EnsureItems();LayoutRecorded();}}));}catch(InvalidOperationException){}};
   recorded.Open(recordedAccount,player.Hero,new[]{new ScoutMatch{Id=recordedMatch,Hero=player.Hero}},SettingsManager.Load().Scout.Network);
  }
  ScoutMatchDetail Recorded(){var d=recorded==null?null:recorded.Get(recordedAccount,recordedMatch);return d!=null&&d.Hero==player.Hero?d:null;}
  void LayoutRecorded(){Invalidate();}
  ReviewAssets reviewAssets;
  string itemFilter="all";ScoutMatchDetail analysedDetail;ReviewInventoryAnalysis inventory;int catalogueRevision=-1;
  long[] loadedItemIds=new long[0];
  void EnsureItems(){var d=Recorded();if(d==null||d.Purchases==null)return;var ids=d.Purchases.Select(p=>p.Item).Distinct().OrderBy(id=>id).ToArray();if(ids.Length==0||ids.SequenceEqual(loadedItemIds))return;if(reviewAssets!=null)reviewAssets.Dispose();loadedItemIds=ids;reviewAssets=new ReviewAssets();reviewAssets.Changed+=()=>{try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)Invalidate();}));}catch(InvalidOperationException){}};reviewAssets.Load(ids,SettingsManager.Load().Scout.Network);}
  int DrawRecorded(Graphics g,int y){
   var d=Recorded();if(d==null||(!d.Available&&!d.HasRecordedData))return Note(g,T("RECORDED MATCH DATA","ЗАПИСАННЫЕ ДАННЫЕ МАТЧА"),T("Detailed match data is loading or unavailable. No estimates are substituted.","Подробные данные матча загружаются или недоступны. Оценочные значения не подставляются."),y,Theme.Muted);
   if(!d.Available)y=Note(g,T("PARTIAL MATCH DATA","НЕПОЛНЫЕ ДАННЫЕ МАТЧА"),T("Only recorded events and snapshots are shown. Final totals are unavailable.","Показаны только записанные события и срезы. Итоговые значения недоступны."),y,Theme.Gold);
   if(selected==2)return DrawItems(g,d,y);
   var samples=(d.Timeline??new ReviewSample[0]).Where(s=>s.Second>=0&&s.Second<=(long)d.Seconds+30).ToArray();int axisEnd=Math.Max(1,Math.Max(d.Seconds,samples.Length==0?0:samples.Max(s=>s.Second)));
   y=Note(g,T("MEASURED SNAPSHOTS","ИЗМЕРЕННЫЕ СРЕЗЫ"),samples.Length+T(" recorded points. Hover a point for its exact time and value. Unmeasured intervals are left empty."," записанных точек. Наведите курсор для точного времени и значения. Неизмеренные промежутки оставлены пустыми."),y,Theme.SoulsBlue);
   string[] titles={T("Net worth","Ценность героя"),T("Hero damage","Урон героям"),T("Objective damage","Урон строениям"),T("Ally healing","Лечение союзников")};Color[] colors={Theme.SoulsBlue,Theme.HeroDamage,Theme.StructureDamage,Theme.Healing};int cols=ContentWidth>=850?2:1;float width=(ContentWidth-(cols-1)*14)/cols;
   for(int k=0;k<4;k++){int metric=k;var box=new RectangleF(16+k%cols*(width+14),y+k/cols*254,width,240);Theme.Fill(g,Theme.Card,box,10);DrawText(g,titles[k],15,colors[k],new RectangleF(box.X+16,box.Y+14,width-32,25),true);Func<ReviewSample,long?> value=s=>metric==0?s.Souls:metric==1?s.Damage:metric==2?s.Objectives:s.Healing;var valid=samples.Where(s=>value(s).HasValue).ToArray();if(valid.Length==0){DrawText(g,ScoutProfileData.Missing,16,Theme.Muted,new RectangleF(box.X+16,box.Y+80,width-32,30));continue;}double peak=valid.Max(s=>value(s).Value),max=Math.Max(1,peak);float left=box.X+65,right=box.Right-22,top=box.Y+60,bottom=box.Bottom-43;for(int i=0;i<3;i++){float gy=bottom-(bottom-top)*i/2;using(var pen=new Pen(Color.FromArgb(46,64,68)))g.DrawLine(pen,left,gy,right,gy);DrawText(g,ScoutProfileData.N(max*i/2),10,Theme.Muted,new RectangleF(box.X+8,gy-7,53,20));}
    foreach(var sample in valid){long measured=value(sample).Value;float px=left+(right-left)*sample.Second/axisEnd,py=bottom-(float)((bottom-top)*measured/max);using(var brush=new SolidBrush(colors[k]))g.FillEllipse(brush,px-3.5f,py-3.5f,7,7);Tip(new RectangleF(px-8,py-8,16,16),titles[k]+" · "+Clock(sample.Second)+" · "+measured.ToString("N0",ScoutProfileData.Culture));}
    DrawText(g,"0:00",11,Theme.Muted,new RectangleF(left,box.Bottom-28,75,20));DrawText(g,Clock(axisEnd/2),11,Theme.Muted,new RectangleF((left+right)/2-20,box.Bottom-28,75,20));DrawText(g,Clock(axisEnd),11,Theme.Muted,new RectangleF(right-45,box.Bottom-28,55,20));
   }y+=((4+cols-1)/cols)*254;foreach(var ev in d.Events??new ReviewEvent[0])y=Note(g,Clock(ev.Second)+" · "+T("DEATH","СМЕРТЬ"),ev.Hero>0?T("Recorded killer: ","Записанный убийца: ")+LocalSnapshot.HeroName(ev.Hero):T("Killer unavailable","Убийца неизвестен"),y,Theme.Muted);return y;
  }
  int DrawItems(Graphics g,ScoutMatchDetail d,int y){
   y=Note(g,T("ITEM HISTORY","ИСТОРИЯ ПРЕДМЕТОВ"),T("Recorded item events and valid sale times. Upgrade and removal semantics are not fully verified, so events may include upgrades as well as purchases. No final inventory is inferred.","Записанные события предметов и корректные отметки продажи. Правила обновления и удаления ещё не подтверждены, поэтому записи могут включать улучшения наряду с покупками. Финальный инвентарь не вычисляется."),y,Theme.Gold);
   int catalogue=reviewAssets==null?0:reviewAssets.CatalogueRevision;
   if(inventory==null||analysedDetail!=d||catalogueRevision!=catalogue){inventory=ReviewInventoryAnalysis.Build(d,id=>reviewAssets==null?null:reviewAssets.Get(id));analysedDetail=d;catalogueRevision=catalogue;}
   var analysis=inventory;var events=analysis.Filter(itemFilter);
   if(analysis.UniqueShopItems>0){string direction=string.Join(" · ",analysis.UniqueShopCategories.OrderByDescending(p=>p.Value).Select(p=>T(p.Key=="weapon"?"Weapon":p.Key=="vitality"?"Vitality":p.Key=="spirit"?"Spirit":"Other",p.Key=="weapon"?"Оружие":p.Key=="vitality"?"Живучесть":p.Key=="spirit"?"Дух":"Другое")+": "+p.Value));y=Note(g,T("RECORDED BUILD MIX","КАТЕГОРИИ ЗАПИСАННЫХ ПРЕДМЕТОВ"),direction+T(". Unique known shop item IDs encountered; includes later sales and does not establish equipped stats. Recorded sales: ",". Уникальные известные ID предметов магазина, включая проданные; это не итоговые характеристики. Записано продаж: ")+analysis.KnownSales+T(". Unclassified records: ",". Неопознанные записи: ")+analysis.Unclassified,y,Theme.Green);}
   string leading=analysis.DominantCategory();if(leading.Length>0)y=Note(g,T("BUILD DIRECTION","НАПРАВЛЕННОСТЬ СБОРКИ"),T("Most unique observed shop items belong to: ","Больше всего уникальных записанных предметов в категории: ")+CategoryLabel(leading)+T(". Category mix only; does not establish equipped build or optimality.",". Это состав категорий, не подтверждённая финальная сборка и не оценка эффективности."),y,Theme.SoulsBlue);
   string[] filters={"all","weapon","vitality","spirit","other","unknown"};int columns=ContentWidth>=690?6:3;float fw=(ContentWidth-(columns-1)*6)/columns;
   for(int i=0;i<filters.Length;i++){var box=new RectangleF(16+i%columns*(fw+6),y+i/columns*42,fw,36);Theme.Fill(g,itemFilter==filters[i]?Color.FromArgb(28,66,60):Theme.Card,box,7);DrawText(g,CategoryLabel(filters[i]),12,itemFilter==filters[i]?Theme.Green:Theme.Ink,new RectangleF(box.X+8,box.Y+7,box.Width-16,23),true);itemFilters.Add(new System.Collections.Generic.KeyValuePair<RectangleF,string>(box,filters[i]));}y+=((filters.Length+columns-1)/columns)*42+12;
   foreach(var ev in events){var purchase=ev.Record;var item=ev.Item;var box=new RectangleF(16,y,ContentWidth,76);if(box.Bottom>=offset&&box.Top<=offset+Height/Zoom){Theme.Fill(g,Theme.Card,box,10);if(ev.Sale)Design.Frame(g,box,Theme.Gold);var icon=pictures.Get(Path.Combine(HudAdvice.IconFolder,purchase.Item+".png"));if(icon!=null)g.DrawImage(icon,32,y+10,52,52);else{Theme.Fill(g,Color.FromArgb(37,55,59),new RectangleF(32,y+10,52,52),8);DrawText(g,"?",24,Theme.Muted,new RectangleF(48,y+20,30,30));}DrawText(g,item==null?T("Unidentified item","Неопознанный предмет"):item.Name,16,Theme.Ink,new RectangleF(100,y+10,ContentWidth-116,26),true);DrawText(g,Clock(ev.Second)+" · "+(ev.Sale?T("SALE","ПРОДАЖА"):T("Item event","Событие предмета"))+" · "+CategoryLabel(ReviewInventoryAnalysis.Category(item)),13,ev.Sale?Theme.Gold:Theme.Muted,new RectangleF(100,y+40,ContentWidth-116,24));Tip(box,(item==null?T("Catalogue data unavailable","Нет данных каталога"):item.Name)+" · ID "+purchase.Item+(purchase.UpgradeId>0?T(" · recorded upgrade ID "," · записанный ID улучшения ")+purchase.UpgradeId:""));}y+=88;}
   if(events.Length==0)y=Note(g,T("NO RECORDED ITEM EVENTS","НЕТ ЗАПИСЕЙ О ПРЕДМЕТАХ"),ScoutProfileData.Missing,y,Theme.Muted);return y;
  }
  static string CategoryLabel(string kind){switch(kind){case "all":return T("All","Все");case "weapon":return T("Weapon","Оружие");case "vitality":return T("Vitality","Живучесть");case "spirit":return T("Spirit","Дух");case "unknown":return T("Unknown","Неизвестно");default:return T("Other","Другое");}}
  static string Clock(int seconds){return seconds/60+":"+(seconds%60).ToString("00");}
 }
}
