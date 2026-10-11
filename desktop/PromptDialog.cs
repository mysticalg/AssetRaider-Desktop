namespace AssetRaider.Desktop;

internal sealed class PromptDialog : Form
{
    public PromptDialog(Track track)
    {
        Text="Full prompt · "+track.Title; Width=760; Height=560; MinimumSize=new(440,300); StartPosition=FormStartPosition.CenterParent;
        var text=new TextBox {Text=track.Prompt.ReplaceLineEndings(Environment.NewLine),ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),AccessibleName="Full prompt"};
        var copy=new Button {Text="Copy full prompt",Dock=DockStyle.Bottom,Height=42,Enabled=track.Prompt.Length>0};
        copy.Click+=(_,_)=>{try{Clipboard.SetText(track.Prompt);copy.Text="Copied";}catch(Exception ex){MessageBox.Show(this,ex.Message,"Clipboard");}};
        Controls.Add(text); Controls.Add(copy);
    }
}
