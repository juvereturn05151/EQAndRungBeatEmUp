# Ghost Fair multiplayer

The existing characters, attack timelines, 60 FPS combat clock, enemy prefabs and nine-stage level are reused. Single Player, Local Co-op (2–4 players), and Online Co-op (2–4 machines) start from Main Menu. Foundational online gameplay was exercised through separate host/client executables over loopback. The project is now linked to UGS; the new boss has also been validated through real DTLS Relay allocations with 2–4 separate player processes. See [Boss multiplayer and Relay validation](MultiplayerBossReplication.md) for current changes, evidence and remaining cross-machine tests.

## Start in Unity

1. Let Unity finish importing the added packages and scripts.
2. Choose **Beat Em Up → Multiplayer → Open main menu**.
3. Press Play, then **Play → Single Player / Local Co-op / Online Co-op**.
4. The scene is `Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity`. It is first in Build Settings; the existing gameplay and demo scenes remain included.

Opening `HauntedHouse.unity` directly still uses the original offline scene workflow. Start from Main Menu to use the session, lobby and multiplayer HUD.

## Local joining and controls

Keyboard joins Player 1 automatically if present; otherwise the first gamepad joins. Additional controllers press **A** to join. Press **Enter** on keyboard or **A** on each assigned controller to toggle that player's Ready state. At least two players must join and everyone must be ready. **Space / controller Start**, or the host Start Game button, begins the run at the warm hub.

Each player receives a separate instance of the existing input actions, restricted to their assigned device. Keyboard and mouse form one input source. Gamepads do not share character controls. Multiple players on one keyboard are not implemented.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | Existing movement bindings | Left stick |
| Punch | J / Enter / left mouse | X |
| Launcher / airborne dive | K | Y |
| Jump | Space | A |
| Guard / parry | L | Left shoulder |
| Dodge | Left Alt | Right shoulder |
| Chapel interaction | E | Select / View |
| Choose co-op blessing | 1 / 2 / 3, or click | D-pad left / up / right |

Select/View is used for chapel interaction because the existing action asset also binds gamepad Y to Launcher. The action asset itself was not rewritten.

## Configure internet play

This requires your Unity organization/project access; it cannot be verified through the development loopback tests.

1. In Unity **Edit → Project Settings → Services**, link this project to a Unity Gaming Services project you own. Ensure the resulting Cloud Project ID is present.
2. In the Unity Dashboard for that same project, enable/configure **Authentication** with anonymous sign-in and **Relay**. Complete any required service activation.
3. Refresh the multiplayer catalog and produce one build to distribute to everyone.
4. Host chooses **Online Co-op → CREATE LOBBY**. The game signs in anonymously, allocates Relay capacity for three clients, and displays a join code in the lobby.
5. Friends choose **Online Co-op**, enter that code and select **JOIN BY CODE**. Each machine controls one player. The Online page lets you prefer keyboard or gamepad input.
6. Each player marks Ready. Only the host can start, after every joined player is ready. Lobby cards show four slots, joined status, local/remote ownership and readiness. The code has a Copy button.

Relay uses DTLS through Unity Transport. The implementation follows Unity's [Relay + Netcode integration](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo). A missing configuration produces a message in the menu; Back can cancel an outstanding connection request. Public matchmaking, friend invitations and lobby chat are not included.

## Packages

| Package | Resolved version | Purpose |
|---|---|---|
| Netcode for GameObjects | 2.7.0 | Connection approval, ownership mapping, named messages |
| Unity Transport | 2.7.3 | Direct development transport and Relay DTLS |
| Multiplayer Services SDK | 2.3.3 | Unified Unity services package, including Relay |
| Authentication | 3.7.0 | Anonymous sign-in |
| Services Core | 1.18.0 | Service initialization |

The existing New Input System and UGUI are retained. No competing networking framework was introduced. Unity version used for validation: **6000.4.6f1**.

## Authority, ownership and replication

`PlayerIdentity` gives each instantiated original player prefab a slot and owner. `SessionInput` supplies commands. The existing motor, combat controller, health, hit reactions, combo tracker and run build remain independent components on each host/local player. Local and network sessions use the same gameplay code.

In online mode the host simulates **all** gameplay, including remote players. The sender's authenticated connection maps to its own slot; it cannot select another player's character. Commands carry sequence numbers, movement, button edges, held guard and reward choices. The host rejects stale sequences and invalid movement and validates action legality through the existing combat controller. Reward selection is additionally checked against that player's pending, opened choices. A stalled input stream releases movement and guard after 0.5 seconds.

The host owns enemy targeting/AI, hit queries, damage, projectiles, destructibles, waves, stage exits, chapels, rewards, boss HP and totem vulnerability. Enemies choose the nearest living player. Any living player can activate a Player Zone, but one shared stage controller owns the encounter latch, so it spawns globally once.

The implementation uses NGO **named messages**, rather than a NetworkObject on every actor. The host sends full world snapshots at 20 Hz; client input is sent at 30 Hz plus immediate button edges. Snapshots carry sprite poses/positions/sorting, player ownership/HP, attack index and logical frame/state, enemy reactions, entity lifecycles, per-player combo results and upgrade choices, and current stage/reward/exit state. Ordered catalog indices reference existing sprites and AttackData; the attack timeline is not retransmitted continuously.

Clients disable the scene's independent gameplay actors, AI, stage controller and combat clock. They create lightweight SpriteRenderer replicas from authoritative snapshots, remove vanished entities, and interpolate positions. Stage art remains local data selected by the host's stage index. Clients never apply damage or spawn gameplay waves independently. Cosmetic swing/confirmed-hit events trigger local VFX/SFX; arbitrary particle state is not continuously replicated.

The host sends a scene-load command and waits for every connected player's loaded acknowledgement before enabling simulation. Subsequent stages remain within the same gameplay scene and follow the host's stage index. Connection approval rejects mismatched content catalogs, more than four players, or joining after the run has started.

## Rewards, cameras and defeat

Co-op uses the existing upgrade pool and chapel art with a multiplayer reward coordinator. Each living player approaches the chapel, opens their own choices and selects a blessing for their own `RunBuildState`. Selections never mutate AttackData assets. The exit remains locked until all living, connected participants finish; a disconnect removes that participant from the barrier. Safe-stage and reward protection apply to all participants. Single Player retains the original world-chapel selection flow.

Local co-op has one camera, which follows the living players' group and expands framing when they spread out, using the existing art and stage bounds. Online cameras follow their own living player. A defeated online player follows a surviving player. Player labels and independent colored HP/combos identify P1–P4.

One player's death does not end the party. All active players defeated produces Game Over; only the authority starts a new run. There is no new revive mechanic. Client disconnect removes its actor and the remaining party can continue. Host disconnect returns clients to Main Menu. There is no host migration or mid-run replacement player.

## Content authoring and debugging

Use **Beat Em Up → Multiplayer → Refresh multiplayer asset catalog** after changing player/stage content, scripts or sprite imports, and before making a release build. `MultiplayerCatalog.asset` references the existing level, player prefab, ordered sprite/attack assets and menu background. Its content hash also covers runtime source and the package manifest. Everyone should use the same distributed build.

The runtime menu uses existing warm outdoor hub artwork, the existing character pose and generated UGUI layout. No new bitmap art or stage redesign was needed. Menu and four-player lobby screenshots are in `Documentation/MultiplayerPreview/`.

In the Editor or a Development Build, **F10** opens separate development controls for localhost/LAN host/client (default port 7777), local lobby, readiness and stage jumps. Direct transport is a test path, separate from the production Relay buttons. Local host and clients must run as separate processes. LAN tests require access to the host address/port; Relay is the intended internet path.

Editor validation entries include:

- **Beat Em Up → Multiplayer → Validate local 2P full run (Play Mode)**
- **Beat Em Up → Multiplayer → Validate local 4P full run (Play Mode)**

These tests enter Play mode and clear encounters programmatically. Use a saved test scene or the Main Menu scene. Do not run them during a run you want to keep. Development-build command-line smoke tests are opt-in through `--coop-test-host`, `--coop-test-client`, or `--coop-test-local`, with `--coop-count` and `--coop-output`. They are inactive in ordinary gameplay and excluded from non-development players.

## Files and validation

New runtime files are under `Assets/EQ_Rung_BeatEmUp/Scripts/Multiplayer/`: identity/roster, device input, catalog, messages/snapshots, session, rewards, menu/bootstrap and opt-in smoke tests. New editor tools are `MultiplayerSetup`, `MultiplayerValidation` and `MultiplayerVisualValidation`. New serialized assets are MainMenu scene and MultiplayerCatalog. Build Settings, package manifest and package lock are updated.

Existing runtime extensions are limited to `StageFlowController` (party triggers/exits/rewards), `EnemyCombat` (nearest living target), `StageFraming` (party camera) and `AttackFeedback` (remote cosmetic cues). Existing player/attack/stage assets are reused; earlier unrelated project edits are preserved.

The matrix uses one offline player, two and four local players with simulated Input System devices, and independent standalone hosts with one, two and three clients. It exercises the actual nine-stage asset, authoritative projectiles and HP damage, scene/stage replication, independent upgrades and reward barriers, boss invulnerability before totem destruction, boss vulnerability afterward, and disconnect handling. The remote combat fixture sends commands through normal ownership messages and verifies Punch 1–3, launcher → AirCombo 1–3, dive, parry, held guard and dodge. Local party checks also cover all-defeated Game Over and retry. Existing ground/air, defense and combo-tracking regressions are run separately.

Full-run smoke tests accelerate enemy clears with direct fixture damage and stage positioning; they do not represent a human combat playthrough. Those foundational full-run results predate the new boss and were not rerun in the boss replication milestone. Actual Relay connectivity and the current boss synchronization are now tested; separate household networks, latency, packet loss and service quotas remain untested. There is no client-side gameplay prediction or rollback, so remote actions wait for the host's result; position interpolation improves presentation but does not remove network latency. Reliable full snapshots should be profiled on real internet links before release.

Test evidence is saved beside this guide as `MultiplayerLocal*Results.txt` and under `MultiplayerOnline2/`, `MultiplayerOnline3/`, `MultiplayerOnline4/`. See the result files for individual checks and final pass/fail status.

Previous foundational validation results:

| Configuration | Result |
|---|---|
| Offline 1 player | 39 checks passed |
| Local 2 players | 77 checks passed |
| Local 4 players | 87 checks passed |
| Host + 1 client | Host 71; client 15 checks passed |
| Host + 2 clients | Host 76; each client 15 checks passed |
| Host + 3 clients | Host 81; each client 15 checks passed |
| Ground attack regression | 293 checks passed |
| Air attack regression | 393 checks passed |
| Defense regression | 188 checks passed |
| Combo tracking regression | 25 checks passed |
| Windows development player build | Succeeded |

Menu and four-player lobby captures were inspected visually. Validation ran in an isolated copy of the current project, preserving the open working editor. No Unity Gaming Services project, credentials or service configuration were invented or changed.
