# Luna Game Unity Client

This Unity 6 project is the production mobile client. The browser prototype at the repository root remains the fast balancing, iPhone playtest, and deterministic simulation harness.

Current implementation:

- C# LCG matches `game_core.js` exactly.
- Destiny Tower counts and lanes match shared Seed fixtures.
- Match rules, four CPU styles, and HUMAN-vs-CPU tap controls run in C#.
- `Assets/Scenes/Main.unity` boots a portrait mobile interface.
- Seeds representing Tower counts 1 through 4 are covered by Edit Mode tests.

Run Edit Mode tests from Unity Test Runner or in batch mode:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath .\unity\LunaGame `
  -runTests -testPlatform EditMode -testResults .\unity-test-results.xml -quit
```

Build commands are available under Unity's `Luna > Build` menu:

- `Android Development APK` for device testing.
- `Android App Bundle` for Google Play upload.
- `iOS Xcode Project` for signing and device/App Store builds on a Mac with Xcode.

Android or iOS Build Support must be installed in Unity Hub for the selected export. This Windows machine can create Android packages; completing and signing the iOS app requires macOS and Xcode.

If Unity batch mode is unavailable, the same C# core can be compiled with the
Roslyn compiler and Mono runtime bundled with the installed Unity Editor. The
standalone parity harness in `../Tools` verifies a SHA-256 snapshot covering the
first 10,000 Seeds against `game_core.js`.
