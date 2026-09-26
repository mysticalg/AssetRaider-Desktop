({ site = 'udio' } = {}) => {
  const tracks = new Map();
  const selector = site === 'suno' ? '[data-testid="clip-row"] a[href*="/song/"], [role="group"] a[href*="/song/"]' : 'main a[href*="/songs/"]';
  for (const a of document.querySelectorAll(selector)) {
    const url = new URL(a.href, location.href);
    const id = (site === 'suno' ? /^\/song\/([a-f0-9-]{36})\/?$/i : /^\/songs\/([A-Za-z0-9-]{8,64})\/?$/).exec(url.pathname)?.[1];
    const hosts = site === 'suno' ? ['suno.com', 'www.suno.com'] : ['www.udio.com', 'udio.com'];
    if (!id || url.protocol !== 'https:' || !hosts.includes(url.hostname)) continue;
    const title = (a.querySelector('h4')?.textContent || a.textContent || '').trim();
    if (!title) continue;
    const row = a.closest(site === 'suno' ? '[data-testid="clip-row"], [role="group"]' : '[role="button"]');
    const clipStatus = row?.getAttribute('data-clip-status');
    if (site === 'suno' && clipStatus && clipStatus !== 'complete') continue;
    const durationRoot = site === 'suno' ? row?.querySelector('[aria-label^="Play "]') || row : row;
    const duration = (durationRoot?.innerText || durationRoot?.textContent || '').match(/\b\d{1,2}:\d{2}\b/)?.[0] || '';
    tracks.set(id, { Id: id, Title: title, Duration: duration, Site: site });
  }
  return [...tracks.values()];
}
