using System;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public sealed class AttackTimelineGUI
{
    public sealed class Track
    {
        public readonly string name;
        public readonly Color color;
        public readonly Func<AttackFrameData, string> marker;
        public Track(string name, Color color, Func<AttackFrameData, string> marker) { this.name = name; this.color = color; this.marker = marker; }
    }
    // Add tracks by registering another descriptor; no runtime data is duplicated here.
    public readonly List<Track> tracks = new List<Track>
    {
        new Track("Sprite", new Color(.35f,.65f,.95f), f => f.sprite ? f.sprite.name : null),
        new Track("Hitboxes", new Color(1,.65f,.15f), f => f.hitboxes != null && f.hitboxes.Count > 0 ? f.hitboxes.Count.ToString() : null),
        new Track("Grab volumes", new Color(1,.25f,.35f), f => f.grabHitboxes != null && f.grabHitboxes.Count > 0 ? f.grabHitboxes.Count.ToString() : null),
        new Track("Committed grab move", new Color(.9f,.3f,.7f), f => f.grabLungeMovementScale>0 ? f.grabLungeMovementScale.ToString("F2") : null),
        new Track("Movement", new Color(.8f,.45f,.95f), f => f.movement != Vector2.zero ? f.movement.ToString("F2") : null),
        new Track("Velocity / gravity", new Color(.6f,.4f,.85f), f => f.movementInputScale != 1 || f.setHorizontalVelocity || f.setVerticalVelocity || f.verticalVelocityModifier != 0 || f.gravityScale != 1 || f.suspendFalling ? "Motion" : null),
        new Track("Attack cancel", new Color(.35f,.8f,.45f), f => f.canCancelIntoAttack ? "Attack" : null),
        new Track("Launcher cancel", new Color(.2f,.65f,.4f), f => f.canCancelIntoLauncher ? "Launcher" : null),
        new Track("Jump cancel", new Color(.25f,.8f,.85f), f => f.canCancelIntoJump ? "Jump" : null),
        new Track("Invulnerability", new Color(.35f,.75f,1), f => f.invulnerable ? "Invulnerable" : null),
        new Track("Super armor", new Color(1,.5f,.3f), f => f.superArmor ? "Armor" : null),
        new Track("Events", new Color(.9f,.8f,.35f), f => f.events != null && f.events.Count > 0 ? f.events.Count.ToString() : null)
    };
    private bool moving;
    private int dropFrame;
    public float Height => 26 + tracks.Count * 25 + 18;
    public void Draw(Rect rect, AttackData attack, int current, int first, int last, ref Vector2 scroll, ref float zoom,
        Action<int, bool> select, Action<int> move, Action<int, Sprite[]> assignSprites, Action focus)
    {
        const float labels = 125, ruler = 26, row = 25;
        int control = GUIUtility.GetControlID("CombatTimeline".GetHashCode(), FocusType.Passive);
        GUI.Box(rect, GUIContent.none);
        for (int t = 0; t < tracks.Count; t++)
            GUI.Label(new Rect(rect.x + 6, rect.y + ruler + t * row, labels - 8, row), tracks[t].name);
        var viewport = new Rect(rect.x + labels, rect.y, Mathf.Max(40, rect.width - labels), rect.height);
        bool pointerInside = viewport.Contains(Event.current.mousePosition);
        var content = new Rect(0, 0, Mathf.Max(viewport.width - 18, (attack.TotalFrames + 1) * zoom), ruler + tracks.Count * row);
        scroll = GUI.BeginScrollView(viewport, scroll, content, true, false);
        int min = Mathf.Max(0, Mathf.FloorToInt(scroll.x / zoom)), max = Mathf.Min(attack.TotalFrames - 1, Mathf.CeilToInt((scroll.x + viewport.width) / zoom));
        for (int i = min; i <= max; i++)
        {
            float x = i * zoom;
            EditorGUI.DrawRect(new Rect(x, 0, zoom, content.height), i % 5 == 0 ? new Color(.24f,.24f,.24f) : new Color(.19f,.19f,.19f));
            if (i >= first && i <= last) EditorGUI.DrawRect(new Rect(x, 0, zoom, content.height), new Color(.25f,.5f,.8f,.35f));
            if (zoom >= 22 || i % 5 == 0) GUI.Label(new Rect(x + 2, 2, Mathf.Max(22, zoom), 20), i.ToString(), EditorStyles.miniLabel);
            var frame = attack.frames[i]; if (frame == null) continue;
            for (int t = 0; t < tracks.Count; t++)
            {
                var cell = new Rect(x + 2, ruler + t * row + 3, Mathf.Max(2, zoom - 4), row - 6);
                string marker = tracks[t].marker(frame);
                if (marker != null)
                {
                    EditorGUI.DrawRect(cell, tracks[t].color);
                    GUI.Label(cell, new GUIContent(t == 1 || t == 9 ? marker : "", $"Frame {i}: {marker}"), EditorStyles.centeredGreyMiniLabel);
                }
                EditorGUI.DrawRect(new Rect(x, ruler + t * row + row - 1, zoom, 1), new Color(0,0,0,.25f));
            }
        }
        EditorGUI.DrawRect(new Rect(current * zoom + zoom * .5f - 1, 0, 2, content.height), new Color(1,.3f,.25f));
        if (moving) EditorGUI.DrawRect(new Rect(dropFrame * zoom, 0, 3, content.height), Color.yellow);
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && pointerInside && content.Contains(evt.mousePosition))
        {
            focus(); GUIUtility.hotControl = control;
            int index = Mathf.Clamp(Mathf.FloorToInt(evt.mousePosition.x / zoom), 0, Mathf.Max(0, attack.TotalFrames - 1));
            moving = evt.alt && attack.TotalFrames > 0;
            if (!moving || index < first || index > last) select(index, evt.shift);
            dropFrame = index; evt.Use();
        }
        if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == control)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(evt.mousePosition.x / zoom), 0, moving ? attack.TotalFrames : Mathf.Max(0, attack.TotalFrames - 1));
            if (moving) dropFrame = index; else select(index, evt.shift);
            evt.Use();
        }
        if (evt.type == EventType.MouseUp && GUIUtility.hotControl == control)
        {
            if (moving) move(dropFrame);
            moving = false; GUIUtility.hotControl = 0; evt.Use();
        }
        if (evt.type == EventType.ScrollWheel && evt.control && pointerInside)
        {
            zoom = Mathf.Clamp(zoom - evt.delta.y * 2, 10, 64); evt.Use();
        }
        if (pointerInside && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && evt.mousePosition.y >= ruler && evt.mousePosition.y < ruler + row)
        {
            var sprites = new List<Sprite>();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is Sprite sprite) sprites.Add(sprite);
                else if (obj is Texture2D)
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(obj))) if (sub is Sprite loaded) sprites.Add(loaded);
            }
            if (sprites.Count > 0 && evt.mousePosition.x >= 0 && evt.mousePosition.x < attack.TotalFrames * zoom)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag(); assignSprites(Mathf.FloorToInt(evt.mousePosition.x / zoom), sprites.ToArray());
                }
                evt.Use();
            }
        }
        GUI.EndScrollView();
    }
}
