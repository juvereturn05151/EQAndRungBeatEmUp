using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    [Serializable]
    public sealed class SceneObjectBinding
    {
        public string id;
        public GameObject target;
    }

    public sealed partial class StageFlowController
    {
        [Tooltip("Actual scene references for the encounter/wave lists in the level asset. Assigned by the encounter editor; save both scene and asset.")]
        public List<SceneObjectBinding> sceneObjectBindings = new List<SceneObjectBinding>();

        public GameObject ResolveSceneObject(string id) => string.IsNullOrEmpty(id) ? null :
            sceneObjectBindings.FirstOrDefault(b => b.id == id)?.target;

        public void ApplySceneObjectStates(IEnumerable<SceneObjectState> states, Dictionary<GameObject, bool> originals = null)
        {
            if (states == null) return;
            foreach (var state in states)
            {
                if (state == null || !state.apply) continue;
                var target = ResolveSceneObject(state.bindingId);
                if (!target) continue;
                if (originals != null && !originals.ContainsKey(target)) originals.Add(target, target.activeSelf);
                target.SetActive(state.active);
            }
        }

        public static void RestoreSceneObjectStates(Dictionary<GameObject, bool> originals)
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.SetActive(pair.Value);
            originals.Clear();
        }

        void BeginEncounterObjects(EncounterState encounter)
        {
            // Capture before the encounter, including objects first touched by later waves.
            foreach (var state in encounter.definition.sceneObjectStates.Concat(encounter.definition.waves.SelectMany(w => w.sceneObjectStates)))
            {
                if (state == null || !state.apply) continue;
                var target = ResolveSceneObject(state.bindingId);
                if (target && !encounter.objectOriginals.ContainsKey(target)) encounter.objectOriginals.Add(target, target.activeSelf);
            }
            ApplySceneObjectStates(encounter.definition.sceneObjectStates, encounter.objectOriginals);
        }

        void EndEncounterObjects(EncounterState encounter)
        {
            if (encounter.definition.restoreSceneObjectsOnEncounterEnd) RestoreSceneObjectStates(encounter.objectOriginals);
            else encounter.objectOriginals.Clear();
        }

        void RestoreActiveEncounterObjects()
        {
            foreach (var encounter in encounters) EndEncounterObjects(encounter);
        }

        static bool EncounterSatisfied(EncounterState encounter) =>
            (!encounter.definition.enabled && !encounter.definition.disabledBlocksProgression) || encounter.completed || encounter.clearedOnce;
        static bool EncounterDependencySatisfied(EncounterState encounter) =>
            (!encounter.definition.enabled && !encounter.definition.disabledBlocksProgression) || encounter.completed;

        bool BossRequirementSkipped
        {
            get
            {
                var plans = encounters.SelectMany(e => e.waves.SelectMany(w => w.definition.enemySpawns
                    .Where(s => s.isBoss || s.prefab && s.prefab.GetComponent<TotemBossController>())
                    .Select(s => new { encounter = e.definition, wave = w.definition }))).ToArray();
                return plans.Length > 0 && plans.All(p => !p.wave.enabled || !p.encounter.enabled && !p.encounter.disabledBlocksProgression);
            }
        }
    }
}
