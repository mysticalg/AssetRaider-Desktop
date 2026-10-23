using System.Drawing.Drawing2D;
using System.Diagnostics;
namespace AssetRaider.Desktop;

internal sealed class SpectrumView : Control
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 1 };
    [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
    [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double nextFrame;
    private readonly AudioPreferences settings;
    private readonly ContextMenuStrip menu = new();
    public SpectrumView(AudioPreferences preferences, Action changed)
    {
        timeBeginPeriod(1); settings=preferences; DoubleBuffered=true; BackColor=Color.FromArgb(24,31,25); ForeColor=Color.FromArgb(188,212,159); AccessibleName="Live audio visualizer; right-click for styles and frame rate";
        var styles=new ToolStripMenuItem("Visualization style");
        foreach(var name in AudioPreferences.Styles) {var item=new ToolStripMenuItem(name);item.Click+=(_,_)=>{settings.Style=name;changed();Invalidate();};styles.DropDownItems.Add(item);}
        var rates=new ToolStripMenuItem("Frame rate");
        foreach(var rate in AudioPreferences.Rates) {var item=new ToolStripMenuItem($"{rate} FPS") {Tag=rate};item.Click+=(_,_)=>{settings.FrameRate=rate;nextFrame=0;changed();};rates.DropDownItems.Add(item);}
        var cycle=new ToolStripMenuItem("Next visualization");cycle.Click+=(_,_)=>{settings.Style=AudioPreferences.Styles[(Array.IndexOf(AudioPreferences.Styles,settings.Style)+1)%AudioPreferences.Styles.Length];changed();Invalidate();};
        menu.Items.AddRange([styles,rates,cycle]);menu.Opening+=(_,_)=>{foreach(ToolStripMenuItem item in styles.DropDownItems)item.Checked=item.Text==settings.Style;foreach(ToolStripMenuItem item in rates.DropDownItems)item.Checked=(int)item.Tag! == settings.FrameRate;};ContextMenuStrip=menu;
        animation.Tick+=(_,_)=>{var now=clock.Elapsed.TotalMilliseconds;if(!Visible||now<nextFrame)return;nextFrame=Math.Max(nextFrame+1000d/settings.FrameRate,now);Invalidate();};animation.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if(Width<30||Height<45)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var levels=AudioSpectrum.Shared.Read();float left=10,top=28,w=Width-20,h=Height-50,bottom=top+h;
        using var grid=new Pen(Color.FromArgb(40,57,45));for(var y=top;y<bottom;y+=24)g.DrawLine(grid,left,y,left+w,y);
        using var green=new Pen(Color.FromArgb(160,242,100),2);using var cyan=new Pen(Color.FromArgb(91,225,235),2);
        using var fill=new LinearGradientBrush(new PointF(0,top),new PointF(0,bottom+1),Color.FromArgb(182,242,95),Color.FromArgb(26,130,115));
        var step=w/levels.Length;
        if(settings.Style=="Orbit") {
            var center=new PointF(Width/2f,top+h/2);var radius=Math.Min(w,h)*.23f;
            for(var i=0;i<levels.Length;i++){var angle=i*Math.PI*2/levels.Length-Math.PI/2;var outer=radius+(float)levels[i]*Math.Min(w,h)*.25f;g.DrawLine(i%2==0?green:cyan,center.X+(float)Math.Cos(angle)*radius,center.Y+(float)Math.Sin(angle)*radius,center.X+(float)Math.Cos(angle)*outer,center.Y+(float)Math.Sin(angle)*outer);}
        } else if(settings.Style=="Neon wave") {
            var points=levels.Select((v,i)=>new PointF(left+i*w/(levels.Length-1),bottom-(float)v*h)).ToArray();
            using var glow=new Pen(Color.FromArgb(50,91,225,235),7);g.DrawCurve(glow,points);g.DrawCurve(cyan,points);
        } else {
            for(var i=0;i<levels.Length;i++){
                var amount=(float)levels[i]*h;var x=left+i*step;var barWidth=Math.Max(1,step-3);
                if(settings.Style=="Mirror") {g.FillRectangle(fill,x,top+(h-amount)/2,barWidth,Math.Max(1,amount));}
                else if(settings.Style=="Fire") {using var fire=new LinearGradientBrush(new PointF(x,top),new PointF(x,bottom+1),Color.FromArgb(255,244,120),Color.FromArgb(226,61,44));g.FillRectangle(fire,x,bottom-amount,barWidth,Math.Max(1,amount));}
                else {g.FillRectangle(fill,x,bottom-amount,barWidth,Math.Max(1,amount));if(amount>2)g.DrawLine(cyan,x,bottom-amount,x+barWidth,bottom-amount);}
            }
        }
        using var font=new Font("Segoe UI",8);using var text=new SolidBrush(ForeColor);
        g.DrawString($"{settings.Style.ToUpperInvariant()}  ·  {settings.FrameRate} FPS",font,text,10,6);
        g.DrawString("Right-click: styles / frame rate",font,text,10,Height-18);
    }
    protected override void Dispose(bool disposing){if(disposing){animation.Dispose();menu.Dispose();timeEndPeriod(1);}base.Dispose(disposing);}
}
