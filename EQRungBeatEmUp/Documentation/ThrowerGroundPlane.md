# Thrower ground-plane correction

Thrower can start and finish Book Throw against grounded or airborne players. Jumping during the telegraph does not cancel the attack. Existing range, lane, cooldown, Ranged reservation, animation, release event and recovery remain in use.

## Coordinates and hit permission

`CharacterMotor.transform.position.x/y` stores horizontal position and walking-lane depth. `CharacterMotor.Height` stores jump elevation separately, applied to the visual child. `CombatProjectile.Initialize` resolves the target's motor and uses its root XY position. The notebook prefab enables `groundPlaneFlight`: `InitializeAtPoint` sets height velocity to zero regardless of the target's jump height. The book follows its initial direction and keeps its release height above the walking plane throughout flight and deflection. Moving between depth lanes can change the initial XY direction; there is no tracking after release.

On `NotebookProjectile.prefab` and `ScreamWaveProjectile.prefab`, **Combat Projectile > Hit > Can Hit Grounded** is enabled and **Can Hit Airborne** is disabled. The shared `CombatHurtbox.Receive` checks these permissions before damage or reactions. Even a low airborne hurtbox intersecting the projectile cannot receive a hit. Landing before contact permits ordinary damage again. Screamer retains its forward rectangular wave and can cast against an airborne player; jumping avoids its damage and Stun.

## Files

Modified:

- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CombatProjectile.cs`
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/NotebookProjectile.prefab`
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/ScreamWaveProjectile.prefab`
- `Assets/Editor/Combat/ThrowerProjectileSetup.cs`
- `Assets/Editor/Combat/ScreamerSetup.cs`
- `Documentation/BloodSheetCorridorThrower.md`
- `Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset` (refreshed build compatibility hash)

Added: `Assets/Editor/Combat/ThrowerGroundPlaneValidation.cs` and its Unity metadata, this guide, and validation reports. Setup defaults preserve the corrected projectile configuration when generating assets again; rerunning setup is unnecessary for testing and can reset existing authored tuning.

## Unity testing

1. Allow Unity 6000.4.6f1 to finish importing and compiling.
2. Save your current scene. Run **Beat Em Up > Enemies > Validate Thrower ground-plane flight (Play Mode)**. This opens HauntedHouse, enters Play Mode, creates temporary fixtures and exits Play Mode on completion. Read `Documentation/ThrowerGroundPlaneValidationResults.txt`; it must finish with `ALL THROWER GROUND-PLANE CHECKS PASSED`.
3. Open the existing AttackCoordinationTest scene and enter Play Mode. Stand within Thrower's configured range and lane; remain grounded through release and verify the book damages you.
4. Jump before the telegraph. Verify Thrower still starts, retains its Ranged reservation, releases the book and completes recovery. The book stays at release height; airborne contact causes no damage.
5. Start grounded, then jump during the telegraph. Verify the throw continues and the release occurs on its existing authored frame.
6. Jump early and land before the book arrives. Verify a grounded collision damages you.
7. Move along walking-lane depth while airborne before release. Verify the initial direction uses your ground XY position. Change depth again after release; verify the book continues straight without homing.
8. Face an incoming book while grounded and trigger the existing parry shortly before contact. Verify damage is negated and the book deflects while retaining its flight height.
9. Against Screamer, verify a grounded forward-wave contact causes Stun. Jump before release and during windup on separate attempts: Screamer must still cast, and airborne contact must cause neither damage nor Stun.

The automated fixture also tests very low airborne overlap to ensure jump immunity comes from hit permissions, not merely a visually missed collision. Its report records each checked behavior.

## Recorded validation

Unity 6000.4.6f1 passed all 35 focused checks (`ThrowerGroundPlaneValidationResults.txt`) and all 362 coordination regression checks (`ThrowerCoordinationRegressionResults.txt`) in an isolated project copy. The updated Windows development player built successfully. Two separate player processes connected through real Authentication/DTLS Relay and passed the existing Hub/boss/minion coordination smoke test (`MultiplayerBossRelay2HostResults.txt` and `MultiplayerBossRelay2Client1Results.txt`). The detailed jump/landing/parry cases were tested in Play Mode; the Relay smoke test verifies broader online integration.
