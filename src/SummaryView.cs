using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace DeadlockOverlay {
    public static class SummaryView {
        public static string[] Labels{get{return new string[]{L.T("Урон героям"),L.T("Строения"),L.T("Поддержка"),L.T("Боевое участие"),L.T("Экономика")};}}
        public static double[] Values(ImpactRow r){return new[]{r.Damage,r.Objectives,r.Support,r.Combat,r.Economy};}
        public static string Strength(ImpactRow r){if(!r.Score.HasValue)return L.T("Недостаточно данных для оценки");var v=Values(r);return string.Join(" · ",Enumerable.Range(0,5).OrderByDescending(i=>v[i]).Take(2).Where(i=>v[i]>0).Select(i=>Labels[i]));}
        static string E(string s){return WebUtility.HtmlEncode(s??"");}
        static string F(double n){return n.ToString("0.0",CultureInfo.InvariantCulture);}
        static string Icon(int hero){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets",hero+".png");return File.Exists(path)?"<img alt='' src='data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(path))+"'>":"<span class='portrait'>?</span>";}
        public static PointF[] Points(double[] values,float cx,float cy,float radius){return values.Select((v,i)=>{double a=-Math.PI/2+i*Math.PI*2/5;return new PointF(cx+(float)(Math.Cos(a)*radius*v),cy+(float)(Math.Sin(a)*radius*v));}).ToArray();}
        static string Polygon(double[] values){return string.Join(" ",Points(values,100,95,68).Select(p=>F(p.X)+","+F(p.Y)));}
        public static string Html(MatchSummary summary){
            var rows=summary.Rank();var best=rows.FirstOrDefault(x=>x.Score.HasValue);
            var b=new StringBuilder(L.T("<!doctype html><html lang='"+L.Language+"'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Deadlock · Разбор матча</title><style>body{margin:0;background:#151817;color:#eee7d2;font:15px system-ui}main{max-width:1180px;margin:auto;padding:40px 24px}h1{font:38px Georgia;margin:14px 0}h2{margin:8px 0}p,small{color:#9ea697}.eyebrow{letter-spacing:3px;color:#e1bf7a;font-size:12px}.hero,.card,.compare{background:#202522;border:1px solid #353e35;border-radius:22px;padding:24px;margin:20px 0}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}.card{margin:0}.identity{display:flex;gap:14px;align-items:center}img,.portrait{width:54px;height:54px;border-radius:14px;background:#353e35;object-fit:cover}.score{margin-left:auto;color:#8bdeab;font-size:28px}.body{display:flex;align-items:center}.body svg{width:43%;min-width:130px}.metrics{flex:1}.metric{font-size:12px;margin:9px 0}.bar{height:5px;background:#394137;border-radius:5px;margin-top:5px;overflow:hidden}.bar i{display:block;height:100%;background:#8bdeab}.stats{padding-top:12px;border-top:1px solid #394137;line-height:1.8;color:#ddd5bf}.strength{color:#e1bf7a;font-size:13px}a{color:#e1bf7a}.teams{display:flex;justify-content:space-between;margin-top:12px}.split{display:flex;height:9px;border-radius:9px;overflow:hidden;background:#394137}.split i{background:#e1bf7a}.split i:last-child{background:#90b6ca}summary{cursor:pointer}@media(max-width:760px){.grid{grid-template-columns:1fr}main{padding:20px 14px}h1{font-size:29px}}</style><main>"));
            b.Append(L.T("<div class='eyebrow'>DEADLOCK / MATCH REVIEW</div><h1>Каждый вклад имеет значение</h1><p>Матч ")+E(summary.MatchId)+" · "+summary.Captured.ToLocalTime().ToString("dd.MM.yyyy HH:mm")+" · "+E(L.ToLanguage(summary.Reason))+L.T("</p><p>Последний полученный снимок трансляции, не обязательно финальный результат.")+(summary.Fresh?"":L.T(" Данные устарели или неполны."))+"</p>");
            if(best!=null)b.Append(L.T("<section class='hero'><div class='eyebrow'>ЛИДЕР ПО ИНДЕКСУ ВКЛАДА</div><div class='identity'>")+Icon(best.Player.Hero)+"<div><h1>"+E(best.Player.Name)+"</h1><small>"+E(best.Player.HeroName)+" · "+Patrons.Name(best.Player.Team)+"</small></div><strong class='score'>"+F(best.Score.Value)+L.T(" / 100</strong></div><p class='strength'>Сильные стороны: ")+E(Strength(best))+"</p></section>");
            b.Append(L.T("<section class='compare'><h2>Команда против команды</h2>"));
            Func<Player,long?>[] getters={p=>p.Damage,p=>p.ObjectDamage,p=>p.Healing};
            for(int i=0;i<3;i++){var metric=getters[i];bool complete=summary.Players.Length==12&&summary.Players.All(p=>metric(p).HasValue);long a=summary.Players.Where(p=>p.Team==2).Sum(p=>metric(p)??0),z=summary.Players.Where(p=>p.Team==3).Sum(p=>metric(p)??0);double portion=complete&&a+z>0?100.0*a/(a+z):50;b.Append("<div class='teams'><span>Hidden King · "+(complete?MatchSummary.N(a):"—")+"</span><small>"+Labels[i]+"</small><span>"+(complete?MatchSummary.N(z):"—")+" · Archmother</span></div><div class='split'><i style='width:"+F(portion)+"%'></i><i style='width:"+F(100-portion)+"%'></i></div>");}
            b.Append("</section><div class='grid'>");
            foreach(var r in rows){var p=r.Player;var values=Values(r);b.Append("<article class='card'><div class='identity'>"+Icon(p.Hero)+"<div><h2>"+E(p.Name)+"</h2><small>"+E(p.HeroName)+" · "+Patrons.Name(p.Team)+"</small></div><strong class='score'>"+(r.Score.HasValue?F(r.Score.Value)+" "+r.Grade:"—")+L.T("</strong></div><div class='body'><svg viewBox='0 0 200 190' aria-label='Пять показателей вклада'>"));
                for(int ring=1;ring<=4;ring++)b.Append("<polygon points='"+Polygon(Enumerable.Repeat(ring/4.0,5).ToArray())+"' fill='none' stroke='#465043'/>");
                if(r.Score.HasValue)b.Append("<polygon points='"+Polygon(values)+"' fill='#8bdeab' fill-opacity='.25' stroke='#8bdeab' stroke-width='2'/>");
                b.Append("</svg><div class='metrics'>");for(int i=0;i<5;i++)b.Append("<div class='metric'>"+Labels[i]+" · "+(r.Score.HasValue?F(values[i]*100)+"%":"—")+"<div class='bar'><i style='width:"+F(values[i]*100)+"%'></i></div></div>");
                b.Append(L.T("</div></div><div class='strength'>Сильные стороны: ")+E(Strength(r))+"</div><div class='stats'>K / D / A: "+MatchSummary.N(p.Kills)+" / "+MatchSummary.N(p.Deaths)+" / "+MatchSummary.N(p.Assists)+L.T("<br>Героям: ")+MatchSummary.N(p.Damage)+L.T(" · Строениям: ")+MatchSummary.N(p.ObjectDamage)+L.T("<br>Лечение: ")+MatchSummary.N(p.Healing)+L.T(" · Души: ")+MatchSummary.N(p.Souls)+"</div></article>");
            }
            b.Append(L.T("</div><details class='compare'><summary>Методика и ограничения оценки</summary><p>")+ImpactProcessor.Explanation+L.T("</p><p>Диаграммы: доля от максимума в матче. Сильные стороны: два наиболее высоких нормированных показателя. Лечение — счётчик источника; самохил отдельно не вычитается.</p></details></main></html>"));return b.ToString();
        }
    }
    public sealed class SummaryWindow : Form {
        protected override bool ShowWithoutActivation{get{return true;}}
        public SummaryWindow(MatchSummary summary){
            Icon=Design.AppIcon;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Shown+=(a,e)=>Design.Fit(this);
            UpdateSummary(summary);
        }
        public void UpdateSummary(MatchSummary summary){
            SuspendLayout();while(Controls.Count>0){var old=Controls[0];Controls.Remove(old);old.Dispose();}
            Text=L.T("Deadlock · Разбор матча ")+summary.MatchId;TopMost=true;if(!Visible)Size=new Size(1200,930);MinimumSize=new Size(800,560);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Background;
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scroll);
            var canvas=new ReviewDashboard(summary){Location=new Point(0,0)};scroll.Controls.Add(canvas);scroll.Resize+=(s,e)=>{canvas.Width=Math.Max(760,scroll.ClientSize.Width-22);};canvas.Width=1160;
            canvas.PlayerSelected+=p=>new PlayerDetailWindow(summary,p).Show(this);
            var tabs=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,Padding=new Padding(18,5,0,0),BackColor=Theme.Card};
            for(int i=0;i<ReviewDashboard.Tabs.Length;i++){int metric=i;var tab=new Button{Text=ReviewDashboard.Tabs[i],Width=145,Height=36};tab.Click+=(a,e)=>{canvas.Metric=metric;foreach(Control c in tabs.Controls)c.BackColor=Theme.Card;tab.BackColor=Color.FromArgb(28,66,60);};tabs.Controls.Add(tab);}Theme.Skin(tabs);Controls.Add(tabs);
            var footer=new Button{Text=L.T("Открыть полный отчёт · сравнение команд и методика"),Dock=DockStyle.Bottom,Height=48};footer.Click+=(s,e)=>{if(string.IsNullOrEmpty(summary.SavedPath))summary.Save();System.Diagnostics.Process.Start(summary.SavedPath);};Controls.Add(footer);Theme.Skin(footer);
            ResumeLayout();Invalidate(true);
        }
    }
    sealed class SummaryCanvas : Control {
        readonly MatchSummary summary;readonly ImpactRow[] rows;readonly HeroImages images=new HeroImages();
        readonly Font title=new Font("Georgia",24,FontStyle.Bold),name=new Font("Segoe UI",13,FontStyle.Bold),small=new Font("Segoe UI",9),tiny=new Font("Segoe UI",8);
        public SummaryCanvas(MatchSummary s){summary=s;rows=s.Rank();SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Background;Resize+=(a,e)=>{Height=205+((rows.Length+1)/2)*310;Invalidate();};}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(var ink=new SolidBrush(Theme.Ink))using(var muted=new SolidBrush(Theme.Muted))using(var gold=new SolidBrush(Theme.Gold))using(var green=new SolidBrush(Theme.Green))using(var line=new Pen(Color.FromArgb(69,79,64))){
                g.DrawString("DEADLOCK / MATCH REVIEW",small,gold,24,22);g.DrawString(L.T("Разбор матча ")+summary.MatchId,title,ink,24,48);
                var best=rows.FirstOrDefault(r=>r.Score.HasValue);g.DrawString(best==null?L.T("Недостаточно данных для общего рейтинга"):L.T("Лидер вклада: ")+best.Player.Name+" · "+best.Player.HeroName+" · "+best.Score.Value.ToString("0.0"),name,green,24,96);
                g.DrawString(L.ToLanguage(summary.Reason)+" · "+summary.Captured.ToLocalTime().ToString("HH:mm:ss")+" · "+(summary.Fresh?L.T("данные свежие"):L.T("данные устарели / неполны")),small,muted,24,131);
                g.DrawString(L.T("Сильные стороны и диаграммы сравнивают игроков внутри матча. Методика — в полном отчёте."),small,muted,24,153);
                float width=(Width-60)/2f;
                for(int i=0;i<rows.Length;i++){
                    float x=20+(i%2)*(width+20),y=188+(i/2)*310;var rect=new RectangleF(x,y,width,292);if(!e.ClipRectangle.IntersectsWith(Rectangle.Ceiling(rect)))continue;
                    var r=rows[i];var p=r.Player;Theme.Fill(g,Theme.Card,rect,18);var icon=images.Get(p.Hero);if(icon!=null)g.DrawImage(icon,x+16,y+16,46,46);
                    using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}){
                        g.DrawString(p.Name,name,ink,new RectangleF(x+74,y+16,width-174,25),format);g.DrawString(p.HeroName+" · "+Patrons.Name(p.Team),tiny,muted,new RectangleF(x+74,y+44,width-90,18),format);
                        g.DrawString(r.Score.HasValue?r.Score.Value.ToString("0.0")+" "+r.Grade:"—",name,green,x+width-92,y+18);
                        var values=SummaryView.Values(r);float cx=x+86,cy=y+139;
                        for(int ring=1;ring<=4;ring++)g.DrawPolygon(line,SummaryView.Points(Enumerable.Repeat(ring/4.0,5).ToArray(),cx,cy,59));
                        if(r.Score.HasValue){var points=SummaryView.Points(values,cx,cy,59);using(var fill=new SolidBrush(Color.FromArgb(65,139,222,171)))g.FillPolygon(fill,points);using(var outline=new Pen(Theme.Green,2))g.DrawPolygon(outline,points);}
                        for(int m=0;m<5;m++){float mx=x+167,my=y+80+m*23;g.DrawString(SummaryView.Labels[m],tiny,muted,mx,my);Theme.Fill(g,Color.FromArgb(57,65,55),new RectangleF(mx,my+15,Math.Max(20,width-190),3),1);if(values[m]>0)Theme.Fill(g,Theme.Green,new RectangleF(mx,my+15,(float)((width-190)*values[m]),3),1);}
                        g.DrawString(L.T("Сильные стороны: ")+SummaryView.Strength(r),tiny,gold,new RectangleF(x+16,y+203,width-32,20),format);
                        g.DrawString("K / D / A   "+MatchSummary.N(p.Kills)+" / "+MatchSummary.N(p.Deaths)+" / "+MatchSummary.N(p.Assists)+L.T("      Души   ")+MatchSummary.N(p.Souls),small,ink,x+16,y+228);
                        g.DrawString(L.T("Урон   ")+MatchSummary.N(p.Damage)+L.T("    Строения   ")+MatchSummary.N(p.ObjectDamage)+L.T("    Хилл   ")+MatchSummary.N(p.Healing),tiny,muted,x+16,y+254);
                    }
                }
            }
        }
        protected override void Dispose(bool disposing){if(disposing){images.Dispose();title.Dispose();name.Dispose();small.Dispose();tiny.Dispose();}base.Dispose(disposing);}
    }
}



