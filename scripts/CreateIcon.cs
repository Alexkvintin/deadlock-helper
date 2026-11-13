using System;using System.IO;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Collections.Generic;
class CreateIcon {
 static void Main(){var sizes=new[]{16,24,32,48,64,128,256};var data=new List<byte[]>();foreach(int size in sizes)using(var b=new Bitmap(size,size))using(var g=Graphics.FromImage(b)){
  g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);g.Clear(Color.Transparent);
  using(var dark=new SolidBrush(Color.FromArgb(11,22,28)))g.FillEllipse(dark,8,8,240,240);
  using(var gold=new Pen(Color.FromArgb(210,178,113),9)){g.DrawArc(gold,26,26,204,204,35,110);g.DrawArc(gold,26,26,204,204,215,110);g.DrawLine(gold,128,13,128,39);g.DrawLine(gold,128,217,128,243);}
  using(var teal=new SolidBrush(Color.FromArgb(91,218,180)))g.FillPolygon(teal,new[]{new Point(145,47),new Point(84,139),new Point(123,133),new Point(108,209),new Point(178,109),new Point(136,116)});
  using(var stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);data.Add(stream.ToArray());}
 }
 using(var file=new BinaryWriter(File.Create("assets/companion.ico"))){file.Write((short)0);file.Write((short)1);file.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)0);file.Write((byte)0);file.Write((short)1);file.Write((short)32);file.Write(data[i].Length);file.Write(offset);offset+=data[i].Length;}foreach(var bytes in data)file.Write(bytes);}
 }
}
