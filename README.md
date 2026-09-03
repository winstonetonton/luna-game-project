# Luna Game Web Prototype v1

This is the first browser integration checkpoint using the canonical game rules.

Included:
- Destiny Tower 1–4 randomization
- latest 8-unit stats/costs
- movement rules including Lance max 2, Knight forward-2 first strike, Bishop/Rook ray blocking
- melee capture-advance / Archer stays
- 3-second objective capture
- dynamic Tower victory / simultaneous final-Tower draw
- 9s warning / 10s re-evaluation state
- fourfold normalized-state repetition draw
- 180-second timeout decided by total captured Towers and Outposts
- live captured-objective score and an in-game timeout rule reminder
- copyable playtest result summaries with Seed, AI matchup, outcome, time, and scores
- direct tap deployment, unit selection, movement, and attacks for HUMAN-vs-CPU and local HUMAN-vs-HUMAN play
- iPhone safe-area layout, 44px controls, sticky mobile controls, and touch highlights
- four AI personalities and deterministic deployment/movement baseline
- visual board and step/autoplay UI
- reproducible Seed input plus one-click random games

## Quality checks

Run the engine and UI regression suite:

```sh
node test_game.js
```

The suite covers the game rules, manual human control, shared browser-script scope, and the DESTINY / START control flow. GitHub Actions runs it automatically for every push and pull request targeting `main`.

## Unity production client

The Google Play production client lives in `unity/LunaGame`. Its current core ports deterministic RNG and Destiny generation plus the 8x5 board, eight unit movement and attack sets, simultaneous damage, melee capture-advance, Archer ranged combat, Knight first strike, deployment costs, three-second objective capture, Tower victory, and the 180-second objective-majority timeout. It also includes deterministic three-second match simulation and Rush, Ranged, Raid, and Defense CPU controllers. A headless parity harness compiles the same production C# files, checks 10,000 shared Seeds, and runs every CPU pairing in GitHub Actions.

The Unity client now boots directly into a portrait mobile interface: HUMAN-vs-CPU board taps, large unit/deployment controls, highlighted legal actions, manual `+3 SEC` pacing, optional autoplay, HUD, objective state, restart, and result display. Open `Assets/Scenes/Main.unity` or any scene and press Play; the runtime bootstrap creates the interface without prefab setup.

Local preview:
`python3 -m http.server 8000`
then open `http://localhost:8000`.
