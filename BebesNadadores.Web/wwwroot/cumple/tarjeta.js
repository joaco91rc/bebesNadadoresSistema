(() => {
  'use strict';
  const confetti = document.getElementById('confetti');
  const musicBtn = document.getElementById('musicBtn');
  const audio = document.getElementById('birthdayAudio');
  const colors = ['#78dff7', '#ffe38d', '#ff9ec8', '#ffffff', '#59c6f2', '#b9f4ff'];
  function burst(amount = 45) {
    if (!confetti) return;
    const fragment = document.createDocumentFragment();
    for (let i = 0; i < amount; i++) {
      const piece = document.createElement('span');
      const round = Math.random() > 0.75;
      piece.className = 'confetti-piece' + (round ? ' is-round' : '');
      piece.style.setProperty('--left', `${Math.random() * 100}%`);
      piece.style.setProperty('--delay', `${Math.random() * .6}s`);
      piece.style.setProperty('--duration', `${3.4 + Math.random() * 2.2}s`);
      piece.style.setProperty('--rotate', `${Math.random() * 360}deg`);
      piece.style.setProperty('--drift', `${-40 + Math.random() * 80}px`);
      piece.style.setProperty('--color', colors[i % colors.length]);
      piece.addEventListener('animationend', () => piece.remove(), { once: true });
      fragment.appendChild(piece);
    }
    confetti.appendChild(fragment);
  }
  document.addEventListener('DOMContentLoaded', () => {
    burst(65);
    setTimeout(() => burst(35), 1200);
    setInterval(() => burst(18), 3800);
  }, { once: true });
  if (document.body.dataset.hayMusica === 'true' && musicBtn && audio) {
    musicBtn.hidden = false;
    musicBtn.addEventListener('click', async () => {
      if (!audio.paused) {
        audio.pause();
        musicBtn.textContent = '♫ Activar música';
        musicBtn.setAttribute('aria-pressed', 'false');
      } else {
        try {
          await audio.play();
          musicBtn.textContent = '♫ Pausar música';
          musicBtn.setAttribute('aria-pressed', 'true');
        } catch {
          musicBtn.textContent = 'No se pudo reproducir la música';
        }
      }
    });
  }
})();
