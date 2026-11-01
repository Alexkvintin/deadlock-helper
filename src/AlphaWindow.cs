using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public class AlphaWindow : Form {
  [StructLayout(LayoutKind.Sequential)]struct PointNative{public int X,Y;public PointNative(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential)]struct SizeNative{public int X,Y;public SizeNative(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Op,Flags,Alpha,Format;}
  [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr h,IntPtr screen,ref PointNative dest,ref SizeNative size,IntPtr memory,ref PointNative source,int key,ref Blend blend,int flags);
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr h,IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  public double BackgroundOpacity{get;protected set;}
  public bool PreviewOnly{get;set;}
  protected AlphaWindow(){BackgroundOpacity=.88;}
  protected Color Surface(Color color){return Color.FromArgb((int)(255*BackgroundOpacity),color);}
  protected virtual void DrawOverlay(Graphics graphics){}
  public Bitmap RenderPixels(){var bitmap=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);DrawOverlay(g);}return bitmap;}
  protected void Present(){if(PreviewOnly||Width<1||Height<1)return;using(var bitmap=RenderPixels()){
   IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),image=IntPtr.Zero,previous=IntPtr.Zero;
   try{image=bitmap.GetHbitmap(Color.FromArgb(0));previous=SelectObject(memory,image);var dest=new PointNative(Left,Top);var source=new PointNative();var size=new SizeNative(Width,Height);var blend=new Blend{Alpha=255,Format=1};if(!UpdateLayeredWindow(Handle,screen,ref dest,ref size,memory,ref source,0,ref blend,2))Log.Write("Layered window failed: "+Marshal.GetLastWin32Error());}
   finally{if(previous!=IntPtr.Zero)SelectObject(memory,previous);if(image!=IntPtr.Zero)DeleteObject(image);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
  }}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){DrawOverlay(e.Graphics);}
 }
}
