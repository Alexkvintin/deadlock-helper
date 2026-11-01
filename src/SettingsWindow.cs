using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DeadlockOverlay {
    public sealed class SettingsWindow : Form {
        public event Action<long> ReconnectRequested;
        public event Action<long> ViewMatchRequested;
        public event Action EndSessionRequested;
        readonly Settings settings;
        readonly Action changed;
        readonly Action<bool> demo;
        readonly Label status=new Label();
        CheckBox demoCheck,layoutCheck;
        protected override bool ShowWithoutActivation{get{return true;}}
        readonly Panel pageHost=new Panel();
        readonly SettingsPage[] pages=new SettingsPage[6];
        bool starting=true; int page=0,selectedPage=0;
        readonly FlowLayoutPanel navigation=new FlowLayoutPanel();
        readonly Label pageTitle=new Label();
        OverlayPreview preview;readonly System.Collections.Generic.Dictionary<int,CheckBox> partChecks=new System.Collections.Generic.Dictionary<int,CheckBox>();
        readonly ArtCache art=new ArtCache();Bitmap background;int backgroundRevision;
        string[] PageNames{get{return new[]{L.Choose("General","Общие"),L.Choose("Overlay","Оверлей"),L.Choose("Statistics","Статистика"),L.Choose("Appearance","Внешний вид"),L.Choose("Hotkeys","Горячие клавиши"),L.Choose("About","О программе")};}}
        void Add(Control c){pages[page].AddSetting(c);}
        public void SelectPage(int index){if(index==3)index=1;if(index<0||index>=pages.Length)return;SuspendLayout();selectedPage=index;pageHost.SuspendLayout();for(int i=0;i<pages.Length;i++)pages[i].Visible=i==index;pages[index].BringToFront();pages[index].PerformLayout();pageHost.ResumeLayout(true);pageHost.Invalidate(true);pageTitle.Text=PageNames[index];int n=0;foreach(Control c in navigation.Controls){c.Text=PageNames[n];c.BackColor=n++==index?Color.FromArgb(27,64,61):Theme.Background;}if(preview!=null){preview.Visible=index==1;preview.Width=Math.Max(360,Math.Min(700,ClientSize.Width/2));preview.Invalidate();}ResumeLayout(true);}
        protected override void OnShown(EventArgs e){base.OnShown(e);Design.Fit(this);}
        protected override void Dispose(bool disposing){if(disposing){art.Dispose();if(background!=null)background.Dispose();}base.Dispose(disposing);}
        protected override void OnPaintBackground(PaintEventArgs e){
            if(ClientSize.Width<1||ClientSize.Height<1)return;
            if(background==null||background.Size!=ClientSize||backgroundRevision!=AssetLibrary.Revision){if(background!=null)background.Dispose();background=new Bitmap(ClientSize.Width,ClientSize.Height);backgroundRevision=AssetLibrary.Revision;using(var g=Graphics.FromImage(background)){g.Clear(Theme.Background);Design.Cover(g,art.Get("background"),ClientRectangle);using(var b=new SolidBrush(Color.FromArgb(165,Theme.Background)))g.FillRectangle(b,ClientRectangle);}}
            e.Graphics.DrawImageUnscaled(background,0,0);
        }

        public SettingsWindow(Settings settings,Action changed,Action<bool> demo) {
            this.settings=settings; this.changed=changed; this.demo=demo;
            L.Language=settings.Language;
            Text="Deadlock Helper by kvintin · v1.0"; Size=new Size(1320,880); MinimumSize=new Size(860,560); Icon=Design.AppIcon; StartPosition=FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Background; ForeColor=Theme.Ink; Font=new Font("Bahnschrift",11f);
            AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            pageHost.Dock=DockStyle.Fill;pageHost.BackColor=Color.FromArgb(16,24,29);
            for(int i=0;i<pages.Length;i++){pages[i]=new SettingsPage{Dock=DockStyle.Fill,Visible=false};pageHost.Controls.Add(pages[i]);}
            var header=new Panel{Dock=DockStyle.Top,Height=86,BackColor=Color.Transparent};
            var title=new Label{Text="DEADLOCK HELPER",Font=new Font("Palatino Linotype",23,FontStyle.Regular),ForeColor=Theme.Gold,AutoSize=true,Location=new Point(26,18),BackColor=Color.Transparent};header.Controls.Add(title);
            header.Controls.Add(new Label{Text="by kvintin",Font=new Font("Bahnschrift",10),ForeColor=Theme.Green,AutoSize=true,Location=new Point(29,54),BackColor=Color.Transparent});
            navigation.Dock=DockStyle.Left;navigation.Width=185;navigation.FlowDirection=FlowDirection.TopDown;navigation.WrapContents=false;navigation.Padding=new Padding(12,18,10,0);navigation.BackColor=Theme.Background;
            for(int i=0;i<PageNames.Length;i++){int target=i;var b=new Button{Text=PageNames[i],Width=158,Height=46,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,0,0,8)};b.Click+=(a,e)=>SelectPage(target);if(i==3)b.Visible=false;navigation.Controls.Add(b);}
            preview=new OverlayPreview(settings){Dock=DockStyle.Right,Width=530};
            pageTitle.BackColor=Color.FromArgb(16,24,29);pageTitle.Dock=DockStyle.Top;pageTitle.Height=62;pageTitle.Font=new Font("Bahnschrift",21,FontStyle.Regular);pageTitle.Padding=new Padding(24,18,0,0);pageTitle.ForeColor=Theme.Ink;
            var content=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(16,24,29)};content.Controls.Add(pageHost);content.Controls.Add(pageTitle);Controls.Add(content);Controls.Add(preview);Controls.Add(navigation);Controls.Add(header);
            Resize+=(a,e)=>{preview.Visible=selectedPage==1;preview.Width=Math.Max(340,Math.Min(700,ClientSize.Width/2));};
            status.AutoSize=false; status.Size=new Size(405,58); status.ForeColor=Color.FromArgb(100,220,192); Add(status);
            var language=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=400};language.Items.AddRange(new object[]{"English (EN)",L.T("Русский (RU)")});language.SelectedIndex=settings.Language=="ru"?1:0;
            language.SelectedIndexChanged+=(a,b)=>{settings.Language=language.SelectedIndex==1?"ru":"en";L.Language=settings.Language;L.Apply(this);SelectPage(selectedPage);Save();};Add(language);
            var automatic=Check(L.T("Автоматически определять активный матч"),settings.AutoMatch,v=>settings.AutoMatch=v);
            Check(L.Choose("Screen recognition (match, hero, clock)","Распознавание экрана (матч, герой, время)"),settings.ScreenRecognition,v=>settings.ScreenRecognition=v);
            Check(L.T("Показывать сводку после выхода из матча"),settings.ShowSummary,v=>settings.ShowSummary=v);
            page=1;Section(L.Choose("LAYOUT & APPEARANCE","РАСПОЛОЖЕНИЕ И ОФОРМЛЕНИЕ"));LayoutControls();
            Check(L.Choose("Keep panels from overlapping","Не допускать пересечения панелей"),settings.AvoidOverlap,v=>settings.AvoidOverlap=v);
            PartCheck(0);PartCheck(1);
            Section(L.T("ПОДСКАЗКИ ПО ПРЕДМЕТАМ"));
            PartCheck(2);
            Add(new Label{Text=L.Choose("Recommendations follow enemy portraits. Place each map timer wherever it suits you.","Подсказки следуют за портретами противников. Каждый таймер можно разместить отдельно."),Width=400,Height=62,ForeColor=Theme.Muted});
            Section(L.T("ТАЙМЕРЫ КАРТЫ"));
            PartCheck(5);
            PartCheck(3);
            PartCheck(4);
            page=1;
            Section(L.Choose("GAME ART LIBRARY","БИБЛИОТЕКА ИЗОБРАЖЕНИЙ"));
            Add(new Label{Text=L.Choose("Import images you are permitted to use. Stored independently of program updates. Importing files does not grant redistribution rights.","Импортируйте изображения, которые вправе использовать. Они сохранятся после обновления программы. Импорт не даёт права на распространение."),AutoSize=true});
            Button(L.Choose("Import an existing assets folder","Импортировать папку изображений"),()=>{using(var dialog=new FolderBrowserDialog{Description=L.Choose("Select your existing assets folder","Выберите существующую папку assets")})if(dialog.ShowDialog(this)==DialogResult.OK){int count=AssetLibrary.Import(dialog.SelectedPath);preview.Invalidate();Invalidate(true);MessageBox.Show(this,L.Choose("Images imported: ","Импортировано изображений: ")+count);}});
            page=0;Section(L.T("ПОДКЛЮЧЕНИЕ К МАТЧУ"));
            Add(new Label {Text=L.T("ID матча (указан внизу справа в игре)"),AutoSize=true});
            var matchInput=new TextBox {Text=settings.LiveMatchId>0?settings.LiveMatchId.ToString():"",Width=400};Add(matchInput);
            Button(L.T("Подключиться вручную"),()=>{long id;if(!long.TryParse(matchInput.Text.Trim(),out id)||id<=0)throw new Exception(L.T("Введите числовой ID матча"));long previous=settings.LiveMatchId;automatic.Checked=false;settings.LiveMatchId=id;Save();if(previous==id&&ReconnectRequested!=null)ReconnectRequested(id);if(ViewMatchRequested!=null)ViewMatchRequested(id);});
            Button(L.T("Отключиться от трансляции"),()=>{automatic.Checked=false;settings.LiveMatchId=0;matchInput.Text="";Save();});
            Button(L.Choose("End session and show report","Завершить сеанс и показать сводку"),()=>{if(EndSessionRequested!=null)EndSessionRequested();});
            page=1;Check(L.T("Показывать оверлей"),settings.Enabled,v=>settings.Enabled=v);
            Section(L.T("ВИД ОВЕРЛЕЯ"));
            demoCheck=Check(L.T("Тестовый матч 6 × 6 (имитация)"),false,v=>demo(v));
            page=2;Check(L.T("Сортировать по урону"),settings.Sort,v=>settings.Sort=v);
            Check(L.T("Показывать числа"),settings.Numbers,v=>settings.Numbers=v);
            Check(L.T("Отобразить хилл (лечение героев)"),settings.ShowHealing,v=>settings.ShowHealing=v);
            Check(L.T("Показывать полосы урона"),settings.Bars,v=>settings.Bars=v);
            Check(L.T("Сокращать большие числа (12.5k)"),settings.Compact,v=>settings.Compact=v);
            page=4;Section(L.Choose("ALL PANELS","ВСЕ ПАНЕЛИ"));var hk=new ScrollSafeComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=400,Margin=new Padding(0,10,0,7) };
            for(int i=1;i<=12;i++) hk.Items.Add(L.T("Скрыть / показать: Ctrl + Shift + F")+i);
            hk.SelectedIndex=settings.Hotkey-112; hk.SelectedIndexChanged+=(a,b)=>{int key=112+hk.SelectedIndex;if(Array.IndexOf(settings.PartHotkeys,key)>=0){hk.SelectedIndex=settings.Hotkey-112;return;}settings.Hotkey=key;Save();}; Add(hk);
            Section(L.Choose("INDIVIDUAL PANELS · CTRL + SHIFT","ОТДЕЛЬНЫЕ ПАНЕЛИ · CTRL + SHIFT"));
            for(int i=0;i<6;i++){int part=i;Add(new Label{Text=LayoutHandle.Names[i],AutoSize=true});var bind=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList};bind.Items.Add(L.Choose("Not assigned","Не назначено"));for(int n=1;n<=12;n++)bind.Items.Add("Ctrl + Shift + F"+n);bind.SelectedIndex=settings.PartHotkeys[i]==0?0:settings.PartHotkeys[i]-111;bind.SelectedIndexChanged+=(a,b)=>{int key=bind.SelectedIndex==0?0:bind.SelectedIndex+111;bool duplicate=key!=0&&(key==settings.Hotkey||Array.FindIndex(settings.PartHotkeys,k=>k==key)!=-1&&settings.PartHotkeys[part]!=key);if(duplicate){bind.SelectedIndex=settings.PartHotkeys[part]==0?0:settings.PartHotkeys[part]-111;return;}settings.PartHotkeys[part]=key;Save();};Add(bind);}
            Add(new Label{Text=L.Choose("Already assigned shortcuts cannot be reused. Each shortcut toggles only its own panel.","Занятое сочетание нельзя назначить повторно. Каждый бинд переключает только свой блок."),AutoSize=true});
            page=0;Check(L.T("Запускать с Windows (в трей)"),settings.Autostart,v=>{if(!UiReview.Active)SettingsManager.SetAutostart(v);settings.Autostart=v;});
            page=5;Section("DEADLOCK HELPER BY KVINTIN   v1.0");
            Section(L.Choose("FEEDBACK & IDEAS","ОТЗЫВЫ И ПОЖЕЛАНИЯ"));
            Add(new Label{Text=L.Choose("Feedback is optional. Share an idea, leave a review or report an issue on GitHub. Nothing is sent automatically.","Обратная связь по желанию. Оставьте отзыв, предложите идею или сообщите об ошибке на GitHub. Ничего не отправляется автоматически."),AutoSize=true});
            Button(L.Choose("Leave a review","Оставить отзыв"),()=>Process.Start("https://github.com/Alexkvintin/deadlock-helper/issues/new?template=review.yml"));
            Button(L.Choose("Suggest an idea","Предложить идею"),()=>Process.Start("https://github.com/Alexkvintin/deadlock-helper/issues/new?template=idea.yml"));
            Button(L.Choose("Report an issue","Сообщить об ошибке"),()=>Process.Start("https://github.com/Alexkvintin/deadlock-helper/issues/new?template=bug.yml"));
            Add(new Label{Text=L.Choose("Independent companion. Not affiliated with Valve.\nGame artwork belongs to its respective owners.","Независимое приложение. Не связано с Valve.\nПрава на игровые материалы принадлежат их владельцам."),Width=400,Height=65,ForeColor=Theme.Muted});
            Check(L.T("Диагностика ресурсов (раз в 30 секунд)"),settings.Diagnostics,v=>settings.Diagnostics=v);
            Button(L.T("Открыть настройки и журнал"),()=>Process.Start("explorer.exe",SettingsManager.Folder));
            Button(L.T("Завершить приложение"),()=>Application.Exit());
            var note=new Label { Text=L.T("Закрытие окна сворачивает приложение в трей.\nTab и Alt+Tab не скрывают панели.\nПосле настройки снимите галочку перемещения."),AutoSize=true,ForeColor=Color.FromArgb(146,156,170),Margin=new Padding(0,12,0,12) }; Add(note);
            FormClosing+=(a,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;EndLayoutEdit();Hide();}};
            Resize+=(a,e)=>{if(WindowState==FormWindowState.Minimized)EndLayoutEdit();};
            Theme.Skin(pageHost);Theme.Skin(navigation);SelectPage(0);starting=false;
        }
        void PartCheck(int part){partChecks[part]=Check(L.Choose("Show ","Показывать ")+LayoutHandle.Names[part],settings.ShowHudPart(part),value=>{if(settings.ShowHudPart(part)!=value)settings.TogglePart(part);});}
        public void SyncVisibility(){bool was=starting;starting=true;foreach(var pair in partChecks)pair.Value.Checked=settings.ShowHudPart(pair.Key);starting=was;if(preview!=null)preview.Invalidate();}
        public void SetStatus(string text) { if(status.Text!=text) status.Text=L.ToLanguage(text); }
        void LayoutControls(){
            layoutCheck=Check(L.Choose("Move panels over the game","Перемещать панели поверх игры"),false,v=>settings.EditLayout=v);
            var choose=new ScrollSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=400};choose.Items.AddRange(LayoutHandle.Names);choose.SelectedIndex=0;Add(choose);
            bool updating=false;
            var size=new RangeSlider{Minimum=50,Maximum=200,Width=400};var alpha=new RangeSlider{Minimum=20,Maximum=100,Width=400};var sizeLabel=new Label{AutoSize=true};var alphaLabel=new Label{AutoSize=true};
            Action load=()=>{updating=true;var p=settings.Placement(choose.SelectedIndex);size.Value=(int)(p.Scale*100);alpha.Value=(int)(p.Opacity*100);sizeLabel.Text=L.T("Размер блока: ")+size.Value+"%";alphaLabel.Text=L.T("Непрозрачность блока: ")+alpha.Value+"%";updating=false;};
            choose.SelectedIndexChanged+=(a,b)=>{preview.Selected=choose.SelectedIndex;load();};preview.SelectionChanged+=index=>{choose.SelectedIndex=index;};preview.Edited+=()=>{load();Save();};size.ValueChanged+=(a,b)=>{if(updating)return;settings.Placement(choose.SelectedIndex).Scale=size.Value/100.0;sizeLabel.Text=L.T("Размер блока: ")+size.Value+"%";Save();};alpha.ValueChanged+=(a,b)=>{if(updating)return;settings.Placement(choose.SelectedIndex).Opacity=alpha.Value/100.0;alphaLabel.Text=L.T("Непрозрачность блока: ")+alpha.Value+"%";Save();};
            Add(sizeLabel);Add(size);Add(alphaLabel);Add(alpha);load();
            Button(L.T("Сбросить расположение блоков"),()=>{settings.Layout=null;settings.Validate();load();Save();});
        }
        public void SetDemoCheck(bool value) { if(demoCheck.Checked!=value)demoCheck.Checked=value; }
        public void EndLayoutEdit(){if(layoutCheck!=null&&layoutCheck.Checked)layoutCheck.Checked=false;else if(settings.EditLayout){settings.EditLayout=false;changed();}}
        void Section(string text){Add(new Label{Text=text,ForeColor=Theme.Gold,Font=new Font("Bahnschrift",10,FontStyle.Regular),AutoSize=true,Margin=new Padding(0,22,0,10)});}
        void Save() { if(starting)return; try { if(!UiReview.Active)SettingsManager.Save(settings); changed(); if(preview!=null)preview.Invalidate(); } catch(Exception e) { Log.Write("Settings save",e); MessageBox.Show(this,e.Message,L.T("Не удалось сохранить")); } }
        CheckBox Check(string text,bool value,Action<bool> set) {
            var c=new Toggle { Text=text,Checked=value,Margin=new Padding(0,3,0,3) };
            c.CheckedChanged+=(a,b)=>{try{set(c.Checked);Save();}catch(Exception e){Log.Write("Setting",e);MessageBox.Show(this,e.Message);}}; Add(c); return c;
        }
        void Slider(string title,int min,int max,int value,Action<int> set) {
            var panel=new FlowLayoutPanel { Width=410,Height=47,Margin=new Padding(0,5,0,0) };
            var label=new Label { Text=title+" · "+value+"%",Width=175,Height=26,Padding=new Padding(0,6,0,0) };
            var slider=new TrackBar { Minimum=min,Maximum=max,Value=value,Width=220,TickStyle=TickStyle.None,SmallChange=5,LargeChange=10 };
            slider.ValueChanged+=(a,b)=>{label.Text=title+" · "+slider.Value+"%";set(slider.Value);Save();}; panel.Controls.Add(label);panel.Controls.Add(slider);Add(panel);
        }
        void Position(string title,double x,double y,Action<double,double> set) {
            var panel=new FlowLayoutPanel { Width=410,Height=34,Margin=new Padding(0,3,0,3) };
            panel.Controls.Add(new Label { Text=title,Width=220,Height=28,Padding=new Padding(0,4,0,0) });
            var nx=new NumericUpDown {Minimum=0,Maximum=100,DecimalPlaces=1,Value=(decimal)(x*100),Width=80};
            var ny=new NumericUpDown {Minimum=0,Maximum=100,DecimalPlaces=1,Value=(decimal)(y*100),Width=80};
            EventHandler h=(a,b)=>{set((double)nx.Value/100,(double)ny.Value/100);Save();}; nx.ValueChanged+=h;ny.ValueChanged+=h;
            panel.Controls.Add(nx);panel.Controls.Add(ny);Add(panel);
        }
        void Button(string text,Action action) { var b=new Button { Text=text,Width=400,Height=33,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,5,0,0) };b.Click+=(a,e)=>{try{action();}catch(Exception ex){Log.Write(text,ex);MessageBox.Show(this,ex.Message,text);}};Add(b); }
    }
}










