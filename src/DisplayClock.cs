using System;
namespace DeadlockOverlay {
 public sealed class DisplayClock {
  string match="";double? current;DateTime previous;
  public double? Update(string id,double? sample,bool paused,DateTime now){
   if(id!=match||!current.HasValue){match=id;current=sample;previous=now;return current;}
   double dt=Math.Max(0,Math.Min(2,(now-previous).TotalSeconds));previous=now;
   if(!sample.HasValue)return null;
   if(paused){current=sample;return current;}
   double predicted=current.Value+dt,error=sample.Value-predicted;
   // Small source/recognition jitter changes the rate slightly, not the visible second.
   current=Math.Abs(error)>15?sample.Value:predicted+Math.Max(-dt*.15,Math.Min(dt*.15,error));return current;
  }
 }
}
