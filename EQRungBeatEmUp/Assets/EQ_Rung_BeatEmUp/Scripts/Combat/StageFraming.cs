using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    // The demo previously had a bare fixed camera. This is its single framing
    // component; lane Y stays separate from CharacterMotor's airborne Height.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class StageFraming : MonoBehaviour
    {
        public static StageFraming Active { get; private set; }
        [Header("Reference framing (fixed vertical view, expanding horizontal view)")]
        [Tooltip("Authoring reference only. Orthographic size fixes the vertical composition at every output resolution.")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Min(.1f)] public float orthographicSize = 2;
        public float verticalCenter = 1.36f;
        [Range(0, 1)] public float backgroundFraction = .4f;
        [Header("Ground lanes (horizontal motor bounds are preserved)")]
        public float bottomLane = -.4f;
        public float topLane = .65f;
        [Header("Camera tracking")]
        public CharacterMotor player;
        public bool followEnabled = true;
        [Min(0), Tooltip("Seconds of horizontal follow damping. Zero follows immediately.")]
        public float followSmoothTime = .12f;
        [Range(0, .45f), Tooltip("Fraction of the viewport reserved at the left edge before follow starts.")]
        public float leftSafeMargin = .25f;
        [Range(0, .45f), Tooltip("Fraction of the viewport reserved at the right edge before follow starts.")]
        public float rightSafeMargin = .25f;
        // Retained for old serialized scenes; viewport margins replace the old world-space dead zone.
        [HideInInspector] public float horizontalDeadZone = 1;
        [Header("Stage art limits (set automatically by stage flow)")]
        public bool clampToStageBounds;
        public float stageLeft = -3.8f, stageRight = 3.8f;
        [Header("Airborne framing")]
        [Min(0)] public float airborneTopMargin = .12f;
        [Min(0)] public float nearbyActorDistance = 3;
        [Min(0)] public float zoomReturnSpeed = .8f;
        [Header("Existing placeholder floor")]
        public SpriteRenderer floor;
        public bool showGizmos = true;
        public float BaseBottom => verticalCenter - orthographicSize;
        public float BackgroundBoundary => BaseBottom + 2 * orthographicSize * (1 - backgroundFraction);
        private Camera view;
        private float followVelocity;
        private bool ownsViewport;
        private readonly List<CharacterMotor> actors = new List<CharacterMotor>();

        private void OnEnable()
        {
            view = GetComponent<Camera>(); Active = this;
            actors.Clear();
            foreach (var motor in FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) Register(motor);
            ApplyFraming(0, true);
        }
        private void OnDisable()
        {
            if (Active == this) Active = null;
            if (ownsViewport && view) { view.rect = new Rect(0, 0, 1, 1); view.ResetAspect(); ownsViewport = false; }
            followVelocity = 0;
        }
        private void OnValidate()
        {
            orthographicSize = Mathf.Max(.1f, orthographicSize);
            topLane = Mathf.Max(bottomLane, topLane);
            followSmoothTime = Mathf.Max(0, followSmoothTime);
            leftSafeMargin = Mathf.Clamp(leftSafeMargin, 0, .45f);
            rightSafeMargin = Mathf.Clamp(rightSafeMargin, 0, .45f);
            view = GetComponent<Camera>(); ApplyFraming(0, true);
            if (Application.isPlaying) foreach (var actor in actors) if (actor) ApplyLaneBounds(actor);
        }
        public void Register(CharacterMotor motor)
        {
            if (!motor || motor.gameObject.scene != gameObject.scene) return;
            if (!actors.Contains(motor)) actors.Add(motor);
            if (Application.isPlaying) ApplyLaneBounds(motor);
        }
        public void ApplyLaneBounds(CharacterMotor motor)
        {
            motor.arenaMin = new Vector2(motor.arenaMin.x, bottomLane);
            motor.arenaMax = new Vector2(motor.arenaMax.x, topLane);
        }
        private void LateUpdate() => ApplyFraming(Time.deltaTime, !Application.isPlaying);
        public void SetStageBounds(float left, float right)
        {
            stageLeft = Mathf.Min(left, right); stageRight = Mathf.Max(left, right);
            clampToStageBounds = true;
        }
        public void ApplyFraming(float seconds, bool reset = false)
        {
            if (!view) view = GetComponent<Camera>();
            if (!view) return;
            view.orthographic = true;
            float size = orthographicSize;
            float x = transform.position.x;
            if (Application.isPlaying && player)
            {
                // Expand upwards only when nearby airborne art needs the room.
                // Keep the lower floor/margin anchored; never change jump physics.
                for (int i = actors.Count - 1; i >= 0; i--)
                {
                    var actor = actors[i];
                    if (!actor) { actors.RemoveAt(i); continue; }
                    if (!actor.isActiveAndEnabled || actor.IsGrounded || !actor.sprite || Mathf.Abs(actor.transform.position.x - player.transform.position.x) > nearbyActorDistance) continue;
                    size = Mathf.Max(size, (actor.sprite.bounds.max.y + airborneTopMargin - BaseBottom) * .5f);
                }
            }
            if (!reset && size < view.orthographicSize) size = Mathf.Max(size, Mathf.MoveTowards(view.orthographicSize, size, zoomReturnSpeed * Mathf.Max(0, seconds)));
            view.orthographicSize = size;
            // A viewport wider than the art cannot be clamped without revealing blank sides.
            // Pillarbox that case instead of changing vertical framing or stretching the art.
            if (clampToStageBounds)
            {
                float fullAspect = view.targetTexture ? (float)view.targetTexture.width / view.targetTexture.height : (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
                float viewportWidth = Mathf.Clamp((stageRight - stageLeft) / Mathf.Max(.0001f, 2 * size * fullAspect), .001f, 1);
                view.rect = new Rect((1 - viewportWidth) * .5f, 0, viewportWidth, 1);
                view.ResetAspect(); ownsViewport = true;
            }
            else if (ownsViewport) { view.rect = new Rect(0, 0, 1, 1); view.ResetAspect(); ownsViewport = false; }
            float halfWidth = size * view.aspect;
            if (reset) followVelocity = 0;
            if (Application.isPlaying && player && followEnabled)
            {
                var bounds = player.sprite ? player.sprite.bounds : new Bounds(player.transform.position, Vector3.zero);
                float minimum = bounds.max.x - halfWidth + 2 * halfWidth * Mathf.Clamp(rightSafeMargin, 0, .45f);
                float maximum = bounds.min.x + halfWidth - 2 * halfWidth * Mathf.Clamp(leftSafeMargin, 0, .45f);
                float target = minimum <= maximum ? Mathf.Clamp(x, minimum, maximum) : bounds.center.x;
                target = ClampHorizontal(target, halfWidth);
                if (Mathf.Approximately(x, target)) followVelocity = 0;
                else x = reset || followSmoothTime <= 0 ? target : Mathf.SmoothDamp(x, target, ref followVelocity, followSmoothTime, Mathf.Infinity, Mathf.Max(0, seconds));
                // Smoothing must not let fast movement or teleports leave the actual viewport.
                float visibleMin = bounds.max.x - halfWidth;
                float visibleMax = bounds.min.x + halfWidth;
                if (visibleMin <= visibleMax) x = Mathf.Clamp(x, visibleMin, visibleMax);
            }
            else followVelocity = 0;
            x = ClampHorizontal(x, halfWidth);
            transform.position = new Vector3(x, BaseBottom + size, transform.position.z);
            if (floor && floor.sprite)
            {
                float height = BackgroundBoundary - BaseBottom;
                float width = Mathf.Max(16, 2 * (Mathf.Abs(x) + size * view.aspect + 1));
                floor.transform.position = new Vector3(0, (BackgroundBoundary + BaseBottom) * .5f, floor.transform.position.z);
                floor.transform.localScale = new Vector3(width / floor.sprite.bounds.size.x, height / floor.sprite.bounds.size.y, 1);
            }
        }
        private float ClampHorizontal(float x, float halfWidth)
        {
            if (!clampToStageBounds) return x;
            float left = stageLeft + halfWidth, right = stageRight - halfWidth;
            return left <= right ? Mathf.Clamp(x, left, right) : (stageLeft + stageRight) * .5f;
        }
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            var camera = GetComponent<Camera>(); if (!camera) return;
            float halfWidth = camera.orthographicSize * camera.aspect;
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + Vector3.forward * 10, new Vector3(halfWidth * 2, camera.orthographicSize * 2, 0));
            float safeLeft = transform.position.x - halfWidth + 2 * halfWidth * leftSafeMargin;
            float safeRight = transform.position.x + halfWidth - 2 * halfWidth * rightSafeMargin;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3((safeLeft + safeRight) * .5f, transform.position.y), new Vector3(safeRight - safeLeft, camera.orthographicSize * 2, 0));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(transform.position.x - halfWidth, BackgroundBoundary), new Vector3(transform.position.x + halfWidth, BackgroundBoundary));
            float left = player ? player.arenaMin.x : -6, right = player ? player.arenaMax.x : 6;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3((left + right) * .5f, (bottomLane + topLane) * .5f), new Vector3(right - left, topLane - bottomLane, 0));
            if (player)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(player.transform.position + Vector3.left * .5f, player.transform.position + Vector3.right * .5f);
            }
        }
    }
}
