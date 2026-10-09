using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    // Presentation only: no collider, player input, teleport, or progression state.
    [DefaultExecutionOrder(200)]
    public sealed class StageExitMarker : MonoBehaviour
    {
        public SpriteRenderer arrow, ground, nextLabel;
        public Sprite[] arrowFrames;
        [Min(1)] public int frameHold = 8;
        [Min(0)] public float bounceHeight = .04f;
        public NextAreaMarkerDefinition Definition { get; private set; }
        public bool Visible { get; private set; }
        public float Proximity { get; private set; }
        public Vector3 GuidancePosition => transform.position + Vector3.up * .75f;
        StageFlowController flow;

        void Awake() => SetVisible(false);
        public void Configure(StageFlowController owner, NextAreaMarkerDefinition definition)
        {
            flow = owner; Definition = definition; transform.position = definition.Position(owner.CurrentStage); SetVisible(false);
        }
        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (arrow) arrow.enabled = visible;
            if (ground) ground.enabled = visible;
            if (nextLabel) nextLabel.enabled = visible;
        }
        void LateUpdate() => RefreshVisual();
        public void RefreshVisual()
        {
            if (!flow || Definition == null || flow.CurrentStage == null) { SetVisible(false); return; }
            transform.position = Definition.Position(flow.CurrentStage);
            SetVisible(flow.ActiveNextArea == Definition);
            if (!Visible) return;
            float distance = flow.LivingPlayers.Select(p => Vector2.Distance(p.transform.position, transform.position)).DefaultIfEmpty(float.PositiveInfinity).Min();
            Proximity = 1 - Mathf.Clamp01(distance / Mathf.Max(.1f, Definition.nearDistance));
            float phase = CombatClock.CurrentTick * CombatClock.FrameSeconds * 5;
            float pulse = .5f + .5f * Mathf.Sin(phase);
            if (arrowFrames != null && arrowFrames.Length > 0) arrow.sprite = arrowFrames[(int)(CombatClock.CurrentTick / Mathf.Max(1, frameHold) % arrowFrames.Length)];
            arrow.transform.localPosition = new Vector3(0, .75f + Mathf.Round(Mathf.Sin(phase) * bounceHeight * (1 + Proximity) * 100) / 100, 0);
            arrow.color = Color.Lerp(new Color(.78f,.74f,.55f), Color.white, .3f + pulse * .4f + Proximity * .3f);
            ground.color = new Color(1,1,1,.55f + pulse * .15f + Proximity * .3f);
            nextLabel.color = arrow.color;
        }
        void OnDisable() => SetVisible(false);

        // Camera viewport/pixel rect includes letterboxing. GUI coordinates have downward Y.
        public static bool TryEdgePosition(Camera view, Vector3 world, out Vector2 point, out float rotation)
        {
            var viewport = view.WorldToViewportPoint(world);
            point = default; rotation = 0;
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) return false;
            var pixels = view.pixelRect;
            var center = new Vector2(pixels.center.x, Screen.height - pixels.center.y);
            var direction = new Vector2((viewport.x - .5f) * pixels.width, (.5f - viewport.y) * pixels.height);
            if (viewport.z < 0) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.right;
            float width = Mathf.Max(1, pixels.width * .5f - 42), height = Mathf.Max(1, pixels.height * .5f - 56);
            float factor = Mathf.Min(width / Mathf.Max(.0001f, Mathf.Abs(direction.x)), height / Mathf.Max(.0001f, Mathf.Abs(direction.y)));
            point = center + direction * factor;
            rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
            return true;
        }
    }
}
