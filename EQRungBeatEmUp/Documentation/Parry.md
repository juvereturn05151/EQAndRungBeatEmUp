# Parry timing and feedback

Parry uses the existing Guard input, player defense states, combat frame clock, enemy interruption and AttackFeedback component. No coroutine or independent defense timer drives the active window.

| Setting | Previous | New default |
|---|---:|---:|
| Active window | 5 frames | 8 frames (0–7) |
| Successful parry hitstop | 5 frames | 6 frames |
| Enemy parry stun | 18 frames | 90 frames (dedicated Stunned state) |
| Player success recovery | 8 frames | 8 frames |
| Counter advantage | 10 frames | 82 frames |
| Additional re-arm delay | None | 6 frames after the full active window |

At 60 combat FPS, eight frames are approximately 133 ms. Durations use combat frames and pause with hitstop. Run upgrades still modify the effective parry window and attacker stun.

## Tune it in Unity

Select `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset`. Its existing defense-data Inspector now includes an editable **Parry Active Frames** field and a zero-based frame track: green is parry opportunity and blue is normal Guard. Experiment with 6–10 frames; eight is a starting value, not a permanent code limit.

Edit **Parry Hitstop Frames**, **Parry Attacker Stun Frames**, **Parry Recovery Frames**, and **Parry Rearm Delay Frames** on the same asset. Counter advantage is displayed as normal enemy stun minus player success recovery; tune those two values to change it. With the defaults, both melee actors freeze for six frames, then the player recovers after eight frames while the enemy has eighty-two stun frames left. Bosses are exempt. Projectile parries reflect eligible projectiles and never stun the shooter remotely. See `ParryResponses.md` for ownership, stun animations, armor and exact testing.

The Inspector's **Successful parry VFX / SFX** section exposes Impact Sound, Volume, Prefab, Scale and Lifetime. Defaults reuse `block_large_71.wav` and the existing Cartoon FX `CFXR4 Sword Hit PLAIN (Cross)` flash/sparks. They are distinct from regular punch impact feedback and are replaceable. Feedback is emitted only after an accepted parry; ordinary blocking does not trigger it. The existing effect renderer disables library camera shake and lights on its temporary instances.

In the existing Attack Data Editor, each hitbox now exposes **Can Be Parried**. It defaults to true for existing attacks. Disable it on a blockable attack to retain normal Guard/chip rules without parry stun or effects. **Unblockable** continues to bypass both Guard and parry. The existing Screamer scream remains unblockable by default. No enemy names are checked.

## Guard flow and re-arm

* A fresh eligible Guard press immediately enters GuardEnter at frame 0 with parry active. There is no startup delay.
* Contact on frames 0–7 produces Parry: no incoming damage, knockback or knockdown. Normal melee attackers enter Stunned; eligible projectiles reflect instead. Feedback and six-frame hitstop remain.
* At frame 8, holding the same button automatically becomes GuardHold. No second input is required. Later contact uses normal block/chip/blockstun rules and does not stun the enemy through parry.
* Holding Guard cannot regenerate the window. The re-arm timer also cannot regenerate it automatically.
* Releasing early does not clear the re-arm timer. With the defaults, another parry press becomes eligible fourteen combat frames after the original press: eight active plus six additional delay frames.
* Repressing while re-arm remains immediately gives normal Guard. After the timer expires, release and make a new eligible press to deliberately open the next window.
* Successful-parry recovery, hitstun, attacks, dodge, airborne, knockdown, get-up, death and existing guard restrictions still prevent incompatible activation. Re-arm timing freezes during hitstop and advances on the existing combat clock even when the player is neutral.

For a counterattack, release Guard and press Attack after the successful-parry recovery. Continuing to hold Guard returns to normal Guard. The eighty-two-frame default advantage provides time to start the counter while a normal melee enemy is still stunned; it does not guarantee every attack's active frame will connect.

## Debugging

In Play mode, select the player's **Combo Controller** component. Its live panel shows state, zero-based defense frame/window, Parry Active, Guard Active, re-arm frames remaining and effective counter advantage. During the window the state is GuardEnter; the existing Parry state is the successful deflection/recovery pose. At expiry the state is GuardHold, Parry Active is false and Guard Active remains true.

The existing CombatDebugOverlay also displays the parry frame, active flags and re-arm count when Show Debug is enabled with its player/enemy references assigned.

## Exact test steps

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`, save scene changes, and run **Beat Em Up → Validate player defense (Play Mode)**. It creates temporary actor instances and defense-data copies, exercises timing/input/feedback, writes `Documentation/PlayerDefenseValidationResults.txt`, and exits Play mode. The initial updated run passed 272 checks. A rendered successful-parry capture is saved in `Documentation/ParryPreview/Success.png`.
2. For a hands-on fight, use the existing level editor to simulate a combat encounter containing a Rusher or place the existing BadGuy prefab within a combat stage's movement bounds. Use a combat stage; the hub/shrine's damage protection deliberately rejects incoming combat hits.
3. Face the attacker and press **L** (keyboard) or **Left Shoulder** (gamepad) shortly before contact. Contact within frames 0–7 should produce the distinct flash/sparks and sound, six-frame hitstop, zero player damage/recoil, and an interrupted enemy.
4. Press Guard much earlier and keep holding it. Watch the live panel transition at frame 8 to GuardHold. Incoming attacks should now block under normal rules, with no parry stun or success effect.
5. Press Guard after taking a hit. Existing hitstun should remain; the late press must not erase damage. Repeat while airborne, attacking, knocked down and getting up to verify restrictions.
6. Release/repress rapidly. Guard should remain usable, but the live re-arm timer should prevent repeated parry windows. Keep holding past timer expiry: it should stay normal Guard until another fresh eligible press.
7. After a successful parry, release Guard and press **Enter** (keyboard) or **West** (gamepad) once the eight-frame recovery ends. Confirm your attack starts while the enemy remains stunned.
8. Change Parry Active Frames to 6, then 10, and repeat the early/late tests. The frame track and live effective window should follow the data. Restore 8 if you want the initial tuning.
9. Disable Can Be Parried on a test attack while leaving Unblockable off: it should block/chip without parry feedback. Enable Unblockable to confirm defense is bypassed. Restore the test attack afterward.

**Beat Em Up → Defense → Configure parry defaults** resets parry timing/feedback to the defaults above and refreshes the multiplayer catalog. It replaces tuning edits; normal Inspector tuning does not require this command. After changing defense data for an online build, run the existing multiplayer catalog refresh and use matching builds on both peers.

Multiplayer clients receive accepted parry VFX/SFX cues; hosts retain all defense and stun authority. Separate online peers were not rerun during this change.

## Changed files

* `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/PlayerDefenseData.cs` and `PlayerDefense.asset`: configurable 8/6/24 defaults, re-arm data, feedback references and computed advantage.
* `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/ComboController.Defense.cs`: existing-clock re-arm, active/debug properties and feedback connection.
* `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/AttackData.cs` and `Assets/Editor/Combat/AttackDataEditorWindow.cs`: reusable Can Be Parried hitbox setting.
* `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/AttackFeedback.cs`: successful defense feedback through the shared effect/audio renderer.
* `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CombatDebugOverlay.cs`: optional live parry display.
* `Assets/EQ_Rung_BeatEmUp/Scripts/Multiplayer/MultiplayerSession.cs` and `Resources/MultiplayerCatalog.asset`: accepted parry cues and refreshed catalog/hash.
* `Assets/Editor/Combat/PlayerDefenseSetup.cs`: default configuration command.
* `Assets/Editor/Combat/PlayerDefenseValidation.cs` and its results: updated expectations plus boundary, re-arm, compatibility, feedback and counter tests.
* `Documentation/ProjectEditorGuide.md` and `BlueShirtGuyDefense.md`: updated defense editor instructions and tuning.

Created: `Assets/Editor/Combat/PlayerDefenseDataEditor.cs` and metadata, this guide, and `Documentation/ParryPreview/Success.png`.
