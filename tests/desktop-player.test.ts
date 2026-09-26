import { readFileSync } from "node:fs";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const source = (name: string) => readFileSync(`desktop/Scripts/${name}.js`, "utf8");
const prepare = (options?: { site: string; id?: string }) => (0, eval)(`(${source("player")})`)(options) as Promise<number>;
const api = () => (window as any).__assetRaiderDesktop;

describe("desktop Udio playback controller", () => {
  let audio: HTMLAudioElement;
  let play: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    vi.useFakeTimers();
    document.body.innerHTML = '<main><button aria-label="Play">Play this song</button></main>';
    audio = document.createElement("audio");
    Object.defineProperties(audio, { duration: { value: 120 }, readyState: { value: 4 }, currentSrc: { value: "blob:song", configurable: true } });
    audio.currentTime = 17; audio.loop = true; audio.volume = .4;
    play = vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
    vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => undefined);
    const button = document.querySelector("button")!;
    vi.spyOn(button, "getClientRects").mockReturnValue({ length: 1 } as DOMRectList);
    button.onclick = () => { void audio.play(); };
  });
  afterEach(() => { api()?.stop(); delete (window as any).__assetRaiderDesktop; vi.clearAllTimers(); vi.useRealTimers(); vi.restoreAllMocks(); });

  it("rewinds a detached player and only resumes when Windows capture is ready", async () => {
    expect(await prepare()).toBe(120);
    expect(audio.currentTime).toBe(0);
    expect(audio.loop).toBe(false);
    expect(audio.volume).toBe(1);
    expect(audio.muted).toBe(false);
    await api().start();
    expect(play).toHaveBeenCalledTimes(2);
    api().stop();
    expect(audio.volume).toBe(.4);
    expect(audio.loop).toBe(true);
    expect(HTMLMediaElement.prototype.play).toBe(play);
  });
  it("blocks autoplay from adding a second song after completion", async () => {
    await prepare(); await api().start();
    audio.dispatchEvent(new Event("ended"));
    const next = document.createElement("audio");
    await next.play();
    expect(api().state().Done).toBe(true);
    expect(play).toHaveBeenCalledTimes(2);
  });
  it("reports a changed stream instead of completing the wrong recording", async () => {
    await prepare(); await api().start();
    Object.defineProperty(audio, "currentSrc", { value: "blob:other" });
    expect(api().state().Error).toContain("changed");
  });
  it("reports paused or stalled playback and restores hooks on failure", async () => {
    await prepare(); await api().start();
    await vi.advanceTimersByTimeAsync(31000);
    expect(api().state().Error).toContain("stalled");
    api().stop();
    document.querySelector("button")!.onclick = null;
    const pending = expect(prepare()).rejects.toThrow("20 seconds");
    await vi.advanceTimersByTimeAsync(21000);
    await pending;
    expect(HTMLMediaElement.prototype.play).toBe(play);
  });
  it("does not use unrelated page or player toolbar buttons", async () => {
    document.body.innerHTML = '<button aria-label="Play">Wrong player</button><main></main>';
    await expect(prepare()).rejects.toThrow("not found");
    expect(play).not.toHaveBeenCalled();
  });

  it("uses Suno's song button outside main and ignores its silent keep-alive audio", async () => {
    const button = document.querySelector("button")!;
    document.body.append(button);
    const silence = document.createElement("audio");
    Object.defineProperties(silence, { currentSrc: { value: "https://cdn-o.suno.com/sil-100.mp3" }, duration: { value: .096 }, readyState: { value: 4 } });
    button.onclick = () => { void silence.play(); void audio.play(); };
    expect(await prepare({ site: "suno" })).toBe(120);
    await api().start();
    expect(audio.currentTime).toBe(0);
    expect(api().state().Duration).toBe(120);
    expect(play).toHaveBeenCalledTimes(3);
  });

  it("rejects a different song route and ambiguous Suno Play buttons", async () => {
    await expect(prepare({ site: "suno", id: "11111111-1111-4111-8111-111111111111" })).rejects.toThrow("not the selected song");
    const other = document.createElement("button"); other.setAttribute("aria-label", "Play");
    vi.spyOn(other, "getClientRects").mockReturnValue({ length: 1 } as DOMRectList);
    document.body.append(other);
    await expect(prepare({ site: "suno" })).rejects.toThrow("uniquely");
    expect(play).not.toHaveBeenCalled();
  });
});

describe("desktop library scanning", () => {
  it("keeps duplicate titles distinct, rejects foreign URLs and deduplicates song IDs", () => {
    document.body.innerHTML = `<main>
      <a href="https://www.udio.com/songs/abcdefgh123"><h4>Same title</h4></a>
      <a href="https://www.udio.com/songs/abcdefgh456"><h4>Same title</h4></a>
      <a href="https://www.udio.com/songs/abcdefgh123"><h4>Same title</h4></a>
      <a href="https://example.com/songs/abcdefgh789">Foreign</a>
    </main>`;
    const tracks = (0, eval)(`(${source("library")})`)();
    expect(tracks.map((t: { Id: string }) => t.Id)).toEqual(["abcdefgh123", "abcdefgh456"]);
  });

  it("reads Suno clip rows, skips unfinished clips and excludes playbar/recommendation links", () => {
    document.body.innerHTML = `<div role="group" data-testid="clip-row" data-clip-status="complete">
      <div role="button" aria-label="Play Same title"><span>3:18</span></div>
      <a href="https://suno.com/song/11111111-1111-4111-8111-111111111111">Same title</a>
    </div><div role="group" data-testid="clip-row" data-clip-status="streaming">
      <a href="https://suno.com/song/22222222-2222-4222-8222-222222222222">Not ready</a>
    </div><a aria-label="Playbar: Title" href="https://suno.com/song/33333333-3333-4333-8333-333333333333">Not in library</a>`;
    const tracks = (0, eval)(`(${source("library")})`)({ site: "suno" });
    expect(tracks).toEqual([{ Id: "11111111-1111-4111-8111-111111111111", Title: "Same title", Duration: "3:18", Site: "suno" }]);
  });
});

describe("Suno workspace pagination", () => {
  const change = (direction = "next") => (0, eval)(`(${source("pagination")})`)({ direction });
  beforeEach(() => {
    vi.useFakeTimers();
    document.body.innerHTML = `<button aria-label="Next page"></button><input aria-label="Current page number" value="1">
      <div role="group"><a href="/song/11111111-1111-4111-8111-111111111111">Song</a></div>`;
    vi.spyOn(document.querySelector("button")!, "getClientRects").mockReturnValue({ length: 1 } as DOMRectList);
  });
  afterEach(() => { vi.clearAllTimers(); vi.useRealTimers(); vi.restoreAllMocks(); });
  it("requires the page and song list to advance", async () => {
    document.querySelector("button")!.onclick = () => {
      document.querySelector("input")!.value = "2";
      document.querySelector("a")!.setAttribute("href", "/song/22222222-2222-4222-8222-222222222222");
    };
    const pending = change(); await vi.advanceTimersByTimeAsync(250);
    expect(await pending).toEqual({ Changed: true, Error: "" });
  });
  it("reports a stuck pagination control instead of claiming the scan is complete", async () => {
    const pending = change(); await vi.advanceTimersByTimeAsync(10001);
    expect((await pending).Error).toContain("did not load");
  });
  it("stops at a disabled final-page button", async () => {
    document.querySelector("button")!.disabled = true;
    expect(await change()).toEqual({ Changed: false, Error: "" });
  });
});
