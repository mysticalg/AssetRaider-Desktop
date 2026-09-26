async ({ site = 'udio', id } = {}) => {
  window.__assetRaiderDesktop?.stop();
  const siteName = site === 'suno' ? 'Suno' : 'Udio';
  if (id && location.pathname.replace(/\/$/, '') !== (site === 'suno' ? '/song/' : '/songs/') + id) throw new Error('The page is not the selected song. Load the library and retry.');
  const buttons = [...document.querySelectorAll(site === 'suno' ? 'button[aria-label="Play"]' : 'main button[aria-label="Play"]')].filter(b => b.getClientRects().length);
  if (buttons.length !== 1) throw new Error(`The song Play button was not found uniquely. Sign into ${siteName} in the app browser, then retry.`);
  const playButton = buttons[0];
  const proto = HTMLMediaElement.prototype;
  const originalPlay = proto.play;
  const originalPause = proto.pause;
  let player;
  let active = true;
  let started = false;
  let done = false;
  let error = '';
  let src = '';
  let duration = 0;
  let lastTime = 0;
  let lastAdvance = Date.now();
  let oldVolume = 1;
  let oldMuted = false;
  let oldLoop = false;
  let oldRate = 1;
  let endedHandler;
  const restore = () => {
    active = false;
    proto.play = originalPlay;
    if (player) {
      originalPause.call(player);
      player.removeEventListener('ended', endedHandler, true);
      player.volume = oldVolume;
      player.muted = oldMuted;
      player.loop = oldLoop;
      player.playbackRate = oldRate;
    }
  };
  window.__assetRaiderDesktop = {
    stop: restore,
    start: async () => {
      if (!active || !player) throw new Error('The prepared player was lost.');
      started = true;
      lastAdvance = Date.now();
      await originalPlay.call(player);
    },
    state: () => {
      if (!player) throw new Error('No player.');
      const current = player.currentTime;
      if (current > lastTime + .01) { lastTime = current; lastAdvance = Date.now(); }
      if (player.error) error = `${siteName} reported a playback error.`;
      if (src && (player.currentSrc || player.src) !== src && !done) error = 'The song changed during recording.';
      if (started && !done && Date.now() - lastAdvance > 30000) error = 'Playback stalled or paused for 30 seconds.';
      if (started && Math.abs(player.playbackRate - 1) > .001) error = 'Playback speed changed during recording.';
      return { Duration: duration, Elapsed: current, Done: done, Error: error };
    }
  };
  try {
    player = await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error(`${siteName} did not load its audio player within 20 seconds.`)), 20000);
      proto.play = function() {
        // Suno also plays a 100 ms silent clip to keep its audio session alive.
        // It is not the selected song and must never become the recording timer.
        if (site === 'suno' && /\/sil-\d+\.mp3(?:\?|$)/.test(this.currentSrc || this.src)) return originalPlay.call(this);
        if (done || !active) { originalPause.call(this); return Promise.resolve(); }
        if (!player) {
          player = this;
          oldVolume = this.volume; oldMuted = this.muted; oldLoop = this.loop; oldRate = this.playbackRate;
          // Prime metadata without audible playback, then reset before capture starts.
          this.muted = true;
          clearTimeout(timeout);
          resolve(this);
        } else if (this !== player) {
          error = 'A different player attempted to start.';
          return Promise.resolve();
        }
        return originalPlay.call(this);
      };
      playButton.click();
    });
    const deadline = Date.now() + 25000;
    while (!(Number.isFinite(player.duration) && player.duration > 0 && player.readyState >= 2)) {
      if (!active || Date.now() > deadline || player.error) throw new Error(`${siteName} could not prepare playback. Try playing the song in the app browser first.`);
      await new Promise(r => setTimeout(r, 100));
    }
    originalPause.call(player);
    duration = player.duration;
    if (duration > 1800) throw new Error('This version supports tracks up to 30 minutes.');
    player.loop = false; player.playbackRate = 1; player.currentTime = 0;
    while (player.seeking || player.currentTime > .05) {
      if (Date.now() > deadline) throw new Error('Could not rewind the song to its start.');
      await new Promise(r => setTimeout(r, 50));
    }
    src = player.currentSrc || player.src;
    player.volume = 1; player.muted = false;
    endedHandler = event => { done = true; originalPause.call(player); event.stopImmediatePropagation(); };
    player.addEventListener('ended', endedHandler, true);
    return duration;
  } catch (e) { restore(); throw e; }
}
