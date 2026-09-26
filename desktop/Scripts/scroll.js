({ reset, site = 'udio' }) => {
  const link = document.querySelector(site === 'suno' ? '[data-testid="clip-row"] a[href*="/song/"], [role="group"] a[href*="/song/"]' : 'main a[href*="/songs/"]');
  let scroller = link?.parentElement;
  while (scroller && !(scroller.scrollHeight > scroller.clientHeight + 4 && /auto|scroll/.test(getComputedStyle(scroller).overflowY))) scroller = scroller.parentElement;
  scroller ||= document.scrollingElement;
  if (!scroller) return { Bottom: true, Position: 0 };
  if (reset) scroller.scrollTop = 0;
  else scroller.scrollTop += Math.max(250, scroller.clientHeight * .75);
  return { Bottom: scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - 8, Position: scroller.scrollTop };
}
