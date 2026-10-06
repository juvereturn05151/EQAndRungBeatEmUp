# Project editor guide

This guide covers the custom authoring editors and menu commands currently implemented in this project. Most editors appear in Unity's Inspector when you select the corresponding asset or component. There are two dedicated authoring windows: Attack Data Editor and Enemy AI Editor.

## Where to start

- Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity` for offline stage testing.
- Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset` to edit the level.
- Select **Haunted House Stage Flow** in the Hierarchy to access scene references, stage previews and Play Mode debug controls. Its **Edit level / ordered stages** button selects the level asset.
- Use the Project window for saved assets, the Hierarchy for scene instances, Scene View for layout/handles and Game View for actual Play Mode gameplay.
- Shared asset edits affect all users of that asset. Duplicate an asset with Ctrl+D before creating a separate variation. Prefab Mode edits affect future spawns; scene-instance changes require applying prefab overrides if they should be permanent.
- Temporary stage/encounter preview transforms are not saved authoring data. Edit the level asset and its handles. Play Mode component edits normally revert when Play stops; editing a ScriptableObject asset can persist.

## Level/stage editor

**Open:** select ThaiHauntedHouse.asset. **Alternative:** select Stage Flow, click Edit level / ordered stages.

1. Click a stage in **Stages — drag to reorder**. Expand **Selected stage**.
2. Set a unique Stage ID, display name and Stage Type. Safe stages omit encounter/destructible gameplay.
3. Assign Background Sprite and Floor Sprite. Art Width affects their world width. Background/Floor Height and Center Y control composition independently of walkable bounds. Reset to Default Layout resets those four height/center values.
4. Set Movement Min/Max, player entry/exit and exit radius. The green preview rectangle is walkable ground XY; yellow marks the exit. Art sizing does not resize the movement area.
5. Choose completion mode and Reward After Clear. Required combat zones still gate ReachExit. Next Stage Index = -1 follows list order; explicit indices are zero-based, and stage count ends the level.
6. Add destructibles or decorative props in their lists. Destructibles accept a prefab or intact/damaged/broken sprites, HP, hitbox, position and optional drop. CursedTotem participates in the boss protection rules.
7. Edit chapel/card/recovery placements when the stage uses those rewards or recovery interaction.
8. Use **Preview Selected Stage**, **Previous Stage**, **Next Stage**, **Refresh Preview**, **Focus Selected Stage** and **Clear Scene Preview**. Edits and Undo refresh the selected stage without changing the game's runtime stage.

The preview is render-only. Showing all enemy waves together is a layout view, not simultaneous gameplay spawning. Press Play or use Simulate Encounter to test gameplay.

Details: `LiveStageEditing.md`, `StageArtLayout.md`, `HauntedStageFlow.md`.

## Encounter zones and simple spawn triggers

These controls extend the same level and Stage Flow inspectors.

1. Select a stage; click **+ Add Encounter Zone** for locked combat, or **+ Add Spawn Trigger** for an unlocked traversal enemy spawn.
2. Expand its entry under the selected stage's **Encounters** list. Set Encounter ID, trigger and delay. PlayerZone starts when any living player enters/crosses the trigger. StageEnter, Time, PreviousEncounterClear and Manual are also supported.
3. Combat zones enable Use Combat Bounds. Set Combat Bounds, Camera Bounds, Lock Stage Until Clear, Lock Camera, Exit Lock, Required For Completion, Clear Condition and One Shot. Keep IDs unique for manual signals.
4. Expand an encounter or click **Select** to immediately see its Scene bounds. Center arrows move each rectangle; edge/corner squares resize it. Orange is the trigger, red is combat, blue is camera, and cyan marks enemy positions. **Show All Encounter Bounds** and the three visibility toggles control drawing. Disabled bounds remain dimly visible. Numeric fields and handles edit the same data with Undo.
5. **+ Add Wave** creates the first Immediate wave or a subsequent After Previous Wave Cleared wave. Expand Waves to set the trigger and delay.
6. Choose a specific Wave in the preview dropdown. **+ Add Enemy Spawn / Point to Selected Wave** adds an enemy group; assign its prefab, count, interval and group delay in the Encounters list. When All Waves is selected this targets Wave 1.
7. Per-group **+ Spawn Point** adds another position. Move it with cyan handles. Counts exceeding the number of positions cycle through the positions.
8. Duplicate Encounter Zone copies its wave/spawn data independently. Delete supports Undo; review explicit dependency warnings after deleting an encounter.
9. **Simulate Encounter** enters Play Mode at the selected encounter, skipping earlier encounters for that debug run. In Play Mode it restarts that stage directly. Stop Play to end simulation. Clear Preview restores the original editor display without undoing asset edits.

Immediate and After Delay wave delays are measured from encounter activation. After Previous Wave Cleared delay starts at previous-wave completion. Enemy group delay starts at its wave's start, and Interval spaces subsequent enemies.

An encounter normally clears after every queued spawn and wave is finished and all its enemies are defeated. ManualSignal also waits for an explicit clear signal. Clearing releases player and camera limits. One Shot prevents revisits from respawning enemies; disabling it re-arms a PlayerZone after all living players leave its trigger.

Details and sample setup: `CombatEncounterZones.md`. Entrance Gate now has two independently triggered test arenas.

Encounter/wave headers now have **Enabled** and **Select** controls. Disabled encounters skip required progression by default; **Disabled Blocks Progression** can explicitly keep it blocked. Disabled waves are skipped and previous-clear timing finds the previous enabled wave.

Expand an encounter or wave's **Scene Object States** list. Add rows, then drag Hierarchy objects into **Apply | Active | Object** fields. Encounter states run first; wave states override only listed objects. **Restore Scene Objects On Encounter End** optionally restores their original states. Save the level asset and scene: actual scene references live on Stage Flow. **Preview Encounter State**, per-wave preview buttons and **Restore Preview** inspect these states temporarily outside Play Mode. Preview restores automatically before scene save, Play Mode or script reload. Full workflow and file list: `EncounterEditor.md`.

## Stage Flow component inspector and debug

Select **Haunted House Stage Flow**. The normal Inspector assigns Level, Player, Framing, background/floor renderers, ambience and HUD settings. **Editor Stage** shares its authoring selection with the level inspector. It does not jump the running game.

During Play Mode, the inspector displays Current Stage and Exit state, with **Jump to [stage]** and **Advance if unlocked** buttons. F8 opens the offline stage/wave debug panel: signal encounters/waves, inspect live enemies and wave state, complete stage events and jump stages. Debug jumps restart the stage runtime. In online play encounter decisions belong to the host.

## Attack Data Editor window

**Open:** Tools → Combat → Attack Data Editor; double-click an AttackData asset; or select one and click **Open Attack Data Editor timeline**. Player attacks are under `Assets/EQ_Rung_BeatEmUp/Attacks/`; haunted enemy attacks and AI variants are under `Levels/HauntedHouse/Attacks/` and `Levels/HauntedHouse/AI/`.

1. Choose an attack in the top asset field, or use New Attack. Follow Project Selection switches to selected attack assets.
2. Add frames. Click/drag the timeline to scrub; frame indices start at zero. Shift-click/drag selects an inclusive range.
3. Assign each frame's sprite. Drag sprite sequences onto the Sprite track; add enough frames first. Hold Current Sprite Across Range or bulk Set Sprite handles pose holds.
4. On impact frames, Add Hitbox. Drag the yellow center to move it and edge/corner handles to resize it. Edit damage, reaction, hitstun, hitstop and other hitbox data in the right panel. Cyan shows the reference hurtbox.
5. Tune movement, velocity/gravity, attack/launcher/jump cancel permissions, defense flags and events. Bulk controls apply only to the selected range.
6. Play/step the preview, mirror Facing Left/Right, toggle Onion Skin or Movement Path. Wheel zooms; middle-drag pans; Reset restores the preview view.
7. Save and test in Play Mode. The preview does not simulate actual hits, input buffering, gravity or AI.

Timeline shortcuts: Alt-drag moves the selected range; Ctrl+C/Ctrl+V copies/inserts complete frames; Delete removes the range; arrows step frames; Space plays/pauses. These authoring shortcuts require timeline focus, not an active text field. Undo/Redo is supported.

To make an attack playable, assign it to the relevant character prefab's **Combo Controller** Ground Combo, Launcher, Air Combo or Air Dive, or to an enemy AI attack choice. Creating an AttackData asset alone does not attach it to a character.

Details: `AttackDataEditorGuide.md`, `FrameCombatGuide.md`.

## Attack asset Inspector / Scene View editor

Select an AttackData asset once rather than double-clicking it. This existing inspector edits the same data as the timeline window: attack identity/domain/cooldown, feedback, frame navigation and Add/Insert/Duplicate/Delete operations.

Enable **Scene view preview**, assign **Preview relative to character** if useful, and click **Focus preview in Scene view**. Yellow hitbox center/edge handles edit geometry. Preview Facing Left mirrors the visualization. Expand Movement / Velocity and Defense / Optional Events for those frame settings. Preview Attack plays visuals only.

Use the window for timeline/range work and this Inspector for a quick selected-frame edit. They are two views of the same AttackData asset.

## Enemy AI Editor and profile Inspector

**Open:** Beat Em Up → Enemies → Enemy AI Editor; or select an AI profile and click Open Enemy AI Editor. Examples live in `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/AI/`.

1. Select an existing profile, or duplicate an example with Ctrl+D. Create New AI Profile is also available in the window and under Create → Beat Em Up → Enemy AI Profile.
2. Set detection ranges, target rule, preferred distance, lane tolerance, reaction delay and recovery time.
3. Set Default State to a valid normal state. Add states with unique IDs; select their actions, durations, movement scale and facing behavior.
4. Add transitions with Go To destinations and conditions. Every condition in one transition must pass. The first matching transition wins; arrows change priority. Renaming state IDs requires updating their references.
5. Add Attack Choices with unique IDs and AttackData references. Tune horizontal min/max range, lane tolerance, cooldown, weight and prerequisites. Zero weight disables a choice.
6. Use Validate Profile and Attack Range Preview to inspect configuration. Range Preview does not account for live cooldowns or every prerequisite.
7. Assign the profile to the enemy prefab's **Enemy Combat → Ai Profile**, or use the window's Enemy field and Assign Profile to Selected Enemy outside Play Mode.

AI durations use seconds; AttackData timing uses combat frames. Projectile choices also need EnemyProjectileAttack, a projectile prefab and the attack's ThrowProjectile event. Hurt/Knockdown/GetUp/Dead roles are owned by the combat reaction system.

Details: `EnemyAIEditor.md`.

## Enemy Combat live debug Inspector

In Play Mode, select a spawned enemy. Enemy Combat displays current state, target, selected attack, combat reaction, timers, cooldowns, eligible choices and last transition. Pin it in the AI window using the Enemy field to keep watching it while selecting other assets.

**Force State (safe neutral only)** tests a normal state when the enemy can act. **Refresh AI (reset timers)** cancels its current attack and starts the profile again; use after structural edits. **Print Current AI State** writes to Console. Selected-enemy Scene gizmos show aggro radius and target direction.

Without an assigned profile the enemy uses its legacy attack/range fields. Passive Training Dummy suppresses normal AI decisions.

## Upgrade definitions, pool and Run Upgrade Controller

Select assets under `Assets/EQ_Rung_BeatEmUp/Upgrades/`. Definitions and pools use normal Inspector editing.

- **Run Upgrade:** unique ID, name, description, icon, rarity, tags, max stacks, weight, prerequisites, incompatible upgrades and modifier/amount effects. Create → Beat Em Up → Run Upgrade adds a new definition. Damage fractions such as 0.2 mean +20%; HP/frame modifiers use literal values.
- **Upgrade Pool:** add definitions to HauntedUpgradePool.asset; tune Common/Rare/Epic weights and synergy bias. Create → Beat Em Up → Upgrade Pool creates a separate pool.
- Assign the pool to **Run Upgrade Controller** on Haunted House Stage Flow.
- In the stage editor set **Reward After Clear = UpgradeChoice**. Configure Chapel Spawn Point, Reward Choice Center/Spacing and Reward Interact Radius. Reward Selection Controller holds chapel/card prefab references.

Normal gameplay uses the physical chapel/card interaction: clear, approach chapel and press E/gamepad Select, then approach a card and interact again. Tab shows the current build. A stage reward is granted once per run.

In Play Mode, Run Upgrade Controller's custom inspector offers Force Upgrade Choice, Reroll, Give Specific Upgrade, Clear Current Build, Print Modifiers and Jump to Reward (zero-based stage index). F9 opens offline upgrade debug. Forced debug offers retain separate popup controls; those are not the normal world-card workflow.

Details: `StageRunUpgrades.md`, `WorldChapelRewards.md`.

## Combo Tracker inspector

Select a player/prefab with **Combo Tracker**. Timeout Seconds controls how long a combo can wait between hits; Debug Log enables diagnostics. In Play Mode the custom inspector displays Active, Hits/Damage, Remaining Seconds, Best Hits, Last End Reason and Last Target. **Reset Combo Display** resets tracking.

Combo Tracker measures successful hits. The player's **Combo Controller** is the normal Inspector where you assign the ground/air attack sequences, launcher/dive, defense data and buffering/reset timing. Details: `ComboTracking.md`, `ComboSystem.md`.

## Camera / Stage Framing inspector

Select **Main Camera → Stage Framing**. This uses Unity's normal Inspector, not a separate window.

- Follow Enabled toggles horizontal tracking; Follow Smooth Time controls damping.
- Left/Right Safe Margins control how early follow begins. Larger margins shrink the stationary middle region.
- Orthographic Size and Vertical Center set base composition; airborne settings allow jump framing.
- Show Gizmos displays camera/safe-frame/lane markers in Scene View.
- Stage Left/Right are refreshed by Stage Flow from the art edges. Configure Art Width in the level instead of editing those runtime limits.
- Per-encounter Camera Bounds temporarily constrain this same camera system.

Details: `StageCameraFollow.md`, `StageFraming.md`.

## Defense / reaction asset Inspector

Select `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset`. Its defense Inspector shows an editable **Parry Active Frames** field with a green parry / blue Guard frame track. Edit dodge duration, movement/immunity windows, dodge speed, parry recovery/stun/hitstop/re-arm delay, successful-parry VFX/SFX, knockdown/downed/get-up/death timing, block pose and each reaction's sprite-hold list. In Play mode, the player's Combo Controller Inspector shows live parry/Guard flags, frame and re-arm count. Defaults are 8 active / 6 hitstop / 90 normal-enemy stun frames. Melee parries use the shared enemy Stunned state; notebook parries change projectile ownership and reflect instead. Bosses are exempt from generic stun. Tune each enemy's Stun Animation on Enemy Hit Reaction and deflection values on Combat Projectile. See `Parry.md` and `ParryResponses.md` for exact steps.

Frame windows are zero-based 60 FPS combat frames. Assign the asset to the player's Combo Controller → Defense Data. Create → Beat Em Up → Player Defense and Reactions makes a separate asset. Details: `BlueShirtGuyDefense.md`, `DefenseGrounding.md`.

## Multiplayer tools

Beat Em Up → Multiplayer → Open Main Menu opens MainMenu.unity. Press Play and choose Single Player, Local Co-op or Online Co-op. Starting HauntedHouse.unity directly uses the offline workflow.

**Refresh Multiplayer Asset Catalog** updates the Resources catalog, sprite/attack indexing and content hash, and puts MainMenu first in Build Settings. Run it after content changes before making matching host/client builds. Local 2P/4P validation commands are tests, not level editors. Details: `Multiplayer.md`.

## Setup and art commands

These are project-changing utilities, not authoring windows. Use them when preparing or repairing their corresponding system. Several regenerate prefab/scene/controller content, so save your work and review changes when rerunning them.

| Menu under Beat Em Up | What it does |
| --- | --- |
| Edit Punch1 frame data | Selects the existing Punch1 asset for editing. |
| Refresh frame combat setup | Reconnects combat prefab references and removes offensive Animator states now driven by AttackData. |
| Add sample combat walls to demo | Adds sample CombatWall geometry to the demo. |
| Combat → Set up airborne headbutt dive | Configures the airborne dive content/integration. |
| Stages → Create haunted-house playable level | Builds playable level/scene/enemy integration; existing level data is reused, but generated content can be rewritten. |
| Stages → Add outdoor safe hub first | Configures the outdoor hub and its placement in stage order. |
| Upgrades → Set up stage run upgrades | Creates missing upgrade assets and connects reward components. |
| Upgrades → Set up world chapel rewards | Creates missing reward prefabs and connects scene references. |
| Enemies → Create missing AI examples and assign enemy prefabs | Creates missing example profiles and fills empty enemy assignments. |
| Enemies → Configure Thrower notebook projectile | Configures projectile assets/references and release integration. |
| Art → Build and validate haunted stage previews | Checks art import/layout and saves generated background/floor preview prefabs. |
| Art → Build and validate haunted enemy previews | Checks sprite/clip imports and rebuilds generated preview controllers/prefabs/scenes. |

Generated art-preview prefabs demonstrate imported visuals. Playable placement and combat timing are authored through the level/AttackData editors.

## Validation tools

Menu commands containing **Validate** are automated checks. Most open a test scene, enter Play Mode, create fixture actors and reset runtime state. Save scene work first and run them outside a gameplay session you want to keep. Inspect Console and the corresponding `Documentation/*ValidationResults.txt`; art builders also write import results under their art folders. Older validation fixtures can depend on earlier sample content; a failure after changing authored stages needs inspection rather than automatically reverting your layout.

The full menu inventory follows below. Select the check matching what you changed: encounter zones for arenas; camera safe-frame follow for camera tuning; ground/air punch for attack sequences; enemy AI/recovery/projectiles for enemy changes; reward checks for upgrades; local co-op checks for multiplayer integration.

## Bundled asset-package inspectors

The imported Cartoon FX package also supplies editors that appear when their assets/components are selected:

| Editor | How to use it |
| --- | --- |
| Cartoon FX Remaster FREE Welcome Screen | Open Tools → Cartoon FX Remaster FREE - Welcome Screen for package links and introductory resources. |
| CFXR Effect | Select an effect component; configure its exposed lifetime, light/camera-shake and effect settings. |
| CFXR Emission By Surface | Select its component; adjust the particle Shape and object scale, and inspect calculated emission density. |
| CFXR Particle Text | Select its component, edit text/font settings and use Update Text. Its inspector reports limitations for prefab instances. |
| CFXR Particle Text Font Asset | Select the font asset to edit it and import/export kerning. |
| CFXR Shader Importer | Select the package shader source/importer; inspect pipeline detection/errors, reimport, view source or export a shader. |
| Kino Bloom | Select a camera/object with Bloom; edit Threshold, Soft Knee, Intensity, Radius, High Quality and Anti Flicker. This is bundled demo content. |
| Unity template Readme Inspector | Select the template Readme asset to view introductory links. It is not a gameplay authoring tool. |

Unity's built-in Sprite Editor, Animation window, Animator, Prefab Mode and Particle System Inspector remain available for imported sprites, locomotion/reaction clips, prefab references and effects. Offensive combat frame timing is controlled by AttackData.

## Exact custom gameplay menu paths

- **Beat Em Up → Add sample combat walls to demo**
- **Beat Em Up → Art → Build and validate haunted enemy previews**
- **Beat Em Up → Art → Build and validate haunted stage previews**
- **Beat Em Up → Combat → Set up airborne headbutt dive**
- **Beat Em Up → Edit Punch1 frame data**
- **Beat Em Up → Enemies → Configure Thrower notebook projectile**
- **Beat Em Up → Enemies → Create missing AI examples and assign enemy prefabs**
- **Beat Em Up → Enemies → Enemy AI Editor**
- **Beat Em Up → Enemies → Validate AI profiles (Play Mode)**
- **Beat Em Up → Multiplayer → Open main menu**
- **Beat Em Up → Multiplayer → Refresh multiplayer asset catalog**
- **Beat Em Up → Multiplayer → Validate local 2P full run (Play Mode)**
- **Beat Em Up → Multiplayer → Validate local 4P full run (Play Mode)**
- **Beat Em Up → Refresh frame combat setup**
- **Beat Em Up → Stages → Add outdoor safe hub first**
- **Beat Em Up → Stages → Create haunted-house playable level**
- **Beat Em Up → Stages → Validate camera safe-frame follow (Play Mode)**
- **Beat Em Up → Stages → Validate combat encounter zones (Play Mode)**
- **Beat Em Up → Stages → Validate full haunted-house flow (Play Mode)**
- **Beat Em Up → Stages → Validate outdoor safe hub (Play Mode)**
- **Beat Em Up → Stages → Validate per-stage art layout (Play Mode)**
- **Beat Em Up → Upgrades → Set up stage run upgrades**
- **Beat Em Up → Upgrades → Set up world chapel rewards**
- **Beat Em Up → Upgrades → Validate chapel world rewards (Play Mode)**
- **Beat Em Up → Upgrades → Validate stage run upgrades (Play Mode)**
- **Beat Em Up → Validate air punch playtest (Play Mode)**
- **Beat Em Up → Validate airborne headbutt dive (Play Mode)**
- **Beat Em Up → Validate combat bounces (Play Mode)**
- **Beat Em Up → Validate combo sound and effects (Play Mode)**
- **Beat Em Up → Validate combo tracking and HUD (Play Mode)**
- **Beat Em Up → Validate defensive sprite grounding (Play Mode)**
- **Beat Em Up → Validate enemy recovery (Play Mode)**
- **Beat Em Up → Validate frame combat (Play Mode)**
- **Beat Em Up → Validate ground punch playtest (Play Mode)**
- **Beat Em Up → Validate launcher pursuit (Play Mode)**
- **Beat Em Up → Validate player defense (Play Mode)**
- **Beat Em Up → Validate player hit blink (Play Mode)**
- **Beat Em Up → Validate stage framing (Play Mode)**
- **Beat Em Up → Validate Thrower and corridor (Play Mode)**
- **Tools → Combat → Attack Data Editor**

## Screamer tuning

The Screamer fires a forward-moving rectangular wave that applies a distinct Stunned status. Edit spawn offset/event in its EnemyProjectileAttack component; edit speed, lifetime, width, full depth, distance limit and Stun Duration Frames in ScreamWaveProjectile.prefab. Set Hit Type to Stun; the default is 90 combat frames (1.5s at 60 FPS). PlayerDefense.asset exposes the five looping Stunned poses and six overhead Stun Vfx frames. Normal Hitstun remains separate. The existing attack and AI editors retain warning/scream/recovery timing, committed facing, sounds and cooldown. See [Screamer.md](Screamer.md) and [PlayerStun.md](PlayerStun.md) for tuning/test steps. **Beat Em Up → Enemies → Validate Screamer (Play Mode)** checks directional behavior and stun recovery; **Configure Screamer defaults** resets its attack/profile/wave tuning. **Beat Em Up → Configure player stun artwork** imports/assigns the body/VFX and reapplies Screamer defaults/catalog.

## Player meter and Shadow Dragon

Select BlueShirtGuy's **PlayerMeter** component to tune capacity, starting bars and hit/parry gains. Its Play Mode Inspector shows the current resource. Select `Skills/ShadowDragon/ShadowDragon.asset` for cost, hand offset, release event and links to cast/projectile tuning. Edit `ShadowDragonCast.asset` in the existing Frame Attack Editor; **SpawnDragon** on frame 12 releases the shot. The projectile prefab exposes speed, lifetime, damage, reaction, piercing, repeat intervals and animation sprites. See [ShadowDragon.md](ShadowDragon.md) for the complete guide and controls: **I / right trigger**.

- **Beat Em Up → Skills → Configure Shadow Dragon defaults** resets this skill's tuning and refreshes the existing catalog.
- **Beat Em Up → Skills → Validate Shadow Dragon (Play Mode)** checks meter, state/input rules, authored release, projectile hits and presentation.

## Grappler tuning

Select GrapplerBruiser.prefab to tune **Hit Count Armor** (6 hits, 60-frame break stagger, 300-frame recovery delay) and **Combat Grab Controller** (8-unit/second lunge, 2.5-unit cap, optional acceleration, GrabAnchor, connection/slam damage). Open Grappler_Grab.asset in the existing Frame Attack Editor: telegraph 0–29, direction lock 26, committed XY movement 30–47, grab boxes 31–46, miss recovery 48–89. Edit the per-frame committed grab movement scale, dedicated grab volumes and frame events to change timing. Grappler_HoldSlam.asset holds for 30 frames and throws/releases on frame 30. PlayerDefense.asset exposes the marked temporary Grabbed pose. The live AI Inspector shows armor, commit position/direction, lunge travel and capture state. See [Grappler.md](Grappler.md) for all tuning, file changes and exact tests.

- **Beat Em Up → Enemies → Configure Grappler defaults** resets this enemy's tuning and refreshes the multiplayer catalog.
- **Beat Em Up → Enemies → Validate Grappler (Play Mode)** checks armor, lunge/grab timing, walls, avoidance, attachment, release and independent player control.

## Ambusher tuning

Select Ambusher.prefab's **Combat Grab Controller** for leap speed10, height1.6, distance cap4, duration30 frames, GrabAnchor, Face Attack Count3 and Face Hit damage8. Open Ambusher_LeapGrab.asset in the existing Frame Attack Editor: crouch0–19, target lock17, airborne arc20–49, grab45–49, landing/miss recovery50–79. Ambusher_GrabSuccess.asset uses FaceStrike6 and RepeatFaceAttacks16 to repeat impacts, then ThrowTarget/ReleaseTarget18. The AI profile's Use Ground Plane Range supports diagonal/depth approaches; min/max ranges1.25/4. The live AI Inspector and gizmos show target commitment, arc/destination and face strike count. See [Ambusher.md](Ambusher.md) for all files, art status and exact test steps.

- **Beat Em Up → Enemies → Configure Ambusher defaults** resets tuning and refreshes the multiplayer catalog.
- **Beat Em Up → Enemies → Validate Ambusher (Play Mode)** checks leap commitment, capture/strikes/release, walls, interruption and independent players.

## Prefect tuning

Select Prefect.prefab's EnemyCombat and open the existing **Enemy AI Editor** with that enemy assigned. **Prefect support tuning** exposes preferred/retreat/emergency/minimum-call distances, retreat speed, Rusher count/reference, call/push cooldown choices and Push Knockback. Prefect_CallBackup.asset calls at frame24 after the hand-up/whistle telegraph; Prefect_PushAttack.asset activates6–8 with normal hitstun and outward XY recoil. The Level Editor exposes stage Max Active Enemies12 and encounter Max Active Enemies8; authored waves and backup share caps/tracking. Place Prefect through an encounter spawn list so backup can join that wave. See [Prefect.md](Prefect.md) for states, files, art/feedback status and exact tests.

- **Beat Em Up → Enemies → Configure Prefect defaults** resets tuning and marks the existing Rusher role/catalog.
- **Beat Em Up → Enemies → Validate Prefect (Play Mode)** checks calls/caps/tracking, retreat, pushes, interruptions and AI priorities.
