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
- four AI personalities and deterministic deployment/movement baseline
- visual board and step/autoplay UI
- reproducible Seed input plus one-click random games

## Quality checks

Run the engine and UI regression suite:

```sh
node test_game.js
```

The suite covers the game rules, shared browser-script scope, and the DESTINY / START control flow. GitHub Actions runs it automatically for every push and pull request targeting `main`.

Local preview:
`python3 -m http.server 8000`
then open `http://localhost:8000`.

