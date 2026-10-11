namespace AssetRaider.Desktop;

internal static class ArtworkPaint
{
    public static Rectangle Fit(Size source, Rectangle box)
    {
        var scale=Math.Min((double)box.Width/Math.Max(1,source.Width),(double)box.Height/Math.Max(1,source.Height));
        var width=Math.Max(1,(int)(source.Width*scale)); var height=Math.Max(1,(int)(source.Height*scale));
        return new(box.X+(box.Width-width)/2,box.Y+(box.Height-height)/2,width,height);
    }
    public static void Draw(Graphics graphics, Image? image, Rectangle box, Font font)
    {
        using var background=new SolidBrush(Color.FromArgb(224,232,217)); graphics.FillRectangle(background,box);
        if(image!=null) graphics.DrawImage(image,Fit(image.Size,box));
        else TextRenderer.DrawText(graphics,"♪",font,box,Color.FromArgb(108,128,86),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}

// Paint only visible cards: a large scanned library does not create thousands of image controls.
internal sealed class TrackGridView(Func<string,Image?> image) : Control
{
    private readonly VScrollBar scroll=new() {Dock=DockStyle.Right,SmallChange=50};
    private IReadOnlyList<Track> tracks=[];
    public int ArtworkSize=144;
    public Action<Track>? Play, Prompt, FocusTrack;
    public Action? SelectionChanged;
    public Func<bool> CanChangeSelection=()=>true;
    private int Columns=>Math.Max(1,(ClientSize.Width-scroll.Width)/Math.Max(180,ArtworkSize+80));
    private int CardWidth=>Math.Max(1,(ClientSize.Width-scroll.Width)/Columns);
    private int CardHeight=>ArtworkSize+166;
    public void SetTracks(IReadOnlyList<Track> value) {tracks=value; scroll.Value=0; UpdateScroll(); Invalidate();}
    protected override void OnCreateControl() {base.OnCreateControl(); DoubleBuffered=true; BackColor=Color.White; AccessibleName="Artwork grid"; Controls.Add(scroll); scroll.ValueChanged+=(_,_)=>Invalidate();}
    protected override void OnResize(EventArgs e) {base.OnResize(e); UpdateScroll(); Invalidate();}
    private void UpdateScroll() {
        scroll.LargeChange=Math.Max(1,ClientSize.Height); scroll.Maximum=Math.Max(0,((tracks.Count+Columns-1)/Columns)*CardHeight-1);
        scroll.Value=Math.Min(scroll.Value,Math.Max(0,scroll.Maximum-scroll.LargeChange+1)); scroll.Enabled=scroll.Maximum>=ClientSize.Height;
    }
    protected override void OnMouseWheel(MouseEventArgs e) {base.OnMouseWheel(e); scroll.Value=Math.Clamp(scroll.Value-e.Delta/3,0,Math.Max(0,scroll.Maximum-scroll.LargeChange+1));}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var first=scroll.Value/CardHeight;
        for(var row=first;row<= (scroll.Value+ClientSize.Height)/CardHeight;row++) for(var column=0;column<Columns;column++) {
            var index=row*Columns+column; if(index>=tracks.Count) break;
            var track=tracks[index]; var x=column*CardWidth+6; var y=row*CardHeight-scroll.Value+6; var width=CardWidth-12;
            using var background=new SolidBrush(track.Selected?Color.FromArgb(220,240,194):Color.FromArgb(249,251,246)); e.Graphics.FillRectangle(background,x,y,width,CardHeight-12);
            using var border=new Pen(Color.FromArgb(200,213,190)); e.Graphics.DrawRectangle(border,x,y,width,CardHeight-12);
            var cover=new Rectangle(x+8,y+8,width-16,ArtworkSize); ArtworkPaint.Draw(e.Graphics,image(track.ArtworkUrl),cover,Font);
            void Text(string text,int top,int height=23) => TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(x+8,y+top,width-16,height),ForeColor,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
            Text((track.Selected?"☑ ":"☐ ")+track.Title,ArtworkSize+18);
            Text(track.Duration+"  ·  "+track.SunoVersion,ArtworkSize+41);
            Text("Created: "+track.CreatedDate,ArtworkSize+64);
            Text("Released: "+track.ReleaseDate,ArtworkSize+87);
            Text(track.Status+"  ·  "+track.SourceQualityText,ArtworkSize+110);
            var buttonY=y+ArtworkSize+134; var buttonWidth=(width-24)/2;
            e.Graphics.DrawRectangle(border,x+8,buttonY,buttonWidth,24); e.Graphics.DrawRectangle(border,x+16+buttonWidth,buttonY,buttonWidth,24);
            TextRenderer.DrawText(e.Graphics,track.PlaybackAction,Font,new Rectangle(x+8,buttonY,buttonWidth,24),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics,"Full prompt",Font,new Rectangle(x+16+buttonWidth,buttonY,buttonWidth,24),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if(e.X>=ClientSize.Width-scroll.Width) return;
        var column=e.X/CardWidth; var row=(e.Y+scroll.Value)/CardHeight; var index=row*Columns+column; if(index<0||index>=tracks.Count) return;
        var track=tracks[index]; FocusTrack?.Invoke(track); var insideY=(e.Y+scroll.Value)%CardHeight-6;
        if(insideY>=ArtworkSize+134 && insideY<ArtworkSize+158) {if(e.X%CardWidth<CardWidth/2) Play?.Invoke(track); else Prompt?.Invoke(track);}
        else if(CanChangeSelection()&&insideY>=ArtworkSize+18&&insideY<ArtworkSize+41) {track.Selected=!track.Selected; SelectionChanged?.Invoke(); Invalidate();}
    }
}
