using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace DeadlockOverlay {
 // Explicit offline review mode in the production EXE. No OCR, API calls,
 // registry changes or production settings writes are started in this mode.
 public static class UiReview {
  public static bool Active;
  static Screen reviewScreen;
  static void Open(Form window){if(reviewScreen!=null){window.StartPosition=FormStartPosition.Manual;var area=reviewScreen.WorkingArea;window.Size=new Size(Math.Min(window.Width,area.Width),Math.Min(window.Height,area.Height));window.Location=new Point(area.Left+(area.Width-window.Width)/2,area.Top+(area.Height-window.Height)/2);}window.Show();}

  public static void Run(string[] args){Active=true;if(args.Contains("--secondary"))reviewScreen=Screen.AllScreens.FirstOrDefault(s=>!s.Primary);string language=args.Contains("--ru")?"ru":"en";L.Language=language;
   var settings=new Settings{Language=language,ShowHealing=true};settings.Validate();var state=new MatchState();int at=Array.IndexOf(args,"--snapshot");state.Apply(at>=0&&at+1<args.Length?File.ReadAllText(args[at+1]):DemoConnector.Create("offline-review",25),DateTime.UtcNow);state.Spectator=true;
   var form=new Form{Text="Deadlock Helper · VISUAL REVIEW",Size=new Size(550,230),BackColor=Theme.Background,ForeColor=Theme.Ink,StartPosition=FormStartPosition.CenterScreen};
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16)};form.Controls.Add(buttons);
   Action<string,Action> add=(label,action)=>{var button=new Button{Text=label,Width=230,Height=48};button.Click+=(a,e)=>action();buttons.Controls.Add(button);};
   add("Settings",()=>Open(new SettingsWindow(settings,()=>{},v=>{})));
   var summary=MatchSummary.Capture(state,L.Choose("OFFLINE REVIEW · captured data, not a final result","ПРОВЕРКА UI · сохранённые данные, не итог матча"));
   add("Match report",()=>Open(new SummaryWindow(summary)));add("Player detail",()=>Open(new PlayerDetailWindow(summary,summary.Players.First())));
   add("Overlay / light + dark",()=>{var background=new Form{Text="Overlay transparency review",Size=new Size(1150,800),StartPosition=FormStartPosition.CenterScreen,BackColor=Color.White};background.Paint+=(a,e)=>{using(var dark=new SolidBrush(Color.FromArgb(20,30,38)))e.Graphics.FillRectangle(dark,background.ClientSize.Width/2,0,background.ClientSize.Width/2,background.ClientSize.Height);};var images=new HeroImages();var left=new OverlayRenderer(true,images);var right=new OverlayRenderer(false,images);var config=new Settings{ShowHealing=true};config.Validate();config.Placement(0).Opacity=.25;config.Placement(1).Opacity=.25;Action refresh=()=>{state.Received=DateTime.UtcNow;foreach(var p in state.Players.Values)p.Updated=DateTime.UtcNow;var area=background.RectangleToScreen(background.ClientRectangle);left.Refresh(state,config,area,true);right.Refresh(state,config,area,true);};background.Shown+=(a,e)=>refresh();background.Move+=(a,e)=>{if(background.Visible)refresh();};background.FormClosed+=(a,e)=>{left.Dispose();right.Dispose();images.Dispose();};Open(background);});
   Theme.Skin(buttons);if(reviewScreen!=null){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(reviewScreen.WorkingArea.Left+50,reviewScreen.WorkingArea.Top+50);}Application.Run(form);
  }
 }
}
