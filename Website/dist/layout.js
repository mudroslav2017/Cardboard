// Preserve the layered paper collage at the proportions of the Figma artwork.
const page = document.getElementById('page');
const landing = document.getElementById('landing');
const fit = () => landing.style.setProperty('--scene-scale', Math.min(page.clientWidth / 1440, 1));
new ResizeObserver(fit).observe(page);
fit();

// Use the visitor's wall clock and resynchronize on every tick and tab return.
const clockFace = document.querySelector('[data-node-id="1:32"]');
const clockHands = Object.fromEntries(
  [...document.querySelectorAll('[data-clock-hand]')].map(hand => [hand.dataset.clockHand, hand])
);
const timeFormat = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23'
});
let clockTimer;

function tickClock() {
  clearTimeout(clockTimer);
  const now = new Date();
  const second = now.getSeconds();
  const minute = now.getMinutes() + second / 60;
  const hour = now.getHours() % 12 + minute / 60;
  for (const [name, angle] of Object.entries({hour: hour * 30, minute: minute * 6, second: second * 6})) {
    clockHands[name].setAttribute('transform', `rotate(${angle} 29.5312 30.9375)`);
  }
  const readableTime = timeFormat.format(now);
  clockFace.setAttribute('aria-label', `Текущее местное время: ${readableTime}`);
  if (!document.hidden) clockTimer = setTimeout(tickClock, 1000 - now.getMilliseconds() + 10);
}

document.addEventListener('visibilitychange', () => {
  if (document.hidden) clearTimeout(clockTimer);
  else tickClock();
});
tickClock();
