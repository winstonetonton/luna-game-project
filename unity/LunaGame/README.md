# Luna Game Unity Client

This Unity 6 project is the production Android client. The browser prototype at the repository root remains the fast balancing and deterministic simulation harness.

Current migration gate:

- C# LCG matches `game_core.js` exactly.
- Destiny Tower counts and lanes match shared Seed fixtures.
- Seeds representing Tower counts 1 through 4 are covered by Edit Mode tests.

Run Edit Mode tests from Unity Test Runner or in batch mode:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath .\unity\LunaGame `
  -runTests -testPlatform EditMode -testResults .\unity-test-results.xml -quit
```

Android Build Support is required before producing APK/AAB builds.

If Unity batch mode is unavailable, the same C# core can be compiled with the
Roslyn compiler and Mono runtime bundled with the installed Unity Editor. The
standalone parity harness in `../Tools` verifies a SHA-256 snapshot covering the
first 10,000 Seeds against `game_core.js`.

