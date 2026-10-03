using System;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public sealed class AttackPreviewGUI : IDisposable
{
    private PreviewRenderUtility utility;
    private GameObject root;
    private SpriteRenderer currentSprite, previousSprite, nextSprite;
    private Vector2 center = new Vector2(0, .65f);
    private Vector2 startMouse, startCenter;
    private Rect startBox;
    private int draggedBox = -1, handle = -1, undoGroup;
    private bool panning;
    private static readonly Vector2[] handlePoints = { new Vector2(0,0), new Vector2(.5f,0), new Vector2(1,0), new Vector2(1,.5f), new Vector2(1,1), new Vector2(.5f,1), new Vector2(0,1), new Vector2(0,.5f) };
    public void Dispose()
    {
        if (utility != null) utility.Cleanup(); utility = null; root = null;
    }
    public static Vector2 FramePosition(AttackData attack, int index, bool mirror)
    {
        Vector2 result = Vector2.zero;
        for (int i = 0; i <= Mathf.Min(index, attack.TotalFrames - 1); i++) if (attack.frames[i] != null) result += attack.frames[i].movement;
        if (mirror) result.x = -result.x; return result;
    }
    public static Rect BoxRect(AttackHitboxData box, Vector2 origin, bool mirror)
    {
        Vector2 offset = box.offset; if (mirror) offset.x = -offset.x;
        return new Rect(origin + offset - box.size * .5f, box.size);
    }
    public static void SetBoxFromWorldRect(AttackHitboxData box, Rect rect, Vector2 origin, bool mirror)
    {
        box.offset = rect.center - origin; if (mirror) box.offset = new Vector2(-box.offset.x, box.offset.y);
        box.size = new Vector2(Mathf.Max(.01f, rect.width), Mathf.Max(.01f, rect.height));
    }
    public void ResetView() { center = new Vector2(0, .65f); }
    private void Initialize()
    {
        if (utility != null) return;
        utility = new PreviewRenderUtility();
        root = new GameObject("Combat window preview") { hideFlags = HideFlags.HideAndDontSave };
        SpriteRenderer Renderer(string name, int order)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; child.transform.SetParent(root.transform);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sortingOrder = order; return renderer;
        }
        previousSprite = Renderer("Previous ghost", 0); nextSprite = Renderer("Next ghost", 1); currentSprite = Renderer("Current frame", 2);
        utility.AddSingleGO(root); utility.camera.orthographic = true; utility.camera.nearClipPlane = .01f; utility.camera.farClipPlane = 100;
        utility.camera.clearFlags = CameraClearFlags.SolidColor; utility.camera.backgroundColor = new Color(.12f,.14f,.17f);
    }
    public void Draw(Rect rect, AttackData attack, int index, ref int selectedBox, bool mirror, ref float pixelsPerUnit,
        bool onion, bool movementPath, GameObject reference, Action beginEdit)
    {
        GUI.Box(rect, GUIContent.none); if (!attack || index < 0 || index >= attack.TotalFrames || attack.frames[index] == null) return;
        Initialize(); var frame = attack.frames[index]; int facing = mirror ? -1 : 1;
        float scale = pixelsPerUnit;
        Vector2 origin = movementPath ? FramePosition(attack, index, mirror) : Vector2.zero;
        Vector2 Screen(Vector2 world) => rect.center + new Vector2((world.x - center.x) * scale, -(world.y - center.y) * scale);
        Vector2 World(Vector2 screen) => center + new Vector2((screen.x - rect.center.x) / scale, -(screen.y - rect.center.y) / scale);
        Rect ScreenRect(Rect world) { var topLeft = Screen(new Vector2(world.xMin, world.yMax)); return new Rect(topLeft, world.size * scale); }
        if (Event.current.type == EventType.Repaint)
        {
            currentSprite.sprite = frame.sprite; currentSprite.flipX = mirror; currentSprite.transform.localPosition = origin;
            previousSprite.sprite = onion && index > 0 ? attack.frames[index - 1]?.sprite : null;
            nextSprite.sprite = onion && index + 1 < attack.TotalFrames ? attack.frames[index + 1]?.sprite : null;
            previousSprite.color = new Color(.4f,.8f,1,.22f); nextSprite.color = new Color(1,.6f,.4f,.22f);
            previousSprite.flipX = nextSprite.flipX = mirror;
            previousSprite.transform.localPosition = movementPath ? FramePosition(attack, index - 1, mirror) : origin;
            nextSprite.transform.localPosition = movementPath ? FramePosition(attack, index + 1, mirror) : origin;
            utility.BeginPreview(rect, GUIStyle.none); utility.camera.orthographicSize = rect.height / (2 * pixelsPerUnit);
            utility.camera.transform.position = new Vector3(center.x, center.y, -10); utility.camera.transform.rotation = Quaternion.identity;
            utility.Render(); var texture = utility.EndPreview(); GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
        GUI.BeginClip(rect);
        var clipped = rect; clipped.position = Vector2.zero;
        Vector2 Local(Vector2 world) => Screen(world) - rect.position;
        void Wire(Rect world, Color color, float width)
        {
            var box = ScreenRect(world); box.position -= rect.position;
            EditorGUI.DrawRect(new Rect(box.x, box.y, box.width, width), color); EditorGUI.DrawRect(new Rect(box.x, box.yMax - width, box.width, width), color);
            EditorGUI.DrawRect(new Rect(box.x, box.y, width, box.height), color); EditorGUI.DrawRect(new Rect(box.xMax - width, box.y, width, box.height), color);
        }
        var pivot = Local(origin); EditorGUI.DrawRect(new Rect(pivot.x - 9, pivot.y, 18, 1), Color.white); EditorGUI.DrawRect(new Rect(pivot.x, pivot.y - 9, 1, 18), Color.white);
        Vector2 hurtOffset = new Vector2(0,.55f), hurtSize = new Vector2(.55f,1.05f);
        if (reference)
        {
            var collider = reference.GetComponentInChildren<CombatHurtbox>()?.GetComponent<BoxCollider2D>();
            if (collider) { hurtOffset = collider.offset; hurtSize = collider.size; }
        }
        if (mirror) hurtOffset.x = -hurtOffset.x;
        Wire(new Rect(origin + hurtOffset - hurtSize * .5f, hurtSize), Color.cyan, 1);
        if (frame.hitboxes != null)
            for (int b = 0; b < frame.hitboxes.Count; b++)
            {
                var box = frame.hitboxes[b]; if (box == null) continue;
                var world = BoxRect(box, origin, mirror); bool selected = b == selectedBox;
                Wire(world, selected ? Color.yellow : new Color(1,.5f,.1f), selected ? 2 : 1);
                var screen = ScreenRect(world); screen.position -= rect.position;
                GUI.Label(new Rect(screen.x, screen.y - 19, 130, 19), $"Hitbox {b} / ID {box.hitId}", EditorStyles.whiteMiniLabel);
                if (selected)
                {
                    foreach (var point in handlePoints)
                        EditorGUI.DrawRect(new Rect(screen.x + point.x * screen.width - 3, screen.y + point.y * screen.height - 3, 7, 7), Color.yellow);
                    EditorGUI.DrawRect(new Rect(screen.center.x - 4, screen.center.y - 4, 9, 9), Color.yellow);
                }
            }
        if (movementPath)
        {
            Handles.BeginGUI(); Handles.color = new Color(.75f,.4f,1);
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= index; i++) { var at = FramePosition(attack, i, mirror); Handles.DrawLine(Local(previous), Local(at)); previous = at; }
            Handles.EndGUI();
        }
        var forward = frame.movement; forward.x *= facing;
        if (forward != Vector2.zero)
        {
            Handles.BeginGUI(); Handles.color = new Color(.85f,.5f,1); Handles.DrawLine(Local(origin), Local(origin + forward)); Handles.EndGUI();
        }
        GUI.Label(new Rect(8, 6, clipped.width - 16, 22), $"Frame {index} • Facing {(mirror ? "Left" : "Right")}", EditorStyles.whiteLabel);
        GUI.Label(new Rect(8, clipped.height - 44, clipped.width - 16, 40), "Yellow: selected hitbox  Orange: hitboxes\nCyan: hurtbox  White: pivot", EditorStyles.whiteMiniLabel);
        GUI.EndClip();
        var evt = Event.current; int control = GUIUtility.GetControlID("CombatHitboxPreview".GetHashCode(), FocusType.Passive);
        if (evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition)) { pixelsPerUnit = Mathf.Clamp(pixelsPerUnit - evt.delta.y * 8, 30, 500); evt.Use(); }
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && evt.button == 2)
        {
            panning = true; startMouse = evt.mousePosition; startCenter = center; GUIUtility.hotControl = control; evt.Use();
        }
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && evt.button == 0 && frame.hitboxes != null)
        {
            beginEdit(); draggedBox = -1; handle = -1;
            // Selected box handles get priority over overlapping boxes.
            for (int pass = 0; pass < 2 && draggedBox < 0; pass++)
                for (int b = frame.hitboxes.Count - 1; b >= 0 && draggedBox < 0; b--)
                {
                    if ((pass == 0) != (b == selectedBox) || frame.hitboxes[b] == null) continue;
                    var box = ScreenRect(BoxRect(frame.hitboxes[b], origin, mirror));
                    if (b == selectedBox)
                        for (int h = 0; h < handlePoints.Length; h++)
                        {
                            var point = box.position + Vector2.Scale(box.size, handlePoints[h]);
                            if (Vector2.Distance(point, evt.mousePosition) < 9) { draggedBox = b; handle = h; break; }
                        }
                    if (draggedBox < 0 && box.Contains(evt.mousePosition)) draggedBox = b;
                }
            if (draggedBox >= 0)
            {
                selectedBox = draggedBox; startBox = ScreenRect(BoxRect(frame.hitboxes[draggedBox], origin, mirror)); startMouse = evt.mousePosition;
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(attack, handle < 0 ? "Move frame hitbox" : "Resize frame hitbox");
                GUIUtility.hotControl = control; evt.Use();
            }
        }
        if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == control)
        {
            Vector2 delta = evt.mousePosition - startMouse;
            if (panning) center = startCenter + new Vector2(-delta.x / pixelsPerUnit, delta.y / pixelsPerUnit);
            else if (draggedBox >= 0 && draggedBox < frame.hitboxes.Count)
            {
                var box = startBox;
                if (handle < 0) box.position += delta;
                else
                {
                    var point = handlePoints[handle];
                    if (point.x == 0) box.xMin = Mathf.Min(box.xMax - .01f * pixelsPerUnit, startBox.xMin + delta.x);
                    if (point.x == 1) box.xMax = Mathf.Max(box.xMin + .01f * pixelsPerUnit, startBox.xMax + delta.x);
                    if (point.y == 0) box.yMin = Mathf.Min(box.yMax - .01f * pixelsPerUnit, startBox.yMin + delta.y);
                    if (point.y == 1) box.yMax = Mathf.Max(box.yMin + .01f * pixelsPerUnit, startBox.yMax + delta.y);
                }
                Vector2 bottomLeft = World(new Vector2(box.xMin, box.yMax));
                SetBoxFromWorldRect(frame.hitboxes[draggedBox], new Rect(bottomLeft, box.size / pixelsPerUnit), origin, mirror);
                EditorUtility.SetDirty(attack);
            }
            evt.Use();
        }
        if (evt.type == EventType.MouseUp && GUIUtility.hotControl == control)
        {
            if (!panning) Undo.CollapseUndoOperations(undoGroup);
            panning = false; draggedBox = -1; GUIUtility.hotControl = 0; evt.Use();
        }
    }
}
