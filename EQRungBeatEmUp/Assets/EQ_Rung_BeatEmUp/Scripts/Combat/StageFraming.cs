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
        [Min(0)] public float horizontalDeadZone = 1;
        [Min(0)] public float airborneTopMargin = .12f;
        [Min(0)] public float nearbyActorDistance = 3;
        [Min(0)] public float zoomReturnSpeed = .8f;
        [Header("Existing placeholder floor")]
        public SpriteRenderer floor;
        public bool showGizmos = true;
        public float BaseBottom => verticalCenter - orthographicSize;
        public float BackgroundBoundary => BaseBottom + 2 * orthographicSize * (1 - backgroundFraction);
        private Camera view;
        private readonly List<CharacterMotor> actors = new List<CharacterMotor>();

        private void OnEnable()
        {
            view = GetComponent<Camera>(); Active = this;
            actors.Clear();
            foreach (var motor in FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) Register(motor);
            ApplyFraming(0, true);
        }
        private void OnDisable() { if (Active == this) Active = null; }
        private void OnValidate()
        {
            orthographicSize = Mathf.Max(.1f, orthographicSize);
            topLane = Mathf.Max(bottomLane, topLane);
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
        public void ApplyFraming(float seconds, bool reset = false)
        {
            if (!view) view = GetComponent<Camera>();
            if (!view) return;
            view.orthographic = true;
            float size = orthographicSize;
            float x = transform.position.x;
            if (Application.isPlaying && player)
            {
                x = Mathf.Clamp(x, player.transform.position.x - horizontalDeadZone, player.transform.position.x + horizontalDeadZone);
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
            transform.position = new Vector3(x, BaseBottom + size, transform.position.z);
            if (floor && floor.sprite)
            {
                float height = BackgroundBoundary - BaseBottom;
                float width = Mathf.Max(16, 2 * (Mathf.Abs(x) + size * view.aspect + 1));
                floor.transform.position = new Vector3(0, (BackgroundBoundary + BaseBottom) * .5f, floor.transform.position.z);
                floor.transform.localScale = new Vector3(width / floor.sprite.bounds.size.x, height / floor.sprite.bounds.size.y, 1);
            }
        }
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            var camera = GetComponent<Camera>(); if (!camera) return;
            float halfWidth = camera.orthographicSize * camera.aspect;
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + Vector3.forward * 10, new Vector3(halfWidth * 2, camera.orthographicSize * 2, 0));
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
