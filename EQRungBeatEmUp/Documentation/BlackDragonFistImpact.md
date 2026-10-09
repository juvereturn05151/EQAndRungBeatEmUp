# Character 1: Black Dragon Fist impact

Black Dragon Fist now gives a surviving, unprotected enemy a short upward lift and backward push, followed by the existing Falling → Knockdown → Downed → GetUp sequence. It closes the juggle window rather than behaving like the normal launcher. The move retains its original skill input, meter cost, 32-frame cast, frame-12 projectile release, damage and projectile art. Its assets retain their existing `ShadowDragon` filenames and GUIDs; the displayed skill name is Black Dragon Fist.

Heavy feedback begins when the shared projectile collision successfully registers a hit through `CombatHurtbox.Receive`, never during startup or merely releasing the projectile. The shared projectile hit history prevents another hit against the same enemy. The existing damage-driven `HitBlinkEffect` supplies the brief blink. Existing impact VFX and an existing heavy finisher sound are reused.

## Inspector tuning

Select `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon/ShadowDragonProjectile.prefab` and edit its `CombatProjectile > Hit` settings:

| Setting | Current value | Purpose |
| --- | --- | --- |
| Hit Type | KnockDown | Uses shared enemy fall/landing recovery |
| Hit Stop Frames | 11 | About 183 ms at 60 combat frames/second; normal Punch 1 remains 3 |
| Knockback | 5 | Horizontal force applied in the actual attack direction |
| Launch Velocity Y | 2.6 | Short lift before falling; airborne targets use the existing finisher fall speed |
| Knockdown Duration Frames | -1 | Uses each enemy's existing `knockdownRecoveryDelayFrames`; nonnegative values override the downed hold for this hit |
| Damage | 32, unchanged | Lethal damage keeps the existing defeat behavior |

Select `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon/ShadowDragonCast.asset` and edit `Feedback`:

| Setting | Current value |
| --- | --- |
| Impact Sound | `Assets/Deadly Kombat Free version/face_hit_finisher_73.wav` |
| Impact Volume | 0.9 |
| Impact Prefab | Existing CFXR4 Sword Hit Plain Cross |
| Impact Scale | 0.42; normal Punch 1 remains 0.12 |
| Impact Lifetime | 0.45 seconds |
| Impact Shake Strength | 0.05 world units |
| Impact Shake Duration | 0.14 seconds |

Camera feedback is a small, decaying horizontal impulse, quantized to 0.01 world units, clamped within stage/encounter bounds. It does not change the vertical floor composition or feed its offset into camera following. Resetting framing clears the shake, including when stage entry teleports the camera. Other attacks default to zero shake.

The existing knockdown animation and GetUp clip determine their own durations. Their shared timing and normal enemy downed hold (currently 45 combat frames on the tested prefabs) were not changed. Grappler Bruiser's hit-count armor and attack-frame super armor retain their existing reaction rules. The existing TotemBossController boss type keeps its previous hit reaction; its shield/damage protection still gates damage. Dead enemies stay defeated rather than getting up. In particular, the authored Thrower has 25 HP, so the unchanged 32-damage skill defeats a full-health Thrower.

## Files

Modified runtime files, all under `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/`:

- `EnemyHitReaction.cs`: handle the existing KnockDown hit type through the existing fall and landing recovery phases; honor the existing per-hit duration field.
- `AttackData.cs`: two opt-in impact camera fields; clarify the existing knockdown-duration tooltip.
- `AttackFeedback.cs`: trigger configured camera feedback only for confirmed impacts.
- `StageFraming.cs`: bounded camera impulse with no accumulated drift and clean reset behavior.

Modified assets:

- `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon/ShadowDragonProjectile.prefab`
- `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon/ShadowDragonCast.asset`
- `Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon/ShadowDragon.asset`
- `Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset`: refresh compatibility hash while preserving sprite/attack indexing.

Created editor tools:

- `Assets/Editor/Combat/BlackDragonFistSetup.cs`: **Beat Em Up → Skills → Tune Black Dragon Fist impact** reapplies these defaults. The old Shadow Dragon builder recreates older defaults; use this tuning command afterward if rebuilding that skill.
- `Assets/Editor/Combat/BlackDragonFistValidation.cs`: **Beat Em Up → Skills → Validate Black Dragon Fist impact (Play Mode)** creates an empty fixture scene and writes the validation report. Save the current scene before running it. The command tunes the skill first, so it restores the documented values.

## Verification

All 145 assertions passed in Unity 6000.4.6f1 Play Mode in an isolated project copy, using real player/enemy prefabs, the real cast/release event, swept projectile collisions and the shared combat clock. Recovery fixtures raise only instantiated enemies' HP so they survive; no enemy assets were edited. Another 48 file checks confirmed tested asset/source transfer and unchanged baseline assets.

Covered Rusher, Thrower, Screamer, Ambusher and Prefect recovery, plus left-facing Rusher; no heavy feedback during startup or release; precise 11-frame impact freeze; fall pose and displacement after the freeze; VFX spawn and assigned sound/volume; existing blink; normal downed/GetUp boundaries; one collision per target; camera expiry/reset; armor, boss shield and super-armor resistance; airborne hits; optional duration override; damage while downed; lethal defeat; normal Punch 1/2/3 and launcher reactions.

Detailed reports: `Documentation/BlackDragonFistValidationResults.txt` and `Documentation/BlackDragonFistAssetVerification.txt`. Transfer verification compares tuned assets with the tested copy and confirms unchanged normal attack, Character 2 and enemy prefab assets. Audio-source creation and clip assignment were tested; hardware audio listening, a manual Stage 1 playthrough and multiplayer sessions were not performed.
