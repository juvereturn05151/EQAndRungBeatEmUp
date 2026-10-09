using System;
using UnityEngine;

namespace BeatEmUp
{
    public enum NextAreaTarget { StageExit, EncounterEntry }
    public enum NextAreaShowAfter { Immediately, EncounterComplete, SequenceComplete, ControlledByScript }

    [Serializable]
    public sealed class NextAreaMarkerDefinition
    {
        public string markerId = "Next area";
        public bool enabled = true;
        public GameObject markerPrefab;
        public NextAreaTarget target;
        [Tooltip("Existing PlayerZone encounter to walk into; this does not create a trigger.")]
        public string targetEncounterId;
        public bool useTransitionPosition = true;
        public Vector3 exitPosition;
        public Vector3 markerOffset;
        public NextAreaShowAfter showAfter = NextAreaShowAfter.EncounterComplete;
        [Tooltip("Existing encounter ID whose completion reveals this marker.")]
        public string afterEncounterId;
        public bool showEdgeArrow = true;
        public bool markerPreview = true;
        [Min(.1f)] public float nearDistance = 1.5f;

        public bool TryTransitionPosition(StageSegmentDefinition stage, out Vector3 position)
        {
            position = stage.playerExitPoint;
            if (target == NextAreaTarget.StageExit) return true;
            var encounter = stage.encounters.Find(e => e.encounterId == targetEncounterId);
            if (encounter == null || !encounter.enabled || encounter.trigger != EncounterTrigger.PlayerZone) return false;
            position = encounter.triggerZone.center;
            return true;
        }

        public Vector3 Position(StageSegmentDefinition stage)
        {
            TryTransitionPosition(stage, out var transition);
            return (useTransitionPosition ? transition : exitPosition) + markerOffset;
        }
    }
}
