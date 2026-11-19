using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DeadlockOverlay {
 public sealed class CounterAdviceWindow:Form {
  readonly HudAdvice advice;readonly Settings settings;readonly Action changed;
  readonly ComboBox mode=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList},lane=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
  readonly Label context=new Label{Dock=DockStyle.Top,Height=58,ForeColor=Theme.Green},note=new Label{Dock=DockStyle.Bottom,Height=76,ForeColor=Theme.Muted};
  readonly ListView items=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,ShowItemToolTips=true};
  readonly Button mark=new Button{Dock=DockStyle.Fill},clear=new Button{Dock=DockStyle.Fill};
  ImageList icons;int revision=-1;string laneKey="",language="";bool updating;
  sealed class Choice {public string Key,Name;public override string ToString(){return Name;}}
  public CounterAdviceWindow(HudAdvice advice,Settings settings,Action changed){this.advice=advice;this.settings=settings;this.changed=changed;Text=L.Choose("Counter Items","Контрпредметы");Size=new Size(940,640);MinimumSize=new Size(560,420);BackColor=Theme.Background;ForeColor=Theme.Ink;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
   var bar=new TableLayoutPanel{Dock=DockStyle.Top,Height=100,ColumnCount=2,RowCount=2,Padding=new Padding(8)};bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));mode.Dock=DockStyle.Fill;lane.Dock=DockStyle.Fill;bar.Controls.Add(mode,0,0);bar.Controls.Add(lane,1,0);bar.Controls.Add(mark,0,1);bar.Controls.Add(clear,1,1);
   items.BackColor=Theme.Card;items.ForeColor=Theme.Ink;items.Columns.Add("Item",200);items.Columns.Add("Cost",80);items.Columns.Add("Against",150);items.Columns.Add("Reason",350);items.SelectedIndexChanged+=(s,e)=>ShowReason();
   Controls.Add(items);Controls.Add(note);Controls.Add(bar);Controls.Add(context);Theme.Skin(bar);mode.SelectedIndexChanged+=(s,e)=>{if(!updating&&mode.SelectedIndex>=0){settings.CounterMode=mode.SelectedIndex;revision=-1;changed();}};lane.SelectedIndexChanged+=(s,e)=>{if(!updating&&lane.SelectedItem is Choice){advice.SelectLane(((Choice)lane.SelectedItem).Key);revision=-1;}};mark.Click+=(s,e)=>{if(items.SelectedItems.Count==1){var item=(ItemSuggestion)items.SelectedItems[0].Tag;advice.SetOwned(item.Id,true);revision=-1;}};clear.Click+=(s,e)=>{advice.ClearOwned();revision=-1;};Resize+=(s,e)=>FitColumns();RefreshAdvice();
  }
  void FitColumns(){if(items.Columns.Count!=4)return;int w=Math.Max(480,items.ClientSize.Width-20);items.Columns[0].Width=(int)(w*.24);items.Columns[1].Width=(int)(w*.12);items.Columns[2].Width=(int)(w*.24);items.Columns[3].Width=w-items.Columns[0].Width-items.Columns[1].Width-items.Columns[2].Width;}
  protected override void OnShown(EventArgs e){base.OnShown(e);Design.Fit(this);FitColumns();}
  public void RefreshAdvice(){if(IsDisposed)return;var snapshot=advice.CurrentContext;context.Text=advice.Context+"\n"+(snapshot.LaneStatus??"")+"   |   "+L.Choose("Unspent souls: ","Свободные души: ")+(snapshot.Souls.HasValue?snapshot.Souls.Value.ToString("N0"):L.Choose("unknown","неизвестно"));
   string nextLane=snapshot.Identity+":"+L.Language+":"+string.Join(",",snapshot.Enemies.Select(p=>CounterContext.Key(p)+"/"+p.Hero+"/"+p.Name));
   updating=true;try{if(language!=L.Language){language=L.Language;Text=L.Choose("Counter Items","Контрпредметы");mark.Text=L.Choose("Mark selected item owned","Отметить предмет купленным");clear.Text=L.Choose("Clear marked items","Сбросить метки покупок");mode.Items.Clear();mode.Items.AddRange(new object[]{L.Choose("Auto (overlay)","Авто (оверлей)"),L.Choose("Team Counters","Против команды"),L.Choose("Lane Counters","Против линии"),L.Choose("Situational Items","Ситуационные предметы")});items.Columns[0].Text=L.Choose("Item","Предмет");items.Columns[1].Text=L.Choose("Cost","Цена");items.Columns[2].Text=L.Choose("Against","Против");items.Columns[3].Text=L.Choose("Reason","Причина");mode.SelectedIndex=settings.CounterMode;revision=-1;}
    if(nextLane!=laneKey){laneKey=nextLane;lane.Items.Clear();lane.Items.Add(new Choice{Key="",Name=L.Choose("Automatic / unknown lane","Авто / линия неизвестна")});foreach(var enemy in snapshot.Enemies)lane.Items.Add(new Choice{Key=CounterContext.Key(enemy),Name=(enemy.HeroName??LocalSnapshot.HeroName(enemy.Hero))+" - "+enemy.Name});int selected=0;for(int i=1;i<lane.Items.Count;i++)if(((Choice)lane.Items[i]).Key==advice.ManualLane)selected=i;lane.SelectedIndex=selected;}
   }finally{updating=false;}
   if(revision==advice.Revision)return;revision=advice.Revision;var selectedMode=(CounterMode)settings.CounterMode;if(selectedMode==CounterMode.Auto)selectedMode=snapshot.LanePhase&&snapshot.LaneEnemies.Length>0?CounterMode.Lane:CounterMode.Team;
   var picks=advice.GetCategory(selectedMode);items.BeginUpdate();try{items.Items.Clear();var previous=icons;icons=new ImageList{ImageSize=new Size(32,32),ColorDepth=ColorDepth.Depth32Bit};items.SmallImageList=icons;if(previous!=null)previous.Dispose();foreach(var pick in picks){string reason=Reason(pick);var row=new ListViewItem(pick.Name??"?"){Tag=pick,ToolTipText=reason};row.SubItems.Add(pick.Cost.HasValue?pick.Cost.Value.ToString("N0"):"?");row.SubItems.Add(pick.Target??"");row.SubItems.Add(reason);if(pick.Affordable==false)row.ForeColor=Theme.Muted;
     string path=Path.Combine(HudAdvice.IconFolder,pick.Id+".png");try{if(File.Exists(path)){using(var image=Image.FromFile(path)){icons.Images.Add(image);}row.ImageIndex=icons.Images.Count-1;}}catch{}items.Items.Add(row);}
   }finally{items.EndUpdate();}FitColumns();ShowReason();
  }
  static string Reason(ItemSuggestion pick){return (string.IsNullOrEmpty(pick.Condition)?"":L.Choose("If: ","Если (EN): ")+pick.Condition+". ")+(string.IsNullOrEmpty(pick.Why)?L.Choose("Reason unavailable","Причина неизвестна"):pick.Why);}
  void ShowReason(){if(items.SelectedItems.Count>0){var p=(ItemSuggestion)items.SelectedItems[0].Tag;note.Text=Reason(p);}else note.Text=L.Choose("Inventory is not verified. Mark items you already own. Situational advice is conditional on the displayed situation; English explanations are from the source.","Инвентарь не подтверждён. Отметьте купленные предметы. Ситуационные советы условны; пояснения источника на английском.") + " ("+advice.MarkedOwned.Length+")";}
  protected override void Dispose(bool disposing){if(disposing&&icons!=null)icons.Dispose();base.Dispose(disposing);}
 }
}
