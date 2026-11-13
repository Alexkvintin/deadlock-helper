using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class ArtCache : IDisposable {
  readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
  int revision;
  public Image Get(string name){if(revision!=AssetLibrary.Revision){Dispose();revision=AssetLibrary.Revision;}Image found;if(images.TryGetValue(name,out found))return found;
   string path=name.StartsWith("team-emblem-")?AssetLibrary.Resolve(Path.Combine("ui","patron-"+name.Substring(12)+".png")):AssetLibrary.Resolve(Path.Combine("local-art",name+".png"));
   try{if(File.Exists(path))using(var original=Image.FromFile(path)){int limit=name=="background"||name=="location"?1600:name.StartsWith("hero-")?700:160;double factor=Math.Min(1,limit/(double)Math.Max(original.Width,original.Height));found=new Bitmap(original,Math.Max(1,(int)(original.Width*factor)),Math.Max(1,(int)(original.Height*factor)));}}catch{found=null;}
   images[name]=found;return found;
  }
  public void Dispose(){foreach(var i in images.Values)if(i!=null)i.Dispose();images.Clear();}
 }
 public static class Design {
  public static readonly Color Line=Color.FromArgb(83,77,57);
  public static Icon AppIcon {get{try{return Icon.ExtractAssociatedIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DeadlockHelper.exe"));}catch{return SystemIcons.Application;}}}
  public static void Contain(Graphics g,Image image,RectangleF box,Color tint){float scale=Math.Min(box.Width/image.Width,box.Height/image.Height);var dest=new Rectangle((int)(box.X+(box.Width-image.Width*scale)/2),(int)(box.Y+(box.Height-image.Height*scale)/2),(int)(image.Width*scale),(int)(image.Height*scale));using(var attributes=new System.Drawing.Imaging.ImageAttributes()){var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix00=tint.R/255f;matrix.Matrix11=tint.G/255f;matrix.Matrix22=tint.B/255f;attributes.SetColorMatrix(matrix);g.DrawImage(image,dest,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}}
  public static void Frame(Graphics g,RectangleF rect,Color accent){
   using(var path=Theme.Round(rect,8))using(var pen=new Pen(Color.FromArgb(135,accent),1))g.DrawPath(pen,path);
   using(var pen=new Pen(accent,1)){float x=rect.Left+9,y=rect.Top+9;g.DrawLine(pen,x,y,x+13,y);g.DrawLine(pen,x,y,x,y+13);x=rect.Right-9;y=rect.Bottom-9;g.DrawLine(pen,x,y,x-13,y);g.DrawLine(pen,x,y,x,y-13);}
  }
  public static void Text(Graphics g,string text,float size,Color color,RectangleF box,bool bold=false){using(var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color))using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text??"",font,brush,box,format);}
  public static void Cover(Graphics g,Image image,Rectangle rect){if(image==null)return;float ratio=Math.Max(rect.Width/(float)image.Width,rect.Height/(float)image.Height);var saved=g.Save();g.SetClip(rect,CombineMode.Intersect);g.DrawImage(image,rect.X+(rect.Width-image.Width*ratio)/2,rect.Y+(rect.Height-image.Height*ratio)/2,image.Width*ratio,image.Height*ratio);g.Restore(saved);}
  public static void Fit(Form form){var area=Screen.FromControl(form).WorkingArea;form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,area.Width),Math.Min(form.MinimumSize.Height,area.Height));form.Size=new Size(Math.Min(form.Width,area.Width),Math.Min(form.Height,area.Height));if(!area.Contains(form.Bounds))form.Location=new Point(area.Left+Math.Max(0,(area.Width-form.Width)/2),area.Top+Math.Max(0,(area.Height-form.Height)/2));}
 }
 public sealed class Toggle : CheckBox {
  public Toggle(){AutoSize=false;Height=44;Width=410;BackColor=Theme.Card;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  public override Size GetPreferredSize(Size proposed){float unit=DeviceDpi/96f;int width=Math.Max(120,proposed.Width);var text=TextRenderer.MeasureText(Text,Font,new Size(Math.Max(1,width-(int)(60*unit)),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);return new Size(width,Math.Max((int)(44*unit),text.Height+(int)(18*unit)));}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BackColor);g.SetClip(ClientRectangle);g.SmoothingMode=SmoothingMode.AntiAlias;float unit=DeviceDpi/96f,top=(Height-18*unit)/2;Theme.Fill(g,Checked?Theme.Green:Color.FromArgb(54,65,68),new RectangleF(8*unit,top,34*unit,18*unit),9*unit);using(var brush=new SolidBrush(Checked?Theme.Background:Theme.Muted))g.FillEllipse(brush,(Checked?27:11)*unit,top+3*unit,12*unit,12*unit);TextRenderer.DrawText(g,Text,Font,new Rectangle((int)(54*unit),4,Math.Max(1,Width-(int)(62*unit)),Height-8),Enabled?ForeColor:Theme.Muted,BackColor,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping);if(Focused)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
 }
 public sealed class RangeSlider : Control {
  int minimum,maximum=100,value;bool dragging;
  public int Minimum{get{return minimum;}set{minimum=value;}}
  public int Maximum{get{return maximum;}set{maximum=value;}}
  public int Value{get{return value;}set{int next=Math.Max(minimum,Math.Min(maximum,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
  public event EventHandler ValueChanged;
  public RangeSlider(){BackColor=Theme.Card;DoubleBuffered=true;TabStop=true;Cursor=Cursors.Hand;Height=36;Width=400;AccessibleRole=AccessibleRole.Slider;}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float x=10+(Width-20)*(value-minimum)/(float)Math.Max(1,maximum-minimum);Theme.Fill(g,Color.FromArgb(48,64,70),new RectangleF(10,Height/2-3,Width-20,6),3);if(x>10)Theme.Fill(g,Theme.Green,new RectangleF(10,Height/2-3,x-10,6),Math.Min(3,(x-10)/2));using(var brush=new SolidBrush(Theme.Green))g.FillEllipse(brush,x-7,Height/2-7,14,14);if(Focused)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
  void Seek(int x){Value=minimum+(int)(Math.Max(0,Math.Min(1,(x-10)/(double)Math.Max(1,Width-20)))*(maximum-minimum));}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();dragging=true;Capture=true;Seek(e.X);}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Seek(e.X);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=false;}
  protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left)Value--;if(e.KeyCode==Keys.Right)Value++;if(e.KeyCode==Keys.Home)Value=minimum;if(e.KeyCode==Keys.End)Value=maximum;}
 }
}
