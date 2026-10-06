# BlueShirtGuy: skill meter and Shadow Dragon

BlueShirtGuy now starts with one full bar. Press **I** on keyboard or **right trigger / RT / R2** on gamepad to cast Shadow Dragon from grounded neutral. It spends one bar immediately when the cast is accepted. Punches grant **0.06 bar per accepted enemy hit**; parries grant **0.4 bar**. Meter is retained between rooms and resets to its configured starting amount with the existing health restore / new-run event.

Existing bindings remain: J / X punch, K / Y launcher or air dive, Space / A jump, L / left shoulder guard/parry, Left Alt / right shoulder dodge. The suggestions L and right shoulder were already occupied, so Skill uses free controls. Both normal `PlayerCombatInput` and device-specific `SessionInput` read the same `Player/Skill` action; no legacy input was added.

## Tuning

| Property | Initial value | Where to edit |
|---|---:|---|
| Max / Starting Meter | 1 / 1 bar | PlayerMeter on BlueShirtGuy prefab |
| Gain On Hit / Parry | 0.06 / 0.4 | PlayerMeter |
| Multi-hit bonus | 0 | PlayerMeter; optional extra for subsequent combo hits |
| Launcher / Air-hit bonus | 0.02 / 0.02 | PlayerMeter |
| Meter cost | 1 | Skills/ShadowDragon/ShadowDragon.asset |
| Startup / release / recovery | 12 / frame 12 / 19 frames | ShadowDragonCast.asset, zero-based 60 FPS timeline |
| Cast length | 32 frames | ShadowDragonCast.asset |
| Spawn offset | X 0.8, height 0.75 | ShadowDragon.asset; X mirrors with facing |
| Speed | 7 world units/sec | ShadowDragonProjectile.prefab |
| Travel lifetime | 90 combat frames | Projectile prefab; freezes during its own hitstop |
| Collision radius / lane tolerance | 0.3 / 0.55 | Projectile prefab / Hit |
| Damage / hitstun / hitstop | 32 / 32 / 7 frames | Projectile prefab / Hit |
| Knockback / reaction | 4 / Launcher | Projectile prefab / Hit |
| Launch velocity | X 4, height 5.5 | Projectile prefab / Hit; airborne targets are forced downward |
| Maximum Targets | 0 (unlimited) | Projectile prefab; 1 means stop on first accepted target |
| Repeat Hit Frames | 0 (once per enemy per instance) | Projectile prefab; positive enables intentional timed re-hits |
| Animation holds | emergence 4, travel 4, dissipate 4 | Projectile prefab |

The skill Inspector reports the release frame and links to the cast and projectile. Open the cast in the **existing Frame Attack Editor** to move `SpawnDragon`, edit sprite holds, add recovery frames, or adjust cancel rules. Keep exactly one release event. Also keep `Swing` on the release frame for its sound. This cast has no melee hitboxes and no cancels; movement is locked through recovery. Attack, air, hitstun, guard, dodge, parry recovery, knockdown, downed, get-up, death, pause and hitstop reject the skill without spending. Interrupting an accepted startup cancels the pending dragon; the spent bar is not refunded. Already released dragons continue traveling.

Costs and gains use floating-point **bar units**, so changing Max Meter to 3, Starting Meter to 2, or a second skill's cost to 0.5 needs no runtime rewrite. `PlayerSkillController.RequestSkill(PlayerSkillData)` accepts another equipped/selected skill asset. New characters can reuse the meter, skill component and cast/projectile data.

## Hit and parry routing

`CombatHurtbox.Receive` sends actual accepted enemy damage to the existing `ComboTracker.RecordHit`; its new `AcceptedHit` event supplies hit data to `PlayerMeter`. Existing melee hit history and projectile health-based history eliminate duplicate colliders. Whiffs, immunity, friendly targets, zero damage and player blocks/parries never become accepted enemy-damage events. Skill projectile hits follow the same path and also build meter. Launcher bonus depends on the incoming hit type; air bonus depends on the attacker being airborne. Combo hit count determines the optional multi-hit bonus.

The meter subscribes to `ComboController.DefenseImpact` and awards the large gain only for `DefenseFeedback.Parry`. Normal block does not grant it. There is no meter constant inside the parry state logic. Resource mutations notify `Changed`; values clamp to max, and invalid amounts/costs are rejected.

## Cast and projectile

The cast uses `AttackPlayer` sprite override, frame clock and named events, with four generated poses. Frames 0–5 wind up, 6–11 thrust, 12–19 release/follow-through, 20–31 recover. `PlayerSkillController` handles `SpawnDragon` once per accepted cast and initializes the **existing CombatProjectile** in the player's captured facing, from the configured hand offset. The room owns spawned attacks, so room replacement cleans them up.

CombatProjectile retains the Thrower's aimed, single-target notebook default. Its new forward initialization, unlimited/finite piercing, once-per-health history, optional repeat interval and emergence/dissipation sequences support the dragon. Swept circle queries avoid tunneling and enforce teams, immunity, airborne flags and walking-lane distance. Collision resolves through CombatHurtbox and existing EnemyHitReaction. Default dragon hits launch grounded enemies into the existing landing/knockdown/get-up sequence and force airborne targets downward. The existing launcher bonus makes each dragon hit grant 0.08 bar. Hitstop pauses both actors and the projectile; expiration switches to collision-free dissipating art, then destroys the object. Scenery collision remains configurable.

Release uses the installed long whoosh sound; impact uses the installed large impact/block sound and Cartoon FX Cross spark at larger scale. Edit `ShadowDragonCast.feedback` to swap clip, volume, prefab, scale or duration. Confirmed projectile hits play this shared feedback even after the player's cast has finished. No new voice/roar recording was generated.

## HUD and multiplayer

The existing solo Combo HUD canvas now includes a permanent lower-left skill panel with a violet fill, current/max bars and I / RT hint. `MeterUIWidget` is an independent event-driven binding usable on additional canvases. Session games keep the existing per-player HUD cards and add a skill bar to each card. The solo canvas is disabled by the existing session spawning code, avoiding overlapping player panels.

Skill command bit 32 goes through the existing authoritative command handler. Only authority accepts casts, spends meter and simulates hits. Snapshots carry current/max meter; the existing sprite replication carries cast and projectile art. Projectile impact cues use the attack catalog and play cosmetics on remote clients. The catalog/content hash has been refreshed. Snapshot serialization and device routing are validated; a new live Relay peer session is not included in the automated skill suite.

## Art

Created with the **built-in imagegen tool**, using the existing BlueShirtGuy punch sheet as a character/style reference. Original design: charcoal and indigo horned shadow serpent, violet eyes, snarling jaws and a trailing flame body. No franchise design was copied. Exported point-filtered at 100 PPU with transparent backgrounds:

- `Assets/ArtAssets/Characters/BlueShirtGuy/ShadowDragon/Cast_01.png` through `Cast_04.png`: wind-up, thrust, release, recovery, 160×128 canvases, feet at the existing baseline, player palette mapped to the existing sprites.
- `Dragon_01.png` through `Dragon_06.png`: emergence, three travel variations, two dissipating poses, 256×112 canvases.
- `Source/CastGenerated.png`, `Source/DragonGenerated.png`: preserved generated sheets; `CastNativeReview.png`, `DragonNativeReview.png`: native export reviews.
- `Tools/ShadowDragon/export-sprites.cjs`: crop/nearest-sample/palette/alignment export. It does not draw the characters or dragon.
- `Tools/ShadowDragon/Prompts.md`: generation prompts and provenance.

The generated assets are hooked into gameplay; **no placeholder sprites require manual replacement**. They remain editable/swappable through the attack and projectile data.

## Exact Unity test steps

1. Open Unity 6000.4.6f1 and this project. Let scripts compile. Use **Assets > Refresh** if external changes have not imported.
2. The defaults are already configured. To reset them later, outside Play Mode choose **Beat Em Up > Skills > Configure Shadow Dragon defaults**. This resets the skill's tuning and refreshes the multiplayer catalog.
3. Open the existing HauntedHouse scene, press Play, and focus Game view. Confirm the lower-left meter starts `1 / 1 BAR`. Alternatively use **Beat Em Up > Multiplayer > Open main menu**, Play > Single Player; the existing session HUD shows the meter.
4. Press I / right trigger while grounded and neutral. Confirm cast, hand release, dragon travel, empty meter and natural recovery. Press again while empty; nothing starts.
5. Reach Entrance Gate enemies. Punch to gain small amounts; face an enemy and press L / left shoulder just before contact to parry for 0.4. A full bar caps at 1.
6. Cast through several enemies. Each takes one 32-damage hit with knockdown and a spark/sound. Face left and repeat after replenishing meter; art and travel mirror. Observe expiry and dissipation.
7. Try Skill while attacking, jumping, guarding, dodging, hitstunned, knocked down or getting up. It must not spend. Interrupt the startup with an enemy hit; no delayed projectile should spawn.
8. Outside Play Mode run **Beat Em Up > Skills > Validate Shadow Dragon (Play Mode)**. It enters/exits Play automatically and writes `Documentation/ShadowDragonValidationResults.txt` plus gameplay previews. The suite uses actual prefabs, combat stepping, collision, state logic and synthetic New Input devices.
9. For shared-projectile/defense regressions, use the existing Thrower projectile and player-defense validation menus listed in ProjectEditorGuide. For networking, use matching refreshed builds and test a host/client run; inspect each player's independently replicated meter and projectile visuals.

## Files

Created runtime: `PlayerMeter.cs`, `PlayerSkillData.cs`, `PlayerSkillController.cs`, `ComboController.Skill.cs`, `MeterUIWidget.cs` under `Assets/EQ_Rung_BeatEmUp/Scripts/Combat`.

Created editors: `ShadowDragonSetup.cs`, `ShadowDragonValidation.cs`, `PlayerSkillDataEditor.cs` under `Assets/Editor/Combat`. Created skill assets: `ShadowDragon.asset`, `ShadowDragonCast.asset`, `ShadowDragonProjectile.prefab` under `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon`. Created the art/source/export files above, this guide, validation report and `Documentation/ShadowDragonPreview` images. Unity-generated `.meta` files accompany new assets/scripts/folders.

Modified: `CombatProjectile.cs` (shared moving attack extension), `ComboController.cs` (skill completion/reset), `CombatHurtbox.cs` and `ComboTracker.cs` (accepted-hit event/data), `PlayerCombatInput.cs`, `PlayerInput_Actions.inputactions`, `ComboUIController.cs`, `StageFlowController.cs` (room-owned spawned attack root), `SessionInput.cs`, `WorldSnapshot.cs`, `MultiplayerSession.cs`, `MultiplayerMenu.cs`, `BlueShirtGuy.prefab`, `MultiplayerCatalog.asset`, and `Documentation/ProjectEditorGuide.md`.
