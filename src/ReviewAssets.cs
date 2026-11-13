using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace DeadlockOverlay {
 public sealed class ReviewItem {public long Id;public string Name,Kind,Image;public bool Shop;}
 public sealed class ReviewAssets:IDisposable {
  readonly CancellationTokenSource cancel=new CancellationTokenSource();Task worker;public event Action Changed;
  volatile Dictionary<long,ReviewItem> items=new Dictionary<long,ReviewItem>();
  public ReviewItem Get(long id){ReviewItem item;return items.TryGetValue(id,out item)?item:null;}
  public void Load(long[] ids,bool online){if(worker!=null)return;worker=Task.Run(()=>{try{
   string folder=Path.Combine(SettingsManager.Folder,"counter-helper"),path=Path.Combine(folder,"items.json");var json=Json.Serializer();json.MaxJsonLength=16*1024*1024;string text=File.Exists(path)&&new FileInfo(path).Length<16*1024*1024?File.ReadAllText(path):null;
   if(text==null&&online){text=System.Text.Encoding.UTF8.GetString(Download("https://api.deadlock-api.com/v1/assets/items?language=english",16*1024*1024));Directory.CreateDirectory(folder);File.WriteAllText(path,text);}
   if(text==null)return;var map=new Dictionary<long,ReviewItem>();foreach(var raw in json.DeserializeObject(text) as object[]??new object[0]){var d=Json.Obj(raw);long id=Json.Number(d,"id")??0;if(id<=0)continue;map[id]=new ReviewItem{Id=id,Name=Json.Str(d,"name"),Kind=Json.Str(d,"item_slot_type"),Shop=Json.Str(d,"type")=="upgrade"&&Json.Bool(d,"shopable"),Image=Json.Str(d,"shop_image").Length>0?Json.Str(d,"shop_image"):Json.Str(d,"image")};}items=map;Notify();if(!online)return;
   Directory.CreateDirectory(HudAdvice.IconFolder);foreach(long id in ids.Distinct().Take(64)){cancel.Token.ThrowIfCancellationRequested();var item=Get(id);string file=Path.Combine(HudAdvice.IconFolder,id+".png");if(item==null||!item.Shop||File.Exists(file))continue;Uri uri;if(!Uri.TryCreate(item.Image,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="assets-bucket.deadlock-api.com")continue;try{using(var stream=new MemoryStream(Download(item.Image,4*1024*1024)))using(var image=Image.FromStream(stream)){if(image.Width>2048||image.Height>2048)continue;using(var bitmap=new Bitmap(image,64,64))bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}Notify();}catch(WebException){break;}}
  }catch(OperationCanceledException){}catch(Exception e){Log.Write("Review item assets",e);}});}
  byte[] Download(string url,int limit){var request=(HttpWebRequest)WebRequest.Create(url);request.AllowAutoRedirect=false;request.Timeout=10000;request.ReadWriteTimeout=10000;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;using(cancel.Token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){if((int)response.StatusCode!=200)throw new InvalidDataException();byte[] buffer=new byte[8192];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){cancel.Token.ThrowIfCancellationRequested();if(memory.Length+count>limit)throw new InvalidDataException("Asset too large");memory.Write(buffer,0,count);}return memory.ToArray();}}
  void Notify(){var handler=Changed;if(!cancel.IsCancellationRequested&&handler!=null)handler();}
  public void Dispose(){Changed=null;cancel.Cancel();if(worker!=null)worker.ContinueWith(_=>cancel.Dispose());else cancel.Dispose();}
 }
}
