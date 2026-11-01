using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeadlockOverlay {
 public sealed class ScrollSafeComboBox:ComboBox {
  protected override void WndProc(ref Message m){if(m.Msg==0x20A&&!DroppedDown){var page=Parent;while(page!=null&&!(page is SettingsPage))page=page.Parent;if(page!=null)((SettingsPage)page).ScrollBy(-((short)((long)m.WParam>>16))/120*72);return;}base.WndProc(ref m);}
 }
 public sealed class QuietTable:TableLayoutPanel{public QuietTable(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}}
 public sealed class SettingsPage:Panel {
  readonly TableLayoutPanel rows=new QuietTable();readonly ScrollRail rail=new ScrollRail();readonly Timer motion=new Timer{Interval=16};bool arranging;int offset,target,extent;
  public SettingsPage(){DoubleBuffered=true;BackColor=Color.FromArgb(16,24,29);AutoScroll=false;Padding=new Padding(24,12,24,24);SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
   rows.ColumnCount=1;rows.RowCount=0;rows.AutoSize=false;rows.GrowStyle=TableLayoutPanelGrowStyle.AddRows;rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));rows.BackColor=BackColor;Controls.Add(rows);Controls.Add(rail);rail.BringToFront();rail.Seek+=value=>{target=offset=value;PositionRows();};motion.Tick+=(a,e)=>{int delta=target-offset;if(Math.Abs(delta)<2){offset=target;motion.Stop();}else offset+=(int)(Math.Sign(delta)*Math.Max(1,Math.Abs(delta)*.32));PositionRows();};
  }
  public void ScrollBy(int pixels){target=Math.Max(0,Math.Min(extent,target+pixels));if(target!=offset)motion.Start();}
  public int ScrollOffset{get{return offset;}}
  protected override void OnMouseWheel(MouseEventArgs e){ScrollBy(-e.Delta/120*72);var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
  void PositionRows(){rows.Location=new Point(Padding.Left,Padding.Top-offset);rail.SetRange(extent,ClientSize.Height,offset);}
  void HookWheel(Control c){c.Enter+=(a,e)=>{var current=c;int y=0;while(current!=null&&current!=rows){y+=current.Top;current=current.Parent;}if(current==rows){if(y<offset)ScrollBy(y-target);else if(y+c.Height>offset+ClientSize.Height-Padding.Vertical)ScrollBy(y+c.Height-ClientSize.Height+Padding.Vertical-target);}};c.MouseWheel+=(a,e)=>{ScrollBy(-e.Delta/120*72);var h=e as HandledMouseEventArgs;if(h!=null)h.Handled=true;};foreach(Control child in c.Controls)HookWheel(child);}
  public void AddSetting(Control c){c.Anchor=AnchorStyles.Left|AnchorStyles.Right;c.Margin=new Padding(0,7,0,9);if(c is Label){c.AutoSize=true;c.MinimumSize=Size.Empty;c.BackColor=BackColor;}HookWheel(c);rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));rows.Controls.Add(c,0,rows.RowCount++);}
  protected override void OnLayout(LayoutEventArgs e){if(arranging)return;arranging=true;try{
   base.OnLayout(e);int width=Math.Max(120,ClientSize.Width-Padding.Horizontal-12);rows.SuspendLayout();try{
    rows.MaximumSize=new Size(width,0);rows.Width=width;
    foreach(Control c in rows.Controls){var label=c as Label;if(label!=null){label.MinimumSize=new Size(width-c.Margin.Horizontal,0);label.MaximumSize=new Size(width-c.Margin.Horizontal,0);}c.Width=width-c.Margin.Horizontal;var toggle=c as Toggle;if(toggle!=null)toggle.Height=toggle.GetPreferredSize(new Size(c.Width,0)).Height;var button=c as Button;if(button!=null)button.Height=Math.Max((int)(44*DeviceDpi/96f),TextRenderer.MeasureText(button.Text,button.Font,new Size(c.Width-20,int.MaxValue),TextFormatFlags.WordBreak).Height+18);}
   }finally{rows.ResumeLayout(true);}int bottom=0;foreach(Control child in rows.Controls)bottom=Math.Max(bottom,child.Bottom+child.Margin.Bottom);rows.Height=bottom;extent=Math.Max(0,bottom+Padding.Vertical-ClientSize.Height);target=Math.Min(target,extent);offset=Math.Min(offset,extent);rail.Bounds=new Rectangle(ClientSize.Width-12,12,8,Math.Max(1,ClientSize.Height-24));PositionRows();
  }finally{arranging=false;}}
  protected override void Dispose(bool disposing){if(disposing)motion.Dispose();base.Dispose(disposing);}
 }
 public sealed class ScrollRail:Control {
  int maximum,viewport,position;bool dragging;int grab;public event Action<int> Seek;
  public ScrollRail(){DoubleBuffered=true;BackColor=Color.FromArgb(16,24,29);Cursor=Cursors.Hand;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  Rectangle Thumb{get{int h=Math.Min(Height,Math.Max(32,(int)(Height*viewport/(double)Math.Max(1,viewport+maximum))));return new Rectangle(1,(int)((Height-h)*position/(double)Math.Max(1,maximum)),Math.Max(1,Width-2),h);}}
  public void SetRange(int max,int view,int value){maximum=max;viewport=view;position=value;Visible=max>0;Invalidate();}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;Theme.Fill(e.Graphics,dragging?Theme.Green:Color.FromArgb(78,100,104),Thumb,3);}
  void MoveTo(int y){var t=Thumb;int value=(int)(Math.Max(0,Math.Min(1,(y-grab)/(double)Math.Max(1,Height-t.Height)))*maximum);if(Seek!=null)Seek(value);}
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;var t=Thumb;grab=t.Contains(e.Location)?e.Y-t.Top:t.Height/2;dragging=true;Capture=true;MoveTo(e.Y);}
  protected override void OnMouseMove(MouseEventArgs e){if(dragging)MoveTo(e.Y);}
  protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();}
  protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;}
 }
}
