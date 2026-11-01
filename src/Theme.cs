using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeadlockOverlay {
    public static class Theme {
        public static readonly Color SoulsBlue=Color.FromArgb(113,193,255);
        public static void DrawHeroBalance(Graphics g,string hero,long? balance,bool fresh,Font font,float x,float y,float width){
            string suffix=" ("+(fresh&&balance.HasValue?OverlayRenderer.Format(balance.Value,false):"—")+")";
            float reserve=g.MeasureString(suffix,font).Width;float nameWidth=Math.Max(1,Math.Min(g.MeasureString(hero??"",font).Width,width-reserve));
            using(var text=new SolidBrush(Ink))using(var blue=new SolidBrush(SoulsBlue))using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}){
                OverlayText.Draw(g,hero??"",font,text,new RectangleF(x,y,nameWidth,22),format);OverlayText.Draw(g,suffix,font,blue,new RectangleF(x+nameWidth,y,Math.Max(1,width-nameWidth),22),format);
            }
        }
        public static readonly Color HeroDamage=Color.FromArgb(255,222,117),StructureDamage=Color.FromArgb(203,177,255),Healing=Color.FromArgb(124,239,174);
        public static readonly Color Background=Color.FromArgb(11,18,22),Card=Color.FromArgb(19,30,34),Ink=Color.FromArgb(241,233,214),Muted=Color.FromArgb(157,175,177),Gold=Color.FromArgb(210,178,113),Green=Color.FromArgb(91,218,180);
        public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static void Fill(Graphics g,Color c,RectangleF r,float radius){using(var p=Round(r,radius))using(var b=new SolidBrush(c))g.FillPath(b,p);}
        public static void Skin(Control c){
            c.ForeColor=Ink;
            var b=c as Button;if(b!=null){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Card;b.Height=Math.Max(38,b.Height);b.Cursor=Cursors.Hand;b.Resize+=(s,e)=>{var old=b.Region;using(var p=Round(new RectangleF(0,0,b.Width,b.Height),10))b.Region=new Region(p);if(old!=null)old.Dispose();};using(var p=Round(new RectangleF(0,0,b.Width,b.Height),10))b.Region=new Region(p);}
            if(c is TextBox||c is ComboBox||c is NumericUpDown)c.BackColor=Card;
            var combo=c as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;combo.DrawItem+=(s,e)=>{if(e.Index<0)return;using(var background=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(55,65,54):Card))e.Graphics.FillRectangle(background,e.Bounds);using(var text=new SolidBrush(Ink))e.Graphics.DrawString(L.ToLanguage(Convert.ToString(combo.Items[e.Index])),combo.Font,text,e.Bounds.X+8,e.Bounds.Y+3);};}
            foreach(Control child in c.Controls)Skin(child);
        }
    }
}

