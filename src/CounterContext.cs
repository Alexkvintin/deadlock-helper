using System;
using System.Linq;
using System.Collections.Generic;

namespace DeadlockOverlay {
 public enum CounterMode { Auto,Team,Lane,Situational }
 public sealed class CounterContext {
  public Player Focus;
  public Player[] Enemies=new Player[0],LaneEnemies=new Player[0];
  public string Identity="",Phase="",LaneStatus="";
  public bool Manual,InventoryKnown,LanePhase;
  public long? Souls;
  public static string Key(Player p){return p.AccountId>0?"a:"+p.AccountId:"s:"+p.Team+":"+p.Slot+":"+p.Hero;}
  public static CounterContext Create(MatchState state,MapSnapshot map,string manual){
   var result=new CounterContext();if(state==null||string.IsNullOrEmpty(state.MatchId)||!state.Healthy)return result;
   var roster=state.KnownRoster;var candidates=!state.Spectator?state.Players.Values.Where(p=>p.Local).ToArray():roster.Where(p=>p.Hero>0&&p.Hero==state.ViewedHeroId).ToArray();if(!state.Spectator&&candidates.Length==0)candidates=roster.Where(p=>p.Local).ToArray();
   // Viewing a hero is a perspective change, not a change to the roster's player identity.
   if(candidates.Length!=1&&state.ViewedHeroId>0)candidates=roster.Where(p=>p.Hero==state.ViewedHeroId).ToArray();
   if(candidates.Length!=1||candidates[0].Hero<=0)return result;
   var focus=candidates[0];result.Focus=focus;result.Identity=state.MatchId+":"+Key(focus);result.Enemies=roster.Where(p=>p.Team!=focus.Team&&p.Hero>0).OrderBy(Key).ToArray();
   var observed=state.Players.Values.Where(p=>Key(p)==Key(focus)&&p.Hero==focus.Hero).ToArray();if(observed.Length==1)result.Souls=observed[0].UnspentSouls;
   result.Phase=map!=null&&map.Fresh&&map.Seconds.HasValue?HelperSource.Phase(map.Seconds.Value):"";
   result.LanePhase=LocalSnapshot.Pregame(state.Phase)||map!=null&&map.Fresh&&map.Seconds.HasValue&&map.Seconds.Value<480;
   var selected=result.Enemies.Where(p=>Key(p)==manual).ToArray();
   if(selected.Length==1){result.Manual=true;result.LaneEnemies=selected;result.LaneStatus=L.Choose("Manual lane opponent","Соперник линии выбран вручную");}
   else if(focus.ConfirmedLane.HasValue){result.LaneEnemies=result.Enemies.Where(p=>p.ConfirmedLane==focus.ConfirmedLane).ToArray();result.LaneStatus=result.LaneEnemies.Length>0?L.Choose("Confirmed lane","Подтверждённая линия"):L.Choose("Lane opponents unknown","Противники линии неизвестны");}
   else result.LaneStatus=L.Choose("Lane not confirmed - choose an opponent","Линия не подтверждена - выберите соперника");
   return result;
  }
  public string Signature(string affordability,string owned,CounterMode mode){return Identity+":"+(Focus==null?0:Focus.Hero)+":"+Phase+":"+LanePhase+":"+mode+":"+affordability+":"+owned+":"+string.Join(",",Enemies.Select(p=>Key(p)+"/"+p.Hero))+":"+string.Join(",",LaneEnemies.Select(Key))+":"+Manual;}
 }
}
