using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace DeadlockOverlay {
 public static class AssetLibrary {
  public static string Folder{get{return Path.Combine(SettingsManager.Folder,"art-library");}}
  public static int Revision{get;private set;}
  public static string Resolve(string relative){string local=Path.Combine(Folder,relative);return File.Exists(local)?local:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets",relative);}
  public static int Import(string folder){return ImportInto(folder,Folder);}
  internal static int ImportInto(string folder,string target){
   // No archives, executables or VPK extraction. Only known image names from a
   // user-selected existing asset folder; game licensing is not inferred here.
   var accepted=new List<KeyValuePair<string,string>>();
   foreach(string file in Directory.GetFiles(folder,"*.png",SearchOption.TopDirectoryOnly))if(Regex.IsMatch(Path.GetFileName(file),@"^\d{1,4}\.png$"))accepted.Add(new KeyValuePair<string,string>(file,Path.GetFileName(file)));
   string art=Path.Combine(folder,"local-art");if(Directory.Exists(art))foreach(string file in Directory.GetFiles(art,"*.png",SearchOption.TopDirectoryOnly))if(Regex.IsMatch(Path.GetFileName(file),@"^(hero-\d{1,4}|patron-[23]|background|location|midboss|rift|crystals|buff-[a-z]+)\.png$"))accepted.Add(new KeyValuePair<string,string>(file,Path.Combine("local-art",Path.GetFileName(file))));
   if(accepted.Count==0)throw new InvalidDataException(L.Choose("No compatible images found. Select an assets folder containing hero ID PNGs or local-art.","Изображения не найдены. Выберите папку assets с PNG по ID героя или local-art."));
   foreach(var pair in accepted){var info=new FileInfo(pair.Key);if(info.Length>16*1024*1024)throw new InvalidDataException("Image exceeds 16 MB: "+info.Name);using(var image=Image.FromFile(pair.Key)){if(image.Width>8192||image.Height>8192)throw new InvalidDataException("Image dimensions exceed limit: "+info.Name);}}
   foreach(var pair in accepted){string destination=Path.Combine(target,pair.Value);Directory.CreateDirectory(Path.GetDirectoryName(destination));if(!string.Equals(Path.GetFullPath(pair.Key),Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))File.Copy(pair.Key,destination,true);}
   Revision++;return accepted.Count;
  }
 }
}
