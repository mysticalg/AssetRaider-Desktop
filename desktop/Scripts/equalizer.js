async ({ gains, enabled, gainDb = 0 }) => {
  const owner = window.__assetRaiderDesktop;
  const player = owner?.audio?.();
  if (!player) return;
  if (!owner.eq && !enabled) return;
  const wasPlaying = !player.paused;
  if (!owner.eq) {
    // CORS must be established before connecting a media element to Web Audio.
    if (!player.crossOrigin) {
      const src = player.currentSrc || player.src;
      const position = player.currentTime;
      player.crossOrigin = 'anonymous'; player.src = src; player.load();
      await new Promise((resolve, reject) => {
        const timer = setTimeout(() => finish(new Error('EQ audio loading timed out.')), 20000);
        const ready = () => finish();
        const failed = () => finish(new Error('This source does not allow equalizer playback (CORS).'));
        function finish(error) { clearTimeout(timer); player.removeEventListener('loadeddata', ready); player.removeEventListener('error', failed); error ? reject(error) : resolve(); }
        player.addEventListener('loadeddata', ready); player.addEventListener('error', failed);
        if (player.readyState >= 2) finish();
      });
      player.currentTime = position;
    }
    const context = new AudioContext();
    const source = context.createMediaElementSource(player);
    const headroom = context.createGain();
    const filters = [63,125,250,500,1000,2000,4000,8000].map(frequency => {
      const filter = context.createBiquadFilter(); filter.type = 'peaking'; filter.frequency.value = frequency; filter.Q.value = 1.4; return filter;
    });
    source.connect(headroom); let tail = headroom;
    for (const filter of filters) { tail.connect(filter); tail = filter; }
    tail.connect(context.destination);
    owner.eq = { context, headroom, filters };
    const originalStart = owner.start;
    owner.start = async () => { await context.resume(); await originalStart(); };
    const stop = owner.stop;
    owner.stop = () => { stop(); void context.close(); };
  }
  const {context,headroom,filters} = owner.eq;
  const values = gains.map(value => enabled ? Math.max(-12,Math.min(12,value)) : 0);
  // Manual preamp: do not silently cancel band boosts.
  const preamp = enabled ? Math.max(-12,Math.min(12,gainDb)) : 0;
  headroom.gain.setTargetAtTime(Math.pow(10,preamp/20),context.currentTime,.02);
  filters.forEach((filter,i) => filter.gain.setTargetAtTime(values[i],context.currentTime,.02));
  if (wasPlaying && player.paused) await owner.start();
}
