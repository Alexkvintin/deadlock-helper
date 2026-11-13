using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
namespace DeadlockOverlay {
 public sealed class ProfileImages:IDisposable {
  readonly Dictionary<string,Image> images=new Dictionary<string,Image>();readonly Dictionary<string,DateTime> stamps=new Dictionary<string,DateTime>();
  public Image Get(string path){if(string.IsNullOrEmpty(path))return null;try{DateTime stamp=File.Exists(path)?File.GetLastWriteTimeUtc(path):DateTime.MinValue;DateTime before;Image image;if(stamps.TryGetValue(path,out before)&&before==stamp){images.TryGetValue(path,out image);return image;}if(images.TryGetValue(path,out image)&&image!=null)image.Dispose();images[path]=null;stamps[path]=stamp;if(stamp==DateTime.MinValue)return null;using(var raw=Image.FromFile(path))image=new Bitmap(raw);images[path]=image;return image;}catch{return null;}}
  public void Dispose(){foreach(var image in images.Values)if(image!=null)image.Dispose();images.Clear();stamps.Clear();}
 }
 public static class ProfileVisuals {
  public static void Card(Graphics g,RectangleF box,string label,string value,string source,Color accent){Theme.Fill(g,Theme.Card,box,10);Design.Text(g,label,12,Theme.Muted,new RectangleF(box.X+16,box.Y+12,box.Width-32,20));Design.Text(g,value,23,accent,new RectangleF(box.X+16,box.Y+36,box.Width-32,31),true);Design.Text(g,source,11,Theme.Muted,new RectangleF(box.X+16,box.Y+72,box.Width-32,30));}
  public static void Avatar(Graphics g,Image avatar,RectangleF box,string name){Theme.Fill(g,Color.FromArgb(35,59,63),box,12);if(avatar!=null){var save=g.Save();using(var path=Theme.Round(box,12))g.SetClip(path,System.Drawing.Drawing2D.CombineMode.Intersect);g.DrawImage(avatar,box);g.Restore(save);}else Design.Text(g,string.IsNullOrWhiteSpace(name)?"?":name.Substring(0,1).ToUpperInvariant(),32,Theme.Green,new RectangleF(box.X+20,box.Y+18,box.Width-25,50),true);}
 }
}
