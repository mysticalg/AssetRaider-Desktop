namespace AssetRaider.Desktop;

internal sealed class EqualizerView : TableLayoutPanel
{
    public EqualizerView(AudioPreferences settings, Action changed)
    {
        Dock = DockStyle.Fill; Margin = new Padding(4, 0, 0, 0); BackColor = Color.FromArgb(24,31,25); ForeColor = Color.FromArgb(218,248,162);
        ColumnCount = 9; RowCount = 4; Padding = new Padding(6);
        for (var i = 0; i < 9; i++) ColumnStyles.Add(new(SizeType.Percent, 100f/9));
        RowStyles.Add(new(SizeType.Absolute,28)); RowStyles.Add(new(SizeType.Absolute,20)); RowStyles.Add(new(SizeType.Percent,100)); RowStyles.Add(new(SizeType.Absolute,20));
        var heading = new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false, Margin=Padding.Empty };
        var enabled = new CheckBox {Text="8-BAND EQ",Checked=settings.Enabled,AutoSize=true,Font=new Font("Segoe UI",8),Margin=Padding.Empty};
        var reset = new Button {Text="Flat",AutoSize=true,Height=24,Font=new Font("Segoe UI",8),Margin=new Padding(8,0,0,0)};
        heading.Controls.AddRange([enabled,reset]); Controls.Add(heading,0,0); SetColumnSpan(heading,9);
        var sliders = new TrackBar[9]; var labels = new Label[9];
        for(var i=0;i<9;i++) {
            var band=i;
            var value = i==0 ? settings.GainDb : settings.Gains[i-1];
            labels[i]=new Label {Text=$"{value:+0;-0;0} dB",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",8),Margin=Padding.Empty};
            var slider = sliders[i] = new TrackBar {Orientation=Orientation.Vertical,Minimum=-12,Maximum=12,Value=(int)value,TickFrequency=6,Dock=DockStyle.Fill,AutoSize=false,Margin=Padding.Empty,AccessibleName=i==0?"Equalizer preamp gain":$"Equalizer {AudioPreferences.Frequencies[i-1]} Hz gain",SmallChange=1,LargeChange=3};
            slider.ValueChanged+=(_,_)=>{if(band==0) settings.GainDb=slider.Value; else settings.Gains[band-1]=slider.Value;labels[band].Text=$"{slider.Value:+0;-0;0} dB";changed();};
            Controls.Add(labels[i],i,1); Controls.Add(slider,i,2);
            var f=i==0?0:AudioPreferences.Frequencies[i-1];
            Controls.Add(new Label {Text=i==0?"Gain":f>=1000?$"{f/1000}k":f.ToString(),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",8),Margin=Padding.Empty},i,3);
        }
        enabled.CheckedChanged+=(_,_)=>{settings.Enabled=enabled.Checked;foreach(var s in sliders)s.Enabled=enabled.Checked;changed();};
        foreach(var s in sliders)s.Enabled=settings.Enabled;
        reset.Click+=(_,_)=>{foreach(var s in sliders)s.Value=0;};
        var tip=new ToolTip(); tip.SetToolTip(enabled,"Playback equalizer: ±12 dB. WAV recordings retain original audio. Gain controls the preamp. Positive gain or band boosts can clip; lower Gain if distorted."); Disposed+=(_,_)=>tip.Dispose();
    }
}
