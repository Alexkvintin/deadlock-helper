using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DeadlockOverlay {
 public sealed class CounterAdviceWindow:Form {
  readonly HudAdvice advice;readonly Settings settings;readonly Action changed;
  readonly ComboBox mode=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList},marked=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
  readonly Label context=new Label{Dock=DockStyle.Top,Height=58,ForeColor=Theme.Green},note=new Label{Dock=DockStyle.Bottom,Height=88,ForeColor=Theme.Muted};
  readonly FlowLayoutPanel lane=new FlowLayoutPanel{Dock=DockStyle.Top,Height=76,WrapContents=false,AutoScroll=true,Padding=new Padding(6)};
  readonly HeroImages portraits=new HeroImages();readonly ToolTip tips=new ToolTip();
  readonly ListView items=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,ShowItemToolTips=true};
  readonly Button mark=new Button{Dock=DockStyle.Fill},clear=new Button{Dock=DockStyle.Fill},unmark=new Button{Dock=DockStyle.Fill},clearLane=new Button{Dock=DockStyle.Fill};
  ImageList icons;int revision=-1;string laneKey="",language="",markedKey="!";bool updating;
  sealed class Choice {public long Id;public string Name;public override string ToString(){return Name;}}
  public CounterAdviceWindow(HudAdvice advice,Settings settings,Action changed){this.advice=advice;this.settings=settings;this.changed=changed;Text=L.Choose("Counter Items","Контрпредметы");Size=new Size(940,640);MinimumSize=new Size(560,480);BackColor=Theme.Background;ForeColor=Theme.Ink;Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   var bar=new TableLayoutPanel{Dock=DockStyle.Top,Height=114,ColumnCount=2,RowCount=3,Padding=new Padding(8)};bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));for(int i=0;i<3;i++)bar.RowStyles.Add(new RowStyle(SizeType.Percent,33.333f));mode.Dock=DockStyle.Fill;marked.Dock=DockStyle.Fill;bar.Controls.Add(mode,0,0);bar.Controls.Add(clearLane,1,0);bar.Controls.Add(mark,0,1);bar.Controls.Add(clear,1,1);bar.Controls.Add(marked,0,2);bar.Controls.Add(unmark,1,2);
   items.BackColor=Theme.Card;items.ForeColor=Theme.Ink;items.Columns.Add("Item",200);items.Columns.Add("Cost",80);items.Columns.Add("Against",150);items.Columns.Add("Reason",350);items.SelectedIndexChanged+=(s,e)=>ShowReason();
   icons=new ImageList{ImageSize=new Size(32,32),ColorDepth=ColorDepth.Depth32Bit};items.SmallImageList=icons;
   Controls.Add(items);Controls.Add(note);Controls.Add(bar);Controls.Add(lane);Controls.Add(context);Theme.Skin(bar);
   mode.SelectedIndexChanged+=(s,e)=>{if(!updating&&mode.SelectedIndex>=0){settings.CounterMode=mode.SelectedIndex;revision=-1;changed();}};
   clearLane.Click+=(s,e)=>{advice.SelectLane("");revision=-1;};
   mark.Click+=(s,e)=>{if(items.SelectedItems.Count==1){var item=(ItemSuggestion)items.SelectedItems[0].Tag;advice.SetOwned(item.Id,true);revision=-1;}};
   unmark.Click+=(s,e)=>{if(marked.SelectedItem is Choice){advice.SetOwned(((Choice)marked.SelectedItem).Id,false);revision=-1;}};
   clear.Click+=(s,e)=>{advice.ClearOwned();revision=-1;};Resize+=(s,e)=>FitColumns();RefreshAdvice();
  }
  void FitColumns(){if(items.Columns.Count!=4)return;int w=Math.Max(480,items.ClientSize.Width-20);items.Columns[0].Width=(int)(w*.24);items.Columns[1].Width=(int)(w*.17);items.Columns[2].Width=(int)(w*.19);items.Columns[3].Width=w-items.Columns[0].Width-items.Columns[1].Width-items.Columns[2].Width;}
  protected override void OnShown(EventArgs e){base.OnShown(e);Design.Fit(this);FitColumns();}
  public void RefreshAdvice(){if(IsDisposed)return;var snapshot=advice.CurrentContext;string caption=advice.Context+"\n"+(snapshot.LaneStatus??"")+"   |   "+L.Choose("Unspent souls: ","Свободные души: ")+(snapshot.Souls.HasValue?snapshot.Souls.Value.ToString("N0"):L.Choose("unknown","неизвестно"));if(context.Text!=caption)context.Text=caption;
   var selectedLanes=advice.ManualLanes;string nextLane=snapshot.Identity+":"+L.Language+":"+AssetLibrary.Revision+":"+string.Join(",",snapshot.Enemies.Select(p=>CounterContext.Key(p)+"/"+p.Hero+"/"+p.Name))+":"+string.Join(",",selectedLanes);
   updating=true;try{if(language!=L.Language){language=L.Language;Text=L.Choose("Counter Items","Контрпредметы");mark.Text=L.Choose("✓ Mark purchased","✓ Отметить покупку");clear.Text=L.Choose("Clear marks","Сбросить покупки");unmark.Text=L.Choose("Undo selected mark","Отменить выбранную отметку");clearLane.Text=L.Choose("Clear lane selection","Очистить выбор линии");mode.Items.Clear();mode.Items.AddRange(new object[]{L.Choose("Auto (overlay)","Авто (оверлей)"),L.Choose("Team Counters","Против команды"),L.Choose("Lane Counters","Против линии"),L.Choose("Situational Items","Ситуационные предметы")});items.Columns[0].Text=L.Choose("Priority / Item","Приоритет / Предмет");items.Columns[1].Text=L.Choose("Cost","Цена");items.Columns[2].Text=L.Choose("Against","Против");items.Columns[3].Text=L.Choose("Reason","Причина");mode.SelectedIndex=settings.CounterMode;revision=-1;}
    if(nextLane!=laneKey){laneKey=nextLane;lane.SuspendLayout();tips.RemoveAll();while(lane.Controls.Count>0){var c=lane.Controls[0];lane.Controls.Remove(c);c.Dispose();}foreach(var enemy in snapshot.Enemies){string id=CounterContext.Key(enemy);bool chosen=selectedLanes.Contains(id);var b=new Button{Width=(int)(126*DeviceDpi/96f),Height=(int)(48*DeviceDpi/96f),Text=(chosen?"✓ ":"")+LocalSnapshot.HeroName(enemy.Hero),TextAlign=ContentAlignment.MiddleLeft,TextImageRelation=TextImageRelation.ImageBeforeText,Image=portraits.Get(enemy.Hero),BackColor=chosen?Color.FromArgb(28,66,60):Theme.Card,ForeColor=chosen?Theme.Green:Theme.Ink,FlatStyle=FlatStyle.Flat,AutoEllipsis=true};b.Click+=(s,e)=>{advice.ToggleLane(id);};tips.SetToolTip(b,enemy.Name+" · "+L.Choose("Toggle lane opponent","Выбрать соперника линии"));lane.Controls.Add(b);}lane.ResumeLayout();}
    var ids=advice.MarkedOwned;string nextMarked=string.Join(",",ids);if(nextMarked!=markedKey){markedKey=nextMarked;marked.Items.Clear();foreach(var id in ids)marked.Items.Add(new Choice{Id=id,Name=advice.OwnedName(id)});if(marked.Items.Count>0)marked.SelectedIndex=0;unmark.Enabled=marked.Items.Count>0;}
   }finally{updating=false;}
   if(revision==advice.Revision)return;revision=advice.Revision;var selectedMode=(CounterMode)settings.CounterMode;if(selectedMode==CounterMode.Auto)selectedMode=snapshot.LanePhase&&snapshot.LaneEnemies.Length>0?CounterMode.Lane:CounterMode.Team;
   var picks=advice.GetCategory(selectedMode).OrderByDescending(p=>p.Affordable==true).ThenByDescending(p=>p.Priority).ToArray();long previousId=items.SelectedItems.Count==1?((ItemSuggestion)items.SelectedItems[0].Tag).Id:0;
   if(icons.Images.Count>256){icons.Images.Clear();}
   items.BeginUpdate();try{items.Items.Clear();int position=0;foreach(var pick in picks){string reason=Reason(pick);string timing=pick.Affordable==true?L.Choose("Now","Сейчас"):pick.Affordable==false?L.Choose("Later","Позже"):L.Choose("Unknown","Неизвестно");var row=new ListViewItem((++position)+". "+(pick.Name??"?")){Tag=pick,ToolTipText=timing+" · "+reason};row.SubItems.Add((pick.Cost.HasValue?pick.Cost.Value.ToString("N0"):"?")+" · "+timing);row.SubItems.Add(pick.Target??"");row.SubItems.Add(reason);if(pick.Affordable==false)row.ForeColor=Theme.Muted;
     string id=pick.Id.ToString(),path=Path.Combine(HudAdvice.IconFolder,id+".png");try{if(!icons.Images.ContainsKey(id)&&File.Exists(path)){using(var image=Image.FromFile(path))icons.Images.Add(id,new Bitmap(image,32,32));}if(icons.Images.ContainsKey(id))row.ImageKey=id;}catch{}items.Items.Add(row);if(pick.Id==previousId)row.Selected=true;}
   }finally{items.EndUpdate();}FitColumns();ShowReason();
  }
  static string Reason(ItemSuggestion pick){return (string.IsNullOrEmpty(pick.Condition)?"":L.Choose("If: ","Если (EN): ")+pick.Condition+". ")+(string.IsNullOrEmpty(pick.Why)?L.Choose("Reason unavailable","Причина неизвестна"):pick.Why);}
  void ShowReason(){if(items.SelectedItems.Count>0){var p=(ItemSuggestion)items.SelectedItems[0].Tag;note.Text=Reason(p);}else note.Text=L.Choose("Inventory is not verified. Mark items you already own. Situational advice is conditional on the displayed situation; English explanations are from the source.","Инвентарь не подтверждён. Отметьте купленные предметы. Ситуационные советы условны; пояснения источника на английском.") + " ("+advice.MarkedOwned.Length+")";}
  protected override void Dispose(bool disposing){if(disposing){tips.Dispose();portraits.Dispose();if(icons!=null)icons.Dispose();}base.Dispose(disposing);}
 }
}
