using Microsoft.Playwright;
using System.Text.Json;
using NAudio.Wave;
namespace AssetRaider.Desktop;

internal static class AudioCheck
{
    internal static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        try {
            var path=Path.Combine(directory,"audio-settings-test.json");
            new AudioPreferences {Gains=[-12,-6,-3,0,3,6,9,12],Enabled=false,GainDb=-6,Style="Orbit",FrameRate=120}.Save(path);
            var saved=AudioPreferences.Load(path);
            if(saved.Gains[7]!=12||saved.Gains[0]!=-12||saved.Enabled||saved.FrameRate!=120||saved.Style!="Orbit"||saved.GainDb!=-6)throw new Exception("Audio settings reload failed.");
            File.WriteAllText(path,"{\"Gains\":[1],\"FrameRate\":0,\"Style\":\"invalid\"}");
            var repaired=AudioPreferences.Load(path);if(repaired.FrameRate!=60||repaired.Gains.Length!=8||repaired.Style!="Spectrum")throw new Exception("Invalid preferences not repaired.");
            using var memory=new MemoryStream();
            using(var wav=new WaveFileWriter(new NonClosingStream(memory),new WaveFormat(48000,16,1)))for(var i=0;i<48000*5;i++)wav.WriteSample((float)(.15*Math.Sin(2*Math.PI*1000*i/48000)));
            var bytes=memory.ToArray();
            using var playwright=await Playwright.CreateAsync();
            await using var browser=await playwright.Chromium.LaunchAsync(new() {Channel="chrome",Headless=true,Args=["--mute-audio","--autoplay-policy=no-user-gesture-required"]});
            var page=await browser.NewPageAsync();
            await page.RouteAsync("https://audio.assetraider.test/**",async route=>{
                if(route.Request.Url.EndsWith(".wav"))await route.FulfillAsync(new(){ContentType="audio/wav",BodyBytes=bytes});
                else await route.FulfillAsync(new(){ContentType="text/html",Body="<script>window.song=new Audio('/tone.wav');song.loop=true;window.__assetRaiderDesktop={audio:()=>song,start:()=>song.play(),stop:()=>song.pause()};</script>"});
            });
            await page.GotoAsync("https://audio.assetraider.test/");await page.WaitForFunctionAsync("() => song.readyState>=2");
            async Task Apply(double gain,bool enabled=true,double gainDb=0)=>await page.EvaluateAsync(MusicBrowser.Script("equalizer"),new {gains=new double[]{0,0,0,0,gain,0,0,0},enabled,gainDb});
            await Apply(0);await page.EvaluateAsync("() => { const o=window.__assetRaiderDesktop;window.meter=o.eq.context.createAnalyser();meter.fftSize=2048;o.eq.filters.at(-1).connect(meter);return o.start(); }");
            async Task<double> Rms(){await Task.Delay(300);return await page.EvaluateAsync<double>("() => {const v=new Float32Array(meter.fftSize);meter.getFloatTimeDomainData(v);return Math.sqrt(v.reduce((s,x)=>s+x*x,0)/v.length);}");}
            var flat=await Rms();await Apply(-12);var cut=await Rms();await Apply(-12,false);var bypass=await Rms();await Apply(12);var boosted=await Rms();await Apply(0,true,-6);var gainDown=await Rms();await Apply(0,true,6);var gainUp=await Rms();await Apply(0,false,6);var gainBypass=await Rms();
            if(flat<.01||cut/flat is <.20 or >.31||bypass/flat is <.93 or >1.07||boosted/flat is <3.8 or >4.2 || gainDown/flat is <.47 or >.53 || gainUp/flat is <1.9 or >2.1 || gainBypass/flat is <.93 or >1.07)throw new Exception($"Real PCM EQ response failed: flat={flat}, cut={cut}, bypass={bypass}, boost={boosted}, gainDown={gainDown}, gainUp={gainUp}");
            var running=await page.EvaluateAsync<string>("() => window.__assetRaiderDesktop.eq.context.state");
            await page.EvaluateAsync("() => window.__assetRaiderDesktop.stop()");
            await page.WaitForFunctionAsync("() => window.__assetRaiderDesktop.eq.context.state==='closed' && song.paused");
            File.WriteAllText(Path.Combine(directory,"audio-check.json"),JsonSerializer.Serialize(new{Passed=true,flat,cut,bypass,boosted,gainDown,gainUp,gainBypass,running,Checks="Real Chrome media-element PCM: 1 kHz -12 dB cut, bypass, +12 dB band boost, +/-6 dB manual preamp, context cleanup, settings round-trip and invalid settings recovery"},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(directory,"audio-check.json"),ex.ToString());return 1;}
    }
    private sealed class NonClosingStream(Stream inner):Stream
    {
        public override bool CanRead=>inner.CanRead;public override bool CanSeek=>inner.CanSeek;public override bool CanWrite=>inner.CanWrite;public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>inner.Position=value;}
        public override void Flush()=>inner.Flush();public override int Read(byte[] b,int o,int n)=>inner.Read(b,o,n);public override long Seek(long o,SeekOrigin s)=>inner.Seek(o,s);public override void SetLength(long n)=>inner.SetLength(n);public override void Write(byte[] b,int o,int n)=>inner.Write(b,o,n);
    }
}
