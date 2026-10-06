# Boss multiplayer and Relay validation

This milestone preserves host-authoritative combat. Clients render boss state and effects; they never test Totem collision or grant vulnerability.

## Changes

- Boss snapshots include the actual boss controller state, phase, selected action, remaining vulnerability frames, warp index and warp version. Totems include respawn frames.
- Each active radial wave sends its identity, world origin, current radius, maximum radius, age and duration. `NetworkTotemWaveVisual` draws a cosmetic LineRenderer without a collider or combat-clock listener. Radius interpolates toward the host value and stops expanding when host combat stops. The client removes the visual when its wave disappears from the snapshot, including death and stage changes.
- Sprite warp versions force immediate positioning even for short warps. Ordinary movement retains interpolation.
- Boss warp, vulnerability, shield and phase cues, plus Totem break cues, enter the existing reliable feedback stream. Effect IDs prevent replay on repeated snapshots. Cue-generated effects carry `NetworkFeedbackVisual` so their sprites are not also replicated as duplicate effects.
- The multiplayer HUD shows boss HP, phase, shield status and the host's vulnerability countdown.
- Empty join codes receive immediate feedback. A client that starts connecting but does not receive its lobby within 30 seconds is disconnected with a retry message.
- Current protocol is `GhostFair/4/` (including the physical Hub and permanent progression); the regenerated catalog includes the new content hash. Older builds must be rebuilt.

## Reproduce the automated test

Create a **Development Build** with MainMenu first. The supplied editor method `MultiplayerValidation.BuildDevelopmentPlayer` refreshes the catalog and builds to `E:/EQRungBeatEmUp/MultiplayerValidationBuild/GhostFair.exe`.

From the project root in PowerShell:

```powershell
& ./Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 2
& ./Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 2 -Relay
& ./Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 3 -Relay
& ./Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 4 -Relay
```

Use `-Player 'C:/path/to/YourGame.exe'` for another development build. The runner starts hidden, separate processes and waits up to three minutes. Relay mode authenticates through UGS, creates a real allocation and exchanges a join code through a unique temporary file. Direct mode is a localhost control test. Reports go to `Documentation/MultiplayerBoss*Results.txt`; player logs go to `Temp/MultiplayerBoss*.log`.

The fixture enters the actual boss stage, holds combat while transmitting snapshots, and manually advances host combat frames. It checks a missed radial wave, a physical hit opening 420 frames, inclusive half-HP phase activation, the last vulnerability frame, shield restoration, boss death, wave cleanup and exit/camera unlock after remaining minions die. Clients verify each corresponding state, their cosmetic ring, absence of boss simulation and short-warp snapping. This is a synchronization test, not a human playthrough or latency benchmark.

## Test on different networks

Send the entire same build folder to another computer. Start **Play → Online Co-op → CREATE LOBBY** on the host. On the other computer, enter its Room code and select **JOIN BY CODE**. Each player toggles Ready; the host starts the game. Use another household connection or a phone hotspot to exercise a different access network. Check movement, parry responsiveness, rewards, stage advancement and the boss together. Keep the host connected throughout the run.

For an automated cross-machine boss check, start these commands on their respective computers. The host logs its room code in the specified log; pass that code to the client. Add another client command for each additional player and set the host's `--peers` accordingly. The client must join within the harness's 60-second lobby wait.

```powershell
# Host computer
./GhostFair.exe --boss-network-test --relay --host --peers 2 --output host-results.txt -logFile host.log
# Client computer, using the host's displayed code
./GhostFair.exe --boss-network-test --relay --join-code ABC123 --output client-results.txt -logFile client.log
```

## Validation limits

Separate processes on this machine can verify real Relay routing, allocation, authentication and state synchronization. They cannot establish behavior across separate household networks or under sustained latency, jitter or packet loss. Full-run reward/disconnect regression, prediction, bandwidth optimization, reconnect and host migration remain separate work. Transport still uses reliable full JSON snapshots.

## Results — 6 October 2026

- Windows development player build succeeded with Unity 6000.4.6f1.
- Boss host/client synchronization passed over actual Relay with 2, 3 and 4 player processes, using the linked cloud project and the production menu's authentication/allocation paths.
- Two-player direct transport passed as a control.
- All 85 dedicated boss gameplay/editor regression checks passed again. Evidence: `MultiplayerBossGameplayRegressionResults.txt`.
- Each Relay host/client report is saved as `MultiplayerBossRelay{players}{role}Results.txt`. The automated fixture covers exact host-frame timing and replicated state; it does not measure real-time parry responsiveness.
- UGS Wire emitted TLS handshake warnings during startup on this machine. Authentication, Relay allocation, DTLS connection and the tested gameplay replication succeeded despite those messages. Wire/Lobby functionality is not validated by this encounter test.

The first harness iteration had a join-code file-sharing race, fixed by publishing the code atomically. A subsequent fixture used a radius smaller than the distance left by arena clamping (0.79 units); the final fixture uses the authored 1.25-unit reach and asserts both whole-wave misses and physical contact. Final reports contain the corrected runs.
