using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
namespace DeadlockOverlay {
 public static class AssetLibrary {
  public static string Folder{get{return Path.Combine(SettingsManager.Folder,"art-library");}}
  public static int Revision{get;private set;}
  static readonly object gate=new object();static readonly HashSet<int> attempted=new HashSet<int>();static Task pending=Task.FromResult(0);
  static bool teamsRequested;
  public static void EnsureTeamIcons(){if(UiReview.Active)return;lock(gate){if(teamsRequested)return;teamsRequested=true;pending=pending.ContinueWith(_=>{foreach(int team in new[]{2,3})try{string relative=Path.Combine("ui","patron-"+team+".png");if(File.Exists(Resolve(relative)))continue;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create("https://media.githubusercontent.com/media/0xThiagoAmaral/deadlock-open-assets/main/images/deadlock/ui/hud/core/team"+(team-1)+"_icon_png.png");request.Timeout=8000;request.ReadWriteTimeout=8000;request.AllowAutoRedirect=false;using(var response=request.GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){byte[] b=new byte[8192];int n;while((n=stream.Read(b,0,b.Length))>0){if(memory.Length+n>2*1024*1024)throw new InvalidDataException();memory.Write(b,0,n);}memory.Position=0;using(var image=Image.FromStream(memory))using(var bitmap=new Bitmap(image,96,96)){Directory.CreateDirectory(Path.Combine(Folder,"ui"));bitmap.Save(Path.Combine(Folder,relative),System.Drawing.Imaging.ImageFormat.Png);}}lock(gate)Revision++;}catch(Exception e){Log.Write("Runtime team icon",e);}});}}
  // Runtime portraits are not repackaged as software-owned artwork. One queued worker,
  // at most one attempt per hero per process, reusing the user's existing asset cache.
  public static void EnsureHeroes(IEnumerable<int> heroes){if(UiReview.Active)return;lock(gate){var ids=heroes.Where(id=>id>0&&!File.Exists(Resolve(id+".png"))&&attempted.Add(id)).Distinct().Take(12).ToArray();if(ids.Length==0)return;pending=pending.ContinueWith(_=>{try{string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","sources.json");if(!File.Exists(source))return;var catalog=Json.Serializer().DeserializeObject(File.ReadAllText(source)) as object[]??new object[0];Directory.CreateDirectory(Folder);foreach(int id in ids){var data=catalog.Select(Json.Obj).FirstOrDefault(d=>Json.Number(d,"id")==id);if(data==null)continue;Uri uri;if(!Uri.TryCreate(Json.Str(data,"url"),UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="assets-bucket.deadlock-api.com")continue;try{ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(uri);request.Timeout=8000;request.ReadWriteTimeout=8000;request.AllowAutoRedirect=false;using(var response=request.GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){byte[] buffer=new byte[8192];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){if(memory.Length+n>2*1024*1024)throw new InvalidDataException();memory.Write(buffer,0,n);}memory.Position=0;using(var image=Image.FromStream(memory)){if(image.Width>2048||image.Height>2048)continue;using(var bitmap=new Bitmap(image,64,64))bitmap.Save(Path.Combine(Folder,id+".png"),System.Drawing.Imaging.ImageFormat.Png);}}lock(gate)Revision++;}catch(Exception e){Log.Write("Runtime hero portrait",e);}}}catch(Exception e){Log.Write("Runtime portrait catalog",e);}});}}
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
