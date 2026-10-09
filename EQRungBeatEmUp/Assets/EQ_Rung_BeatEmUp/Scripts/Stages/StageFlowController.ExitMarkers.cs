using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    public sealed partial class StageFlowController
    {
        readonly List<StageExitMarker> exitMarkers = new List<StageExitMarker>();
        readonly HashSet<string> scriptMarkers = new HashSet<string>();
        public IReadOnlyList<StageExitMarker> ExitMarkers => exitMarkers;
        public bool EncounterIsComplete(string id) => encounters.Any(e => e.definition.encounterId == id && EncounterSatisfied(e));

        // A visibility override, never an encounter/transition completion signal.
        public void SetNextAreaMarkerVisible(string id, bool visible)
        {
            if (!HasAuthority) return;
            if (visible) scriptMarkers.Add(id); else scriptMarkers.Remove(id);
            RefreshExitMarkers();
        }

        void CreateExitMarkers()
        {
            exitMarkers.Clear(); scriptMarkers.Clear();
            foreach (var definition in CurrentStage.nextAreaMarkers)
            {
                if (!definition.enabled || !definition.markerPrefab) continue;
                var go = Instantiate(definition.markerPrefab, room.transform);
                var marker = go.GetComponent<StageExitMarker>();
                if (!marker) { Destroy(go); continue; }
                marker.Configure(this, definition); exitMarkers.Add(marker);
            }
            RefreshExitMarkers();
        }

        void HideExitMarkers()
        {
            foreach (var marker in exitMarkers) if (marker) marker.SetVisible(false);
        }

        bool GuidanceAllowed => isActiveAndEnabled && CurrentStage != null && !LevelCompleted &&
            string.IsNullOrEmpty(Failure) && !CombatClock.IsPaused && LivingPlayers.Any() &&
            !encounters.Any(e => e.definition.enabled && e.started && !e.completed) &&
            !(RunUpgrades && RunUpgrades.IsChoosing) && !(WorldRewards && WorldRewards.IsPending) && !(CoopRewards && CoopRewards.Pending);

        bool MarkerEligible(NextAreaMarkerDefinition definition)
        {
            if (!definition.enabled || !definition.markerPrefab || !definition.TryTransitionPosition(CurrentStage, out var transition)) return false;
            if (definition.target == NextAreaTarget.StageExit)
            {
                if (!ExitUnlocked || LivingPlayers.Any(p => Vector2.Distance(p.transform.position, transition) <= CurrentStage.exitRadius)) return false;
            }
            else
            {
                var target = encounters.FirstOrDefault(e => e.definition.encounterId == definition.targetEncounterId);
                // The existing swept trigger latches eligibility even during its authored delay.
                if (target == null || target.started || target.eligibleAt >= 0 || EncounterSatisfied(target)) return false;
            }
            switch (definition.showAfter)
            {
                case NextAreaShowAfter.Immediately: return true;
                case NextAreaShowAfter.EncounterComplete: return EncounterIsComplete(definition.afterEncounterId);
                case NextAreaShowAfter.SequenceComplete: return CompletionSatisfied;
                case NextAreaShowAfter.ControlledByScript: return scriptMarkers.Contains(definition.markerId);
                default: return false;
            }
        }

        public NextAreaMarkerDefinition ActiveNextArea => HasAuthority && GuidanceAllowed
            ? CurrentStage.nextAreaMarkers.FirstOrDefault(MarkerEligible) : null;

        public void RefreshExitMarkers()
        {
            var active = ActiveNextArea;
            foreach (var marker in exitMarkers) if (marker) marker.SetVisible(marker.Definition == active);
        }

        public NextAreaMarkerState CaptureNextAreaMarker()
        {
            var definition = ActiveNextArea;
            var marker = exitMarkers.FirstOrDefault(m => m && m.Definition == definition);
            if (definition == null || !marker) return null;
            var catalog = MultiplayerSession.Active ? MultiplayerSession.Active.catalog : null;
            return new NextAreaMarkerState { id = definition.markerId, position = marker.GuidancePosition,
                showEdgeArrow = definition.showEdgeArrow, arrowSprite = catalog ? catalog.SpriteId(marker.arrowFrames[0]) : -1,
                labelSprite = catalog ? catalog.SpriteId(marker.nextLabel.sprite) : -1 };
        }

        public void DrawNextAreaEdgeArrow(bool respectStageHud = true)
        {
            if ((respectStageHud && !showHud) || CombatClock.IsPaused) return;
            var session = MultiplayerSession.Active;
            if (session && session.LocalMenuOpen) return;
            var view = framing ? framing.GetComponent<Camera>() : Camera.main;
            if (!view) return;
            Sprite arrow = null, label = null; Vector3 position; bool edge;
            if (session && !HasAuthority)
            {
                var state = session.Latest?.nextAreaMarker;
                if (state == null || session.Latest.completed || session.Latest.gameOver || session.Latest.rewardPending) return;
                position = state.position; edge = state.showEdgeArrow;
                arrow = session.catalog.SpriteAt(state.arrowSprite); label = session.catalog.SpriteAt(state.labelSprite);
            }
            else
            {
                var active = ActiveNextArea;
                var marker = exitMarkers.FirstOrDefault(m => m && m.Definition == active);
                if (active == null || !marker) return;
                position = marker.GuidancePosition; edge = active.showEdgeArrow;
                arrow = marker.arrowFrames[0]; label = marker.nextLabel.sprite;
            }
            if (!edge || !arrow || !StageExitMarker.TryEdgePosition(view, position, out var point, out float rotation)) return;
            var oldMatrix = GUI.matrix; var oldColor = GUI.color;
            GUI.color = Color.Lerp(new Color(.8f,.75f,.5f), Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 5));
            GUIUtility.RotateAroundPivot(rotation, point);
            GUI.DrawTexture(new Rect(point.x - 24, point.y - 24, 48, 48), arrow.texture);
            GUI.matrix = oldMatrix;
            if (label) GUI.DrawTexture(new Rect(point.x - 32, point.y + 25, 64, 24), label.texture);
            GUI.color = oldColor;
        }
    }
}
