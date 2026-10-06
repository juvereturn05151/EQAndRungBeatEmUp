# Melee parry stun and projectile reflection

Successful melee parry interrupts a normal enemy and enters `EnemyReaction.Stunned` for **90 combat frames**. Successful projectile parry instead reverses the projectile and changes its owner/faction to the defending player. It never remotely stuns or interrupts the shooter. Existing Guard input, eight active frames, player Parry recovery, hitstop, skill meter and feedback remain in use.

## Universal enemy status

The existing `EnemyHitReaction` owns the appended **Stunned** and optional **StunRecovery** branches; existing serialized enum values remain unchanged. It stops AttackPlayer, which clears active melee hitboxes and invokes existing grab/lunge/leap/telegraph interruption listeners. AI pending choices are cleared. Enemy AI and AttackPlayer reject offensive actions until recovery ends. Airborne committed grabs are stopped and grounded at their current ground XY position.

The timer advances through CombatClock and pauses during hitstop. Normal punish hits still damage the enemy and retain the remaining status window. Launchers/special hit reactions can replace the status with the existing combat reaction. Death and restore override/clear it. Recovery automatically releases motion and sprite control, and the existing AI graph resumes.

**Tuning:** select `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset` → **Parry Attacker Stun Frames** (default90). Existing run-upgrade ParryStunBonus still applies. **Parry Recovery Frames** defaults8, giving82 base frames of normal-enemy counter advantage after melee hitstop. Each enemy's **Enemy Hit Reaction** exposes **Stun Recovery Frames** (default0), **Can Be Parry Stunned**, **Stun Animation**, fallback sprite, overhead VFX frames/hold/offset/scale and optional debug display. The existing Enemy AI Editor live panel shows status, remaining/total frames and effective eligibility.

Grappler parry bypasses six-hit armor without consuming or resetting its count. While Stunned/StunRecovery, armor absorption is suspended, so ordinary punish hits damage it. Remaining armor resumes under its existing rules after the status ends or another special hit reaction replaces it. Armor-break hits still use the existing60f GroundHit stagger and300f recovery rule; armor break was separated from the old generic parry interrupt method.

Boss exclusion uses **Can Be Parry Stunned** plus the existing **TotemBossController** classification, including when that component is disabled. Boss attacks marked parryable still negate player damage, emit parry feedback, grant meter and participate in existing melee hitstop; they do not enter generic stun or have their attack timeline cancelled. Future boss types can set the same flag false. Runtime logic does not check enemy names.

## Animation assets and feedback

The status loops sprite frames from an existing **AttackData** asset through CharacterAnimation.HoldSprite; it does not run that asset as an offensive attack or execute its events/hitboxes. Its frame list can be edited in the normal Attack Data Editor. Animator is suppressed while these poses are held.

| Enemy | Stun data | Artwork |
|---|---|---|
| Rusher | `Rusher_Stunned.asset` | Marked placeholder: own last Hurt_Light poses |
| Thrower | `Thrower_Stunned.asset` | Marked placeholder: own last Hurt_Light poses |
| Screamer | `Screamer_Stunned.asset` | Existing dedicated six-frame Stunned sequence |
| GrapplerBruiser | `GrapplerBruiser_Stunned.asset` | Marked placeholder: own last Hurt_Light poses |
| Ambusher | `Ambusher_Stunned.asset` | Marked placeholder: own last Hurt_Light poses |
| Prefect | `Prefect_Stunned.asset` | Marked placeholder: own last Hurt_Light poses |

These assets live under `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks`. BadGuy also supports the same status, with its existing airborne hit-pose fallback and shared indicator. The normal-enemy placeholders should eventually receive dedicated dizzy body artwork; no new body sprites were generated. All reuse the existing pixel-art overhead star/spiral sequence from PlayerDefense. It follows the enemy, loops on combat frames, and hides on status end, death or disable. Captured reviews are in `Documentation/ParryResponsePreview`.

## Projectile ownership and tuning

The existing CombatProjectile swept collision passes projectile context into CombatHurtbox/Guard. Front-side defense uses incoming travel facing, rather than the remote shooter's current position. A parryable but non-deflectable notebook-style projectile is neutralized without shooter stun; unparryable projectiles retain ordinary Guard/unblockable rules.

`Deflect` reverses ground velocity and height velocity, applies **Deflect Speed Multiplier**, changes owner to the defending CharacterMotor and faction to that player's existing hurtbox team, clears enemy-phase hit history, mirrors the sprite, and flashes it briefly cyan. Collision excludes the parrier and every allied player through existing team filtering. The original shooter is no longer the excluded owner and can be hit unless **Can Hit Original Owner** is unchecked.

On `NotebookProjectile.prefab`, initial defaults are:
- **Hit → Can Be Parried:** true; **Can Be Deflected:** true.
- **Max Deflections:**1. Extra attempts are rejected.
- **Deflect Damage Multiplier:**1; **Deflect Speed Multiplier:**1.2.
- **Deflect Hitstun Frames:**18; **Deflect Knockback:**1.
- **Can Hit Original Owner:**true.

Reflected hits use runtime copies of the original hit data, normal enemy hit reactions/armor rules, existing combo credit and accepted-hit filtering. They do not mutate the prefab's hit data. Age and traveled distance remain unchanged on reflection; the finite original lifetime/distance cap continues. Hitstop pauses movement/lifetime consistently with the existing projectile clock.

The defender and projectile receive parry hitstop. The distant shooter is not frozen by the projectile parry. Existing player parry sparks/SFX accompany a separate deflection sound (`blade_hit_08.wav`), projectile flash and visible reversal. The separate cue asset is `Projectile_DeflectFeedback.asset`, editable through normal attack feedback fields.

**ScreamWaveProjectile.prefab** defaults to **Can Be Parried=false**, **Can Be Deflected=false**. Its existing unblockable sonic-wave behavior remains. Both permissions are configurable in the Combat Projectile Inspector; enabling parry also requires disabling Hit → Unblockable. Moving attacks are not automatically reflectable. Ground-wave reflection, if deliberately enabled, mirrors the whole crescent arrangement and keeps rectangular lane filtering.

Both melee parry and projectile deflection emit the same successful DefenseImpact. **PlayerMeter → Gain On Parry** remains0.4, with existing clamping and one gain per success. A later reflected damage hit can separately earn normal hit meter/combo credit. Boss parry also grants successful-parry meter.

The projectile Inspector and optional viewport debug show owner, faction, velocity/direction, permissions, deflection count and age. Runtime decisions remain on the existing host combat simulation. Host snapshots transmit stun poses/indicators and reflected visuals; the deflection sound uses a registered catalog feedback cue. Independent network peers were not launched during validation.

## Exact Unity test steps

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`.
2. For automatic coverage, use **Beat Em Up → Validate parry stun and projectile deflection (Play Mode)**. It enters Play, tests fixtures, writes `Documentation/ParryResponseValidationResults.txt` and returns to Edit Mode. Existing **Validate player defense** and Grappler/Ambusher/Prefect/Screamer validation menus remain available.
3. For manual tests, select ThaiHauntedHouse.asset, choose a combat stage/encounter and configure a single enemy in a wave's Spawn Groups. Test each of the six normal enemy prefabs in turn. Use **Simulate Encounter** to run that encounter. Keep both characters in the same walking lane and face the enemy.
4. Press **L** or **gamepad left shoulder** shortly before a melee hit. A fresh press has active frames0–7. Hold longer to test normal Guard. Release and wait the existing rearm interval before another attempt. J is the normal attack input.
5. Confirm no player damage, cancelled enemy attack, overhead indicator, held stun body poses, inability to move/attack, and automatic recovery. Check **Enemy AI Editor** or **Enemy Hit Reaction → Stun Debug** for90f remaining. Release Guard and attack after the player's8f recovery to punish.
6. Against Grappler, leave all six armor points intact. Parry its active lunge/grab, confirm the lunge and grab hitbox stop, player is never Grabbed, armor count remains6 and punish attacks damage it. Ambusher's descending active grab is likewise parryable; its initial leap/telegraph is not itself a hit.
7. Against Prefect, parry her close Push. Her pending AI action stops and no later call/push event fires from the cancelled timeline.
8. Against Thrower, face the incoming notebook and press Guard just before it reaches you. Confirm reversed travel/visual, player ownership/faction, unchanged shooter status at deflection, and later normal damage/reaction when it returns to the Thrower. Test another local player in its return path: neither allied player is damaged.
9. Set Damage Multiplier2 or Hitstun27 on a test notebook prefab/copy to verify tuning. Set Can Hit Original Owner=false to test immunity, or Can Be Deflected=false to test neutralization. Keep Max Deflections1 and observe finite lifetime. Save tuning outside Play if it should persist.
10. Test the scream wave: Guard/parry does not reflect it under its default settings. Test a parryable boss melee attack: player avoids damage with feedback, while the boss never enters generic Stunned. Boss totem invulnerability remains a separate existing rule.

## Files created / modified

Created (plus Unity metadata):
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/EnemyHitReaction.Stun.cs`
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CombatProjectile.Deflection.cs`
- `Assets/Editor/Combat/ParryResponseSetup.cs`
- `Assets/Editor/Combat/ParryResponseValidation.cs`
- Six per-enemy `*_Stunned.asset` sprite timelines and `Projectile_DeflectFeedback.asset` in the HauntedHouse Attacks folder.
- This guide, validation results, and six review captures in `Documentation/ParryResponsePreview`.

Modified:
- Combat runtime: `EnemyHitReaction.cs`, `EnemyAIController.cs`, `AttackPlayer.cs`, `ComboController.Defense.cs`, `CombatHurtbox.cs`, `CombatProjectile.cs`, `CombatGrabController.cs`, `HitCountArmor.cs`, `AttackData.cs`, `PlayerDefenseData.cs`.
- Multiplayer runtime: `MultiplayerSession.cs` (deflection feedback cue).
- Editors: `EnemyAIProfileEditor.cs`, `CombatProjectileEditor.cs`, `PlayerDefenseSetup.cs`, `PlayerDefenseValidation.cs` (updated expectations for90f dedicated stun).
- Assets: `PlayerDefense.asset`; Rusher, Thrower, Screamer, GrapplerBruiser, Ambusher, Prefect, WhiteGhostBoss and BadGuy prefabs; NotebookProjectile and ScreamWaveProjectile prefabs; MultiplayerCatalog.asset.
- Documentation: `Parry.md`, `ProjectEditorGuide.md`, refreshed player-defense and enemy regression result files/previews.

**Beat Em Up → Configure parry stun and projectile deflection** reapplies the initial defaults/animation assignments and refreshes the multiplayer catalog. Use it for setup, not after tuning values you intend to retain. No Input System bindings, Animator Controllers or duplicate enemy brains were added.
