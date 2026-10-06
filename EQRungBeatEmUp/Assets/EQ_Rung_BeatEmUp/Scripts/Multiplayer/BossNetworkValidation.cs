#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    // Opt-in development harness. Uses the actual session, scene handshake and wire snapshots.
    public sealed class BossNetworkValidation : MonoBehaviour
    {
        MultiplayerSession session;
        readonly List<string> results = new List<string>();
        readonly HashSet<string> seen = new HashSet<string>();
        string output, codeFile, joinCode;
        bool failed, finished, relay, host;
        bool hubOpened,hubChanged,hubRestored,statOpened,statSent;
        int peers = 2, preferredCharacter;
        string lastHubObservation;
        float nextHubLog;
        void Update()
        {
            if(host && session && session.ValidationPhase=="HubStats" && Time.unscaledTime>=nextHubLog)
            {
                nextHubLog=Time.unscaledTime+1;
                Debug.Log("Host Hub stats: "+string.Join(" | ",PlayerRoster.Players.Select(p=>"slot="+p.slot+" station="+p.GetComponent<MetaProgress>().OpenStation+" health="+p.GetComponent<MetaProgress>().profile.healthLevel+" position="+p.transform.position)));
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateSaves()
        {
            if(Environment.GetCommandLineArgs().Contains("--boss-network-test")) MetaSave.DirectoryOverride=Path.Combine(Application.temporaryCachePath,"NetworkHubValidation-"+Guid.NewGuid().ToString("N"));
        }
        void OnEnable() => Application.logMessageReceived += ExceptionLogged;
        void OnDisable() => Application.logMessageReceived -= ExceptionLogged;
        void ExceptionLogged(string message, string stack, LogType type)
        {
            if (type != LogType.Exception || finished) return;
            Check(false, "Unhandled exception: " + message); Finish();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Environment.GetCommandLineArgs().Contains("--boss-network-test"))
                new GameObject("Boss network validation").AddComponent<BossNetworkValidation>();
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var args = Environment.GetCommandLineArgs();
            relay = args.Contains("--relay"); host = args.Contains("--host");
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--output") output = args[i + 1];
                if (args[i] == "--code-file") codeFile = args[i + 1];
                if (args[i] == "--join-code") joinCode = args[i + 1];
                if (args[i] == "--peers") peers = Mathf.Clamp(int.Parse(args[i + 1]), 2, 4);
                if (args[i] == "--character") preferredCharacter = int.Parse(args[i + 1]);
            }
            session = MultiplayerSession.Active;
            Check(session != null, "MainMenu created a session");
            if (!session) { Finish(); yield break; }
            session.PreferredCharacter=preferredCharacter;
            if (host) yield return Host(); else yield return Client();
            Finish();
        }
        void Check(bool condition, string label)
        {
            results.Add((condition ? "PASS: " : "FAIL: ") + label);
            if (!condition) failed = true;
        }
        IEnumerator Wait(Func<bool> condition, string label, float seconds = 60)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Check(condition(), label + (condition() ? "" : " / " + session.Status));
            if (!condition()) Finish();
        }
        IEnumerator Hold(string phase)
        {
            session.ValidationPhase = phase;
            yield return new WaitForSecondsRealtime(1);
        }
        IEnumerator Host()
        {
            if (relay)
            {
                var task = session.HostRelay();
                yield return Wait(() => task.IsCompleted, "Relay allocation request finished");
                if (finished) yield break;
                Check(session.InLobby && !string.IsNullOrEmpty(session.Lobby.code), "Anonymous Authentication and DTLS Relay host created");
                if (!session.InLobby) { results.Add(session.Status); yield break; }
                if (!string.IsNullOrEmpty(codeFile)) { File.WriteAllText(codeFile + ".tmp", session.Lobby.code); File.Move(codeFile + ".tmp", codeFile); }
                Debug.Log("RELAY TEST ROOM CODE: " + session.Lobby.code);
            }
            else session.DirectHost();
            yield return Wait(() => session.Lobby.slots.Count == peers, "All requested players connected");
            if (finished) yield break;
            session.Ready(0);
            yield return Wait(() => session.CanStart, "All players ready");
            if (finished) yield break;
            session.StartGame();
            yield return Wait(() => session.Flow && session.Flow.enabled, "Scene load acknowledgements released simulation");
            if (finished) yield break;
            var flow = session.Flow;
            Check(PlayerRoster.Players.All(p=>p.character==session.Lobby.slots.Find(s=>s.slot==p.slot).character && p.GetComponent<PlayerCharacterLoadout>().character==session.catalog.CharacterAt(p.character)),"Host instantiates each lobby-selected character through shared loadout");
            var hub=flow.GetComponent<PlayerHubController>();
            if(hub && hub.InHub)
            {
                var hubClock=FindFirstObjectByType<CombatClock>(); hubClock.enabled=false;
                foreach(var p in PlayerRoster.Players) p.Motor.ResetForStage(hub.definition.stations[0]);
                session.ValidationPhase="HubCharacter";
                yield return Wait(()=>PlayerRoster.Players.Where(p=>!session.IsLocalOwner(p.owner)).All(p=>p.GetComponent<MetaProgress>().OpenStation==0),"Remote players open physical Hub character station through network input");
                yield return new WaitForSecondsRealtime(2);
                Check(PlayerRoster.Players.All(p=>p.GetComponent<PlayerCharacterLoadout>().character.characterId==p.GetComponent<MetaProgress>().profile.characterId),"Host character loadouts match persistent Hub choices");
                foreach(var p in PlayerRoster.Players) { p.GetComponent<MetaProgress>().OpenStation=-1; p.Motor.ResetForStage(hub.definition.stations[1]); }
                var own=PlayerRoster.Players.First(p=>session.IsLocalOwner(p.owner)); hub.Interact(own.Motor); hub.Execute(own.Motor,HubAction.Stat,0);
                session.ValidationPhase="HubStats";
                yield return Wait(()=>PlayerRoster.Players.Count()==peers && PlayerRoster.Players.All(p=>p.GetComponent<MetaProgress>().profile.healthLevel==1),"Remote players purchase permanent health through validated Hub requests");
                if(finished) yield break;
                Check(PlayerRoster.Players.All(p=>p.Health.EffectiveMaximum==210),"Host applies permanent health to every selected character");
                yield return new WaitForSecondsRealtime(1);
                foreach(var p in PlayerRoster.Players) p.GetComponent<MetaProgress>().OpenStation=-1;
            }
            var areaPlayer=PlayerRoster.Players.FirstOrDefault(p=>p.GetComponent<PlayerSkillController>().equippedSkill.delivery==PlayerSkillDelivery.Area);
            if(areaPlayer)
            {
                var skillClock=FindFirstObjectByType<CombatClock>(); skillClock.enabled=false;
                var skill=areaPlayer.GetComponent<PlayerSkillController>();
                Check(skill.RequestSkill(),"Selected Character 2 casts equipped guardian on host");
                for(int i=0;i<16;i++) skillClock.StepFrame();
                yield return Hold("Character2Guardian");
                for(int i=0;i<8;i++) skillClock.StepFrame();
                yield return Hold("Character2Blast");
                for(int i=0;i<60;i++) skillClock.StepFrame();
                Check(skill.GuardiansManifested==1 && skill.AreaReleases==1,"Selected Character 2 releases one guardian and one area through shared frame events");
            }
            flow.EnterStage(flow.level.stages.FindIndex(s => s.stageId == "Stage07_WhiteGhostBossChamber"));
            var clock = FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            clock.StepFrame(); clock.StepFrame();
            var boss = flow.StageEnemies.Select(h => h.GetComponent<TotemBossController>()).First(b => b);
            var data = Instantiate(boss.data); boss.data = data; data.warpCooldownFrames = 10000; data.moveChance = 0;
            data.vulnerabilityFrames = 420;
            boss.BindEncounter();
            var bossBrain=boss.GetComponent<EnemyCombat>(); var coordinator=bossBrain.coordinator;
            Check(coordinator && coordinator.settings.useAttackCoordination && data.phase1.Concat(data.phase2).All(a=>a.coordination.ignoreCoordinator),"Host boss bypass is independent of encounter attack tokens");
            var coordinatedMinions=new System.Collections.Generic.List<CharacterHealth>();
            foreach(var prefab in new[]{data.rusherPrefab,data.grapplerPrefab,data.throwerPrefab})
                coordinatedMinions.AddRange(flow.RequestBossMinions(bossBrain,prefab,1,new[]{new Vector2(1.7f,.2f),new Vector2(-1.7f,-.2f),new Vector2(.5f,0)},0,.1f));
            Check(coordinatedMinions.Count==3 && coordinatedMinions.All(h=>h.GetComponent<EnemyCombat>().coordinator==coordinator),"Host boss Rusher/Grappler/Thrower minions inherit encounter coordinator");
            foreach (var p in PlayerRoster.Players) { p.Health.maximumHealth = 10000; p.Health.Restore(); }
            for (int i = 0; i < 90 && boss.WarpsPerformed == 0; i++) clock.StepFrame();
            Check(boss.WarpsPerformed > 0 && boss.Invulnerable && boss.Totems.Count == 4, "Boss warps and starts protected with four Totems");
            var totem = boss.Totems[0]; var motor = boss.GetComponent<CharacterMotor>();
            var attacker = PlayerRoster.Players.First().Motor;
            data.breakWaveRadius = 1.25f; totem.Prop.totemBreakRadius = 1.25f;
            data.totemMode = BossTotemMode.OneShot;
            motor.SnapGrabToGround(boss.ClampPoint(Vector2.zero));
            Break(totem, attacker); clock.StepFrame();
            Check(boss.Invulnerable, "Distant Totem wave does not unlock boss");
            yield return Hold("BossWaveMiss");
            for (int i = 0; i < data.breakWaveFrames + 1; i++) clock.StepFrame();
            Check(boss.Invulnerable, "Boss stays protected throughout the entire missed wave");
            totem.Prop.Respawn(data.totemRespawnHP);
            motor.SnapGrabToGround(boss.ClampPoint(totem.transform.position));
            Check(Vector2.Distance(motor.transform.position, totem.transform.position) < totem.BreakRadius, "Boss fixture is inside authored radial reach");
            Break(totem, attacker);
            for (int i = 0; i < data.breakWaveFrames && boss.Invulnerable; i++) clock.StepFrame();
            Check(!boss.Invulnerable && boss.VulnerabilityRemaining == data.vulnerabilityFrames, "Physical wave contact opens full configured window (remaining=" + boss.VulnerabilityRemaining + ", distance=" + Vector2.Distance(motor.transform.position, totem.transform.position) + ")");
            yield return Hold("BossWaveHit");
            var health = boss.GetComponent<CharacterHealth>(); health.Damage(health.EffectiveMaximum * .5f);
            Check(boss.Phase2 && boss.PhaseTransitions == 1, "Half HP activates phase two once");
            yield return Hold("BossPhase2");
            bool tokenCapsValid=true;
            for (int i = 0; i < data.vulnerabilityFrames - 1; i++)
            { clock.StepFrame(); foreach(var category in EnemyAttackCoordinator.Categories) tokenCapsValid &= coordinator.Occupied(category)<=coordinator.settings.Capacity(category); }
            Check(tokenCapsValid,"Host-authoritative boss minions respect category caps throughout online combat");
            Check(!boss.Invulnerable && boss.VulnerabilityRemaining == 1, "Vulnerable through penultimate frame");
            yield return Hold("BossWindowLastFrame");
            clock.StepFrame(); Check(boss.Invulnerable, "Host closes vulnerability on exact configured frame");
            yield return Hold("BossProtectedAgain");
            totem.Prop.Respawn(data.totemRespawnHP); motor.SnapGrabToGround(totem.transform.position);
            Break(totem, attacker); clock.StepFrame(); health.Damage(100000);
            Check(boss.State == BossEncounterState.Dead, "Boss death stops authoritative AI");
            foreach (var minion in flow.LivingEnemies.ToArray()) minion.Damage(100000);
            for (int i = 0; i < 5; i++) clock.StepFrame();
            flow.Tick(.1f);
            Check(flow.EncountersComplete && flow.ExitUnlocked && !flow.ActiveCameraBounds.HasValue, "Defeated boss and remaining minions unlock exit and camera");
            yield return Hold("BossDead");
            Check(!FindObjectsByType<TotemBreakWave>(FindObjectsSortMode.None).Any(), "Death cleans up host waves");
            Destroy(data);
            yield return new WaitForSecondsRealtime(1);
        }
        static void Break(BossTotem totem, CharacterMotor attacker) =>
            totem.Prop.Receive(new AttackHitboxData { damage = 100000, laneTolerance = 100 }, 1, attacker);
        IEnumerator Client()
        {
            if (relay)
            {
                if (string.IsNullOrEmpty(joinCode) && !string.IsNullOrEmpty(codeFile))
                {
                    yield return Wait(() => File.Exists(codeFile) && !string.IsNullOrWhiteSpace(File.ReadAllText(codeFile)), "Host published a Relay code");
                    if (finished) yield break;
                    joinCode = File.ReadAllText(codeFile).Trim();
                }
                var task = session.JoinRelay(joinCode);
                yield return Wait(() => task.IsCompleted, "Relay join request finished");
            }
            else session.DirectJoin();
            if (finished) yield break;
            yield return Wait(() => session.InLobby, "Client entered network lobby");
            if (finished) yield break;
            yield return Wait(() => session.Lobby.slots.Any(s => session.IsLocalOwner(s.owner) && s.character==preferredCharacter), "Host accepted owned character selection");
            if (finished) yield break;
            session.Ready(session.Lobby.slots.First(s => session.IsLocalOwner(s.owner)).slot);
            yield return Wait(() => session.Latest != null, "Client received world snapshot");
            if (finished) yield break;
            Check(!session.IsAuthority && !FindObjectsByType<TotemBossController>(FindObjectsSortMode.None).Any(), "Client has no authoritative boss AI");
            Check(session.Latest.players.Any(p=>session.IsLocalOwner(p.owner) && p.character==preferredCharacter),"Client snapshot preserves selected character identity");
            float until = Time.realtimeSinceStartup + 80;
            while (session.InGame && Time.realtimeSinceStartup < until)
            {
                var world = session.Latest;
                var owned=world?.players.FirstOrDefault(p=>session.IsLocalOwner(p.owner));
                if(owned!=null && world.validationPhase?.StartsWith("Hub")==true)
                {
                    string observation=world.validationPhase+"/station="+owned.hubStation+"/character="+owned.character+"/healthLevel="+owned.meta?.healthLevel+"/position="+owned.position;
                    if(observation!=lastHubObservation) { lastHubObservation=observation; Debug.Log("Hub network test: "+observation); }
                }
                if(world?.validationPhase=="HubCharacter" && owned!=null)
                {
                    if(!hubOpened && owned.hubStation<0) { hubOpened=true; session.SubmitOwnedCommand(new PlayerCommand{buttons=(int)PlayerButtons.Interact,choice=-1}); }
                    if(owned.hubStation==0 && !hubChanged) { hubChanged=true; session.HubCommand(owned.slot,HubAction.Character,(preferredCharacter+1)%2); }
                    if(hubChanged && owned.character!=(preferredCharacter) && !hubRestored) { hubRestored=true; session.HubCommand(owned.slot,HubAction.Character,preferredCharacter); }
                    if(hubRestored && owned.character==preferredCharacter && owned.meta.characterId==session.catalog.CharacterAt(preferredCharacter).characterId) seen.Add("HubCharacter");
                }
                if(world?.validationPhase=="HubStats" && owned!=null)
                {
                    if(owned.hubStation<0 && !statOpened) { statOpened=true; session.SubmitOwnedCommand(new PlayerCommand{buttons=(int)PlayerButtons.Interact,choice=-1}); }
                    if(owned.hubStation==1 && !statSent) { statSent=true; session.HubCommand(owned.slot,HubAction.Stat,0); }
                    if(owned.meta?.healthLevel==1 && owned.maxHp==210) seen.Add("HubStats");
                }
                if(world?.validationPhase=="Character2Guardian" && FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Any(v=>v.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("WandBarrier")))) seen.Add("Character2Guardian");
                if(world?.validationPhase=="Character2Blast" && FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Any(v=>v.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("BarrierPulse")))) seen.Add("Character2Blast");
                var boss = world?.entities.Find(e => e.kind == "Boss");
                if (boss != null && !string.IsNullOrEmpty(world.validationPhase) && seen.Add(world.validationPhase))
                {
                    string phase = world.validationPhase;
                    if (phase == "BossWaveMiss")
                    {
                        Check(boss.invulnerable && world.totemWaves.Count == 1 && session.WaveReplicaCount == 1, "Client sees missed radial wave without vulnerability");
                        var copy = JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(world));
                        var sprite = copy.sprites.FirstOrDefault(s => s.teleportVersion > 0);
                        Check(sprite != null, "Snapshot includes explicit warp version");
                        if (sprite != null)
                        {
                            sprite.position += Vector3.right * .1f; sprite.teleportVersion++;
                            session.ApplySnapshot(copy);
                            var visual = GameObject.Find("Network visual " + sprite.id);
                            Check(visual && Vector3.Distance(visual.transform.position, sprite.position) < .001f, "Short warp snaps instead of interpolating");
                            session.ApplySnapshot(world);
                        }
                    }
                    if (phase == "BossWaveHit") Check(!boss.invulnerable && boss.vulnerabilityFrames == 420 && session.WaveReplicaCount == 1, "Client sees full 420-frame window and cosmetic ring");
                    if (phase == "BossPhase2") Check(boss.phase == 2 && boss.hp <= boss.maxHp * .5f, "Client receives phase two and host HP");
                    if (phase == "BossWindowLastFrame") Check(!boss.invulnerable && boss.vulnerabilityFrames == 1, "Client sees penultimate vulnerability frame");
                    if (phase == "BossProtectedAgain") Check(boss.invulnerable && boss.vulnerabilityFrames == 0 && session.WaveReplicaCount == 0, "Client sees restored shield and wave cleanup");
                    if (phase == "BossDead")
                    {
                        Check(boss.dead && boss.state == "Dead" && session.WaveReplicaCount == 0, "Client receives boss death and cleans up waves");
                        Check(world.exitOpen && !world.cameraLocked, "Client receives unlocked exit and camera");
                        break;
                    }
                }
                yield return null;
            }
            foreach (string phase in new[] { "BossWaveMiss", "BossWaveHit", "BossPhase2", "BossWindowLastFrame", "BossProtectedAgain", "BossDead" }) Check(seen.Contains(phase), "Observed " + phase);
            if(session.Lobby.slots.Any(s=>session.catalog.CharacterAt(s.character)?.skill.delivery==PlayerSkillDelivery.Area))
            {
                Check(seen.Contains("Character2Guardian"),"Client reconstructs original guardian from reliable feedback cue");
                Check(seen.Contains("Character2Blast"),"Client reconstructs radial spirit blast from reliable feedback cue");
            }
            if(session.catalog.level.stages[0].hub)
            {
                Check(seen.Contains("HubCharacter"),"Client changes character twice in Hub and receives saved identity");
                Check(seen.Contains("HubStats"),"Client receives purchased permanent health and updated maximum");
                Check(MetaSave.Load(0,session.catalog.level.stages[0].hub).healthLevel==1,"Client persists its own host-approved upgrade to disk");
            }
        }
        void Finish()
        {
            if (finished) return; finished = true;
            results.Add("BOSS NETWORK " + (failed ? "FAILED" : "PASSED") + " / " + (relay ? "RELAY" : "DIRECT"));
            if (!string.IsNullOrEmpty(output)) File.WriteAllLines(output, results);
            Debug.Log(string.Join("\n", results));
            if (!Application.isEditor) Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif

