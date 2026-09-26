async ({ direction }) => {
  const name = direction === 'previous' ? 'Previous page' : 'Next page';
  const button = [...document.querySelectorAll('button[aria-label]')].find(b => b.getAttribute('aria-label') === name && b.getClientRects().length);
  if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return { Changed: false, Error: '' };
  const page = () => document.querySelector('input[aria-label="Current page number"]')?.value || '';
  const fingerprint = () => [...document.querySelectorAll('[data-testid="clip-row"] a[href*="/song/"], [role="group"] a[href*="/song/"]')].slice(0, 6).map(a => a.getAttribute('href')).join('|');
  const oldPage = page();
  const oldFingerprint = fingerprint();
  button.click();
  const deadline = Date.now() + 10000;
  while (Date.now() < deadline) {
    await new Promise(r => setTimeout(r, 200));
    const current = fingerprint();
    if (current && current !== oldFingerprint && (!oldPage || page() !== oldPage)) return { Changed: true, Error: '' };
  }
  return { Changed: false, Error: `Suno's ${name.toLowerCase()} button did not load another page.` };
}
