using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
namespace DeadlockOverlay {
 public sealed class PartyMarkers {
  public sealed class State {public string Session="";public Dictionary<string,int> Slots=new Dictionary<string,int>();}
  readonly string path;State state=new State();
  static readonly Color[] palette={Color.FromArgb(84,218,184),Color.FromArgb(195,144,247),Color.FromArgb(247,188,92),Color.FromArgb(91,181,247),Color.FromArgb(244,134,180),Color.FromArgb(182,220,101)};
  public PartyMarkers(string file){path=file;try{if(File.Exists(path)&&new FileInfo(path).Length<16384)state=Json.Serializer().Deserialize<State>(File.ReadAllText(path))??new State();}catch{state=new State();}}
  public void Assign(string session,Dictionary<long,PartyInfo> parties){
   if(state.Session!=session)state=new State{Session=session};if(state.Slots==null)state.Slots=new Dictionary<string,int>();
   var groups=parties.Values.Where(p=>p.Confidence!=PartyConfidence.Unknown).Distinct().Select(p=>new{Info=p,Key=string.Join(",",p.Members.OrderBy(x=>x))}).OrderBy(p=>p.Key).ToArray();var active=new HashSet<string>(groups.Select(p=>p.Key));foreach(string key in state.Slots.Keys.Where(k=>!active.Contains(k)).ToArray())state.Slots.Remove(key);
   var used=new HashSet<int>();foreach(var group in groups){int slot;if(state.Slots.TryGetValue(group.Key,out slot)&&slot>=0&&slot<6&&used.Add(slot))continue;state.Slots.Remove(group.Key);}
   foreach(var group in groups){int slot;if(!state.Slots.TryGetValue(group.Key,out slot)){slot=Enumerable.Range(0,6).First(i=>!used.Contains(i));used.Add(slot);state.Slots[group.Key]=slot;}group.Info.Group=((char)('A'+slot)).ToString();group.Info.Color=palette[slot];}
   try{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,Json.Serializer().Serialize(state));}catch(Exception e){Log.Write("Party markers cache",e);}
  }
 }
}
