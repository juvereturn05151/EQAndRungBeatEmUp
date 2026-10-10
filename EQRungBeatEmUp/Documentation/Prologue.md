# Playable prologue

The prologue is installed in `PlayerHub.unity` and `HauntedHouse.unity`. It uses the existing player prefabs, character selection, frame combat, defense, skill meter, Thrower AI, hub and stage flow. A new story profile starts it automatically before the main game. Choose **NEW GAME** on the main menu, then select a play mode and characters normally. Story progress resets only when the match starts. **CONTINUE GAME** resumes the saved story checkpoint; it is disabled until a story save exists. After the prologue is complete, Continue returns to the sanctuary. Existing meta progression is preserved.

## Story order

1. EQ and Rung reunite at the Gatai Udom Suksa school fair. A 26-unit panorama lets players explore left and right with a following camera; reaching the far-right stalls advances the story. The unselected hero accompanies the party. Students animate everyday fair activities: eating ice cream, sipping a drink, checking a phone and waving. They sit behind both heroes at a smaller scale with warm festival lighting; hero lighting returns to normal outside the fair.
2. Chanai recognizes the two after sixteen years. They do not recognize him. He transforms the friendly uniformed boy and girl students into Rusher demons at their original positions, prompting an illustrated escape cutscene and a teleport to Prapot's sanctuary. The chase is an automatic concept-art camera push, with gameplay input locked; it has no movement objective or gameplay level transition.
3. Prapot explains that powerful magic deliberately sealed their memories. The perpetrator's name is withheld here.
4. The flashback opens with Cream alone, with EQ, Rung and player visuals hidden. Two Thai technical-school delinquents close in, then walk her offscreen together. After the captors leave, EQ and Rung enter from the left. EQ says “ไปกันเถอะเพื่อน!” and Rung replies “เออ!” Skipping restores the heroes and clears Cream and the captors before the tutorial.
5. Nine playable practice objectives cover lane movement, attacks, headbutt combo, block, parry, dash/run, grounded launcher, air smash and an enemy wave. Instructions read the actual keyboard/gamepad bindings. Powers remain locked.
6. The first Thrower Boss encounter allows combat but ends in a telegraphed, scripted defeat. Its health cannot reach zero. The party's defeat never invokes the main game's Game Over flow.
7. EQ and Rung awaken their powers, recover health, practice the existing equipped skill, and fight a winnable Thrower rematch.
8. Cream is rescued. Only then does a brief, silent Chanai silhouette prepare a memory-erasure spell.
9. Back in the present, Prapot identifies Chanai as responsible. His motive remains unknown. The party returns to the existing hub with **SAVE THE CHILDREN** as its objective.

`ChanaiRecognizesHeroes`, `MemorySealConfirmed`, `MemoryErasureWitnessed`, `ChanaiResponsible` and `ChanaiMotiveUnresolved` preserve the mystery for subsequent stages. Do not resolve the motive in future dialogue accidentally.

## Dialogue and timeline editing

Open `Assets/EQ_Rung_BeatEmUp/Story/PrologueDialogue.asset` to edit conversations, Thai/English lines, speakers, portraits, typewriter speed, optional voice blips, automatic advance delays, callbacks and choices. Conversation IDs are referenced by timeline Dialogue events. The Thai font uses Noto Sans Thai with its OFL license included under `Story/Fonts`.

`Prologue.asset` assigns environments, NPCs, combat prefabs, characters and ten timeline assets. Select any numbered timeline to use its reorderable Inspector. Positive parallel groups run consecutive events together and join before advancing. Final events run both after normal completion and after a skip; put essential flags and final visual states there. CutsceneController exposes play, pause, resume and skip methods plus started/finished events. Dialogue callbacks set named story flags. The Prapot hub NPC uses the same dialogue and cutscene controllers.

Both story heroes use their existing locomotion controllers, including the unselected companion. Following and timeline movement play Walk and return to Idle at the destination or on skip. Entering a cinematic clears player attack/run overrides and samples Idle immediately; deliberate timeline poses such as the awakening still apply afterward.

In Play Mode, timeline Inspectors expose pause/resume and skip controls. Preview is available during a playable prologue segment, when a story world and actors exist. A plays-once timeline that has already been seen applies its final state. Set playsOnce off temporarily when previewing the same sequence repeatedly.

**Beat Em Up → Story → Build or reset prologue assets** regenerates the default definition, dialogue, timelines and delinquent prefab from the source seed. It overwrites authored edits to those assets. Normal dialogue/timeline editing does not require this command. `Tools/Prologue/create_dialogue.py` produces the default bilingual seed; `package_art.py` packages the generated NPC source sheet. `package_good_students.py` packages `GoodStudentsSource.png` into the boy and girl sprites. Opening possession uses the existing Rusher for boys and a female Rusher variant for girls, with matching demon art and preserved combat timing and AI; technical-school delinquents are reserved for the flashback. Runtime does not depend on Python.

## Checkpoints and multiplayer

Story progress uses the separate PlayerPrefs key `EQRung.Story.v1`. It stores the chapter checkpoint, tutorial step and story flags. Cinematic skips apply required end states, including power unlock and Cream's rescue. Retry restarts the current playable chapter; a rematch defeat retries the rematch without repeating the scripted first loss. Tutorial progress is shared by the party.

The online host owns progression and simulation. Snapshots carry chapter, dialogue line/revision, flags, camera, fade, objective, boss health and magic-effect cues alongside the existing world sprite replication. Guests can advance dialogue and submit choices; stale revisions are rejected. Whole-scene skips belong to the host. The multiplayer content hash includes the story definition and its dependencies. Both peers must use the same project build.

## Assets and verification

| File | Responsibility |
| --- | --- |
| `Scripts/Story/PrologueDirector.cs` | Story chapters, actors, practice, bosses, checkpoints, hub restoration, replicated presentation |
| `Scripts/Story/CutsceneController.cs`, `CutsceneSequence.cs` | Sequential/parallel timelines, camera motion/shake, sound/music, poses, gameplay/stage events, skip final states, pause/resume |
| `Scripts/Story/DialogueController.cs`, `DialogueDatabase.cs` | Bilingual TMP UI, portraits, typing, advance, choices, callbacks |
| `Scripts/Story/StoryProgress.cs` | Separate versioned story save and network snapshot schema |
| `Scripts/Story/PrologueDefinition.cs` | Inspector asset references |
| `Scripts/Story/ProloguePracticeTracker.cs` | Real accepted-hit, defense, run and skill objective tracking |
| `Scripts/Story/StoryNpcConversation.cs`, `StorySpriteAnimation.cs` | Reusable nearby NPC conversation and awakening aura animation |
| `Scripts/Multiplayer/MultiplayerSession.Story.cs` | Authoritative dialogue advances and choice requests |
| `Assets/Editor/Story/PrologueSetup.cs` | Default asset construction and scene installation |
| `Assets/Editor/Story/CutsceneSequenceEditor.cs` | Timeline authoring and Play Mode preview controls |
| `Assets/Editor/Story/PrologueValidation.cs` | Automated full-flow integration checks and captures |

Existing files changed: `CharacterHealth.cs` adds an encounter-owned nonlethal health floor; `PlayerSkillController.cs` gives prologue projectiles a cleanup owner; `StageFlowController.cs` routes the existing Interact action to story NPCs; multiplayer session/menu/snapshot/catalog setup connect story readiness, synchronization, replay and content compatibility. The two existing gameplay scenes receive the director component. No existing attack assets or character animations are replaced by this prologue implementation.

New runtime systems are under `Scripts/Story`, with the networking extension in `Scripts/Multiplayer/MultiplayerSession.Story.cs`. Editor authoring and acceptance checks are under `Assets/Editor/Story`. School and hideout backgrounds, Prapot, Cream, Chanai and portraits are under `ArtAssets/Story`. Sanctuary art, EQ/Rung animations, delinquents, combat sound effects and skills reuse project assets. The Thrower boss uses a separate prefab, attack, AI profile and stronger projectile; the existing stage Thrower remains unchanged. The dragon awakening reuses its existing skill art.

Run **Beat Em Up → Story → Validate playable prologue (Play Mode)** for the automated integration check. It temporarily resets the story profile, restores the previous save afterward, exercises real combat/input APIs, and writes `Documentation/PrologueValidationResults.txt` plus screenshots in `Documentation/ProloguePreview`. Final wave and rematch clearance are forced in the integration check so it can verify downstream story state; this is not a substitute for human difficulty testing.

Verified in Unity 6000.4.6f1: **124 integration checks passed**. The complete flow reaches the existing main game. Additional checks cover cinematic combat/dialogue pause, skip end states, all nine lessons, nonlethal scripted-boss health, practice-projectile cleanup, rematch retry, all canon lines, silhouette order, camera-art coverage, saved mystery flags, snapshot serialization, English switching, Prapot interaction and choice callbacks. Captured school, sanctuary, first boss, rescue and silhouette screenshots were inspected; the sanctuary art fills the viewport and Chanai's flashback appearance is a silhouette from its first visible frame.

Remaining manual checks: balance the full rematch with both characters; play through every dialogue normally rather than skipping; test local co-op with physical controllers; run a two-machine online session with latency; profile a standalone build for the 60 FPS target. Automated snapshot serialization does not establish network latency behavior or hardware performance.

The female variant lives under `ArtAssets/Characters/Enemies/FemaleRusher`. `Tools/Prologue/package_female_rusher.py` slices its generated 4×4 pose sheet. **Beat Em Up → Story → Build female Rusher variant** imports the frames and builds separate animation, attack and AI assets, then assigns the schoolgirl transformation and refreshes the multiplayer catalog.

**Beat Em Up → Story → Update school fair activities and illustrated escape** updates the activity frames, `03_SchoolFairEscape` timeline and concept illustration without resetting dialogue. `Tools/Prologue/package_student_activities.py` slices `StudentActivitiesSource.png`. Student identity is stored independently of the animated sprite, preserving boy/girl Rusher transformation. The escape illustration clears before Prapot's scene, including after skips.
