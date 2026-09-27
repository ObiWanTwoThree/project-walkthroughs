# Brass Orrery

A one-thumb arcade game for phones, built as a single HTML page with a canvas. It needs no build step and no dependencies.

Your planet rides a brass arm around the sun. **Tap anywhere** to swing it between the inner and outer orbit. Dodge the rust-red asteroids and collect the stars. Passing an asteroid scores 1 and a star scores 3. The arm speeds up as your score climbs.

## Play it

Serve the folder over HTTP and open it on your phone:

```bash
cd mobile_game
python -m http.server 8000
```

Then visit `http://<your-computer's-LAN-IP>:8000` on a phone connected to the same Wi-Fi.

To install it like an app, host the folder over HTTPS (GitHub Pages works) and use **Add to Home Screen**. The service worker caches the game so it also works offline.

## Controls

| Input | Action |
| --- | --- |
| Tap / Space / Enter | Swap orbit |
| ↑ / ↓ | Outer / inner orbit |
| P / Esc | Pause |

## Files

- `index.html`: the whole game (rendering, physics, sound, UI)
- `manifest.webmanifest`, `icon.svg`: home-screen install metadata
- `sw.js`: offline cache

## How it works

- The game loop runs on `requestAnimationFrame` with a clamped delta time, so it behaves the same at 60 Hz and 120 Hz.
- Positions are stored as an angle plus an orbit lane rather than pixels, which keeps the world correct across screen rotations and resizes.
- Hazards are spawned ahead of the planet with a minimum angular gap that grows with speed (`0.45 + ω × 0.35` radians). Every layout is therefore survivable.
- The static dial and starfield are pre-rendered to an offscreen canvas once per resize. Only moving pieces are drawn each frame.
- Sound comes from Web Audio oscillators, so there are no audio files. Haptics use `navigator.vibrate` where supported. Your best score is kept in `localStorage`.
