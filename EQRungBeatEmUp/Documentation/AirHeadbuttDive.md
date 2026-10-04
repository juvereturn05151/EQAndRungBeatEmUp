# BlueShirtGuy airborne headbutt

Press **Launcher while airborne**: keyboard **K**, or gamepad **north / Y / Triangle**. Grounded Launcher retains its existing move. The new `AirHeadbuttDive` AttackData runs through the existing ComboController, AttackPlayer, CharacterMotor and AttackHitbox; no separate combat component or Animator attack was introduced.

The player may start from a normal jump or a manual jump out of Launcher. During an air punch, Launcher buffers into the dive only when that punch's authored attack/launcher cancel window allows it. Normal AirPunch1 → AirPunch2 → AirPunch3 remains available.

## First-pass frame data

| Frames (zero-based) | Phase / drawing | Movement |
| --- | --- | --- |
| 0–1 | AirReady | Stop vertical drift; gravity 0 |
| 2–4 | Windup | Hold startup, gravity 0 |
| 5–6 | DiveStart | Forward 6 units/sec, downward 8 units/sec |
| 7–13 | ActiveDive | Continue committed travel, gravity scale 0.35 |
| 14–16 | Impact | Ground contact, horizontal velocity 0 |
| 17–23 | LandingRecovery | Grounded recovery, normal gravity |

The asset contains **24 nominal frames** at the existing 60 Hz combat rate. A high dive holds frame 13 until landing, extending flight time. Ground contact at any height jumps directly to frame 14, disables the attack hitbox and plays ten grounded impact/recovery frames, on hit or whiff. Landing uses `GroundAttack` while the dive asset finishes, then returns to `Idle`; airborne travel uses `AirAttack` with `IsAirDiving`. Movement, attacks, jump, guard and dodge cannot cancel the committed dive/recovery.

Travel is mirrored by facing and integrated by the existing motor, including arena bounds and wall collision. Frame 5 sets vertical velocity once; repeated held-frame ticks do not replay velocity, displacement or event hooks. Normal gravity/control returns after completion or interruption.

## Hit and repeat rules

Frames 7–13 have a **0.42 × 0.42** head/front torso box at offset **(0.48, 0.20)** relative to the player's ground position plus airborne height. Its horizontal offset mirrors with facing; lane tolerance is 0.4. Feet trail behind the attacking head. All active frames share hit ID 0 with no repeat interval, so each victim receives one hit per dive, including an extended frame-13 hold.

Damage **14**, hitstop **6 frames**, hitstun **22 frames**, knockback **1.2**. `AirFinisher` hits grounded enemies through the existing strong ground-hit reaction and ends airborne juggling with downward velocity **5**, using the existing floor bounce/knockdown rules. Grounded knockdown was left as an optional balance change.

`AirDiveUsed` prevents another dive before the next landing, even after interruption. Ground contact resets eligibility, but current landing recovery still rejects inputs. An airborne Launcher buffer cannot turn into a grounded Launcher after landing.

## Art and tuning

Six new transparent drawings: **AirReady, Windup, DiveStart, ActiveDive, Impact, LandingRecovery**. Each uses a **160 × 128** canvas, **100 PPU**, point filtering and pivot **(0.5, 0.0625)** / pixel **(80, 8)**. The attached head-first concept and existing `BlueShirtGuy_Walk2_01` supplied the art references. The original generated sheet, six exports, sprite strip and animated preview are preserved in `Assets/ArtAssets/Characters/BlueShirtGuy/AirDive/`.

Select `Assets/EQ_Rung_BeatEmUp/Attacks/AirHeadbuttDive.asset` in the Inspector or the existing Attack Data Editor. Sprites, frame count, hitboxes, damage, movement input scale, velocity, gravity and events remain editable. `requiresAirborne`, `landingFrame` and `airborneHoldFrame` expose the new landing/hold behavior. When changing timing, update the landing/hold indices and keep the hold index before landing. Other attacks default to no landing branch or hold.

Existing `AttackPlayer.FrameEvent` hooks emit **DiveWindup** (0), **DiveWhoosh** (5) and **DiveLanding** (14). Existing `CharacterHealth.Damaged` and HitBlinkEffect handle connected-hit feedback. No new sound library or VFX system was added.

## Files and validation

- Created `Assets/EQ_Rung_BeatEmUp/Attacks/AirHeadbuttDive.asset` and the AirDive art folder.
- Updated `Scripts/Combat/AttackData.cs`, `AttackPlayer.cs`, `ComboController.cs` and `ComboController.Defense.cs` within `Assets/EQ_Rung_BeatEmUp/`.
- Updated `Assets/Editor/AttackDataEditor.cs` and `Assets/Editor/Combat/AttackDataEditorWindow.cs` to expose the new authoring fields.
- Assigned `airDive` on `Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab`; ComboDemo and HauntedHouse inherit that assignment. Existing scene overrides were preserved.
- Created `Assets/Editor/Combat/AirDiveSetup.cs` and `AirDiveValidation.cs`. Setup preserves an existing tuned dive asset. The Play Mode validation menu checks real keyboard/gamepad Launcher actions, ground/air hits, whiff recovery, left-facing travel, high flight holds, interruption, repeat blocking, air-punch cancels, post-launch manual jump, hitbox placement and sprite import settings.
- Art was drawn using the **built-in imagegen tool**. The exact generation prompt/source are in `Tools/AirDive/generation.json`; `Tools/AirDive/package.cjs` records the export pipeline.

Unity 6000.4.6f1 validation ran in an isolated project copy. Results are saved alongside this document: `AirDiveValidationResults.txt`, `AirDiveCombatRegressionResults.txt`, `AirDiveDefenseRegressionResults.txt`, `AirDiveStageRegressionResults.txt`. The dive suite passed **56 checks**. Existing regression checks passed: **101 combat**, **188 defense**, **79 stage flow**, including normal launcher/three-air-punch routes on keyboard and gamepad. These checks verify behavior and data; final feel can be tuned in the authored asset during playtesting.
