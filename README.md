# Echo

Turn-based slide puzzle. Two tokens race for gems. After every 5 of your moves a ghost comes back and replays them. Each player keeps at most one ghost for now.

Play vs a bot (easy / medium / hard) or hot-seat 2-player on the same keyboard.

## Controls

- Arrow keys, WASD, on-screen arrows, or swipe to tilt
- **Z** undo (also undoes the bot reply)
- **R** new game
- **H** cycle mode
- **1–4** Easy / Medium / Hard / 2 Players

## Unity

Open this folder in **Unity 6**. The Main scene already uses the Switchback GameUI, now wired to Echo.

To publish WebGL:

1. `Echo → Build WebGL to docs` (or File → Build Profiles → Web, output `docs`)
2. Push `main`
3. GitHub → Settings → Pages → Deploy from branch `main` / folder `/docs`

Live URL after Pages is on: https://samarthraj7.github.io/Missing_Piece_Game/
