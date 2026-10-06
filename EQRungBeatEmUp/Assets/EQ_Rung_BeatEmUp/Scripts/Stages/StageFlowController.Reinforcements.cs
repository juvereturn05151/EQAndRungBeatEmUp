using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class StageFlowController
    {
        int EncounterCount(EncounterState encounter)=>encounter.waves.Sum(w=>w.enemies.Count(h=>h && h.gameObject.activeInHierarchy && !h.IsDead));
        int AvailableSlots(EncounterState encounter)=>Mathf.Max(0,Mathf.Min(Mathf.Max(1,encounter.definition.maxActiveEnemies)-EncounterCount(encounter),Mathf.Max(1,CurrentStage.maxActiveEnemies)-LivingEnemies.Count()));
        EncounterState OwnerEncounter(EnemyCombat owner)=>owner ? encounters.FirstOrDefault(e=>e.started&&!e.completed&&e.waves.Any(w=>w.enemies.Contains(owner.reaction.health))) : null;
        public int ReinforcementSlots(EnemyCombat owner){var e=OwnerEncounter(owner);return HasAuthority&&e!=null&&!CurrentStage.IsSafeStage ? AvailableSlots(e) : 0;}
        public int EncounterEnemyCount(EnemyCombat owner){var e=OwnerEncounter(owner);return e!=null ? EncounterCount(e) : 0;}
        public int EncounterRusherCount(EnemyCombat owner){var e=OwnerEncounter(owner);return e!=null ? e.waves.SelectMany(w=>w.enemies).Count(h=>h&&!h.IsDead&&h.gameObject.activeInHierarchy&&h.GetComponent<EnemyCombat>()?.role==EnemyRole.Rusher) : 0;}
        CharacterHealth SpawnTrackedEnemy(GameObject prefab,Vector2 point,WaveState wave,EncounterState encounter,bool isBoss=false)
        {
            GetEncounterMovement(encounter.definition,out var minimum,out var maximum,true);
            var go=Instantiate(prefab,point,Quaternion.identity,room.transform);
            var motor=go.GetComponent<CharacterMotor>();motor.arenaMin=minimum;motor.arenaMax=maximum;
            var combat=go.GetComponent<EnemyCombat>();if(combat){combat.target=player.transform;combat.passiveTrainingDummy=false;combat.coordinator=encounter.coordinator;}
            var support=go.GetComponent<PrefectSupport>();if(support)support.flow=this;
            var boss=go.GetComponent<TotemBossController>();if(isBoss&&!boss)boss=go.AddComponent<TotemBossController>();
            if(boss){boss.flow=this;bossSpawned=true;}
            var health=go.GetComponent<CharacterHealth>();wave.enemies.Add(health);if(boss)boss.BindEncounter();return health;
        }
        public IEnumerable<Vector2> BackupSpawnCandidates(EnemyCombat owner)
        {
            var e=OwnerEncounter(owner);if(e==null)yield break;
            GetEncounterMovement(e.definition,out var min,out var max,true);
            foreach(var point in e.definition.waves.SelectMany(w=>w.enemySpawns).SelectMany(s=>s.spawnPoints).Distinct().OrderByDescending(p=>Vector2.Distance(owner.transform.position,p)))
                yield return new Vector2(Mathf.Clamp(point.x,min.x+.3f,max.x-.3f),Mathf.Clamp(point.y,min.y+.2f,max.y-.2f));
            for(int side=0;side<2;side++)for(int lane=0;lane<5;lane++)yield return new Vector2(side==0?min.x+.3f:max.x-.3f,Mathf.Lerp(min.y+.2f,max.y-.2f,lane/4f));
        }
        public int RequestRusherBackup(EnemyCombat owner,GameObject rusher,int count,float playerClearance=1.25f,float enemyClearance=.75f)
        {
            var prefabBrain = rusher ? rusher.GetComponent<EnemyCombat>() : null;
            if (!prefabBrain || prefabBrain.role != EnemyRole.Rusher) return 0;
            return RequestBossMinions(owner, rusher, count, null, playerClearance, enemyClearance).Count;
        }
        // Boss summons share tracked wave ownership, caps, clearance and wall checks with support reinforcements.
        public List<CharacterHealth> RequestBossMinions(EnemyCombat owner, GameObject prefab, int count, IEnumerable<Vector2> authoredPoints, float playerClearance, float enemyClearance)
        {
            var spawned = new List<CharacterHealth>();
            if (!HasAuthority || CombatClock.IsPaused || !owner || owner.reaction.health.IsDead || !prefab || count <= 0 || CurrentStage == null || CurrentStage.IsSafeStage) return spawned;
            var e = OwnerEncounter(owner); var shape = prefab.GetComponent<CharacterMotor>();
            if (e == null || !shape || !prefab.GetComponent<EnemyCombat>() || !prefab.GetComponent<CharacterHealth>()) return spawned;
            var wave = e.waves.FirstOrDefault(w => w.started && !w.completed && w.enemies.Contains(owner.reaction.health)); if (wave == null) return spawned;
            GetEncounterMovement(e.definition, out var min, out var max, true);
            var authored = authoredPoints?.ToArray() ?? new Vector2[0];
            foreach (var raw in authored.Length > 0 ? authored : BackupSpawnCandidates(owner))
            {
                if (spawned.Count >= count || AvailableSlots(e) <= 0) break;
                var half = shape.wallCollisionSize * .5f;
                var point = new Vector2(Mathf.Clamp(raw.x, min.x + half.x, max.x - half.x), Mathf.Clamp(raw.y, min.y + half.y, max.y - half.y));
                if (LivingPlayers.Any(p => Vector2.Distance(p.transform.position, point) < playerClearance) || LivingEnemies.Any(h => Vector2.Distance(h.transform.position, point) < enemyClearance)) continue;
                if (Physics2D.OverlapBoxAll(point + shape.wallCollisionOffset, shape.wallCollisionSize, 0, shape.wallCollisionMask).Any(c => !c.isTrigger && c.GetComponentInParent<CombatWall>()?.isActiveAndEnabled == true)) continue;
                spawned.Add(SpawnTrackedEnemy(prefab, point, wave, e)); Physics2D.SyncTransforms();
            }
            return spawned;
        }
    }
}
