using System;
using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// All authoring commands act on the runtime asset. Copies exist only for Undo-safe operations/clipboard.
public static class AttackFrameAuthoring
{
    private const string ClipboardPrefix = "BeatEmUp.AttackFrames.v1:";
    [Serializable] private sealed class ClipboardFrames { public List<ClipboardFrame> frames = new List<ClipboardFrame>(); }
    [Serializable] private sealed class ClipboardFrame { public AttackFrameData data; public string spriteGuid; public long spriteId; }
    public static AttackFrameData Clone(AttackFrameData frame) => JsonUtility.FromJson<AttackFrameData>(JsonUtility.ToJson(frame ?? new AttackFrameData()));
    public static AttackHitboxData CloneBox(AttackHitboxData box) => JsonUtility.FromJson<AttackHitboxData>(JsonUtility.ToJson(box ?? new AttackHitboxData()));
    public static void Change(AttackData attack, string label, Action operation)
    {
        if (!attack) return;
        Undo.RegisterCompleteObjectUndo(attack, label);
        operation(); EditorUtility.SetDirty(attack);
    }
    public static void Range(AttackData attack, int first, int last, string label, Action<AttackFrameData> edit)
    {
        if (!attack || attack.frames.Count == 0) return;
        first = Mathf.Clamp(first, 0, attack.frames.Count - 1); last = Mathf.Clamp(last, first, attack.frames.Count - 1);
        Change(attack, label, () => { for (int i = first; i <= last; i++) { if (attack.frames[i] == null) attack.frames[i] = new AttackFrameData(); edit(attack.frames[i]); } });
    }
    public static int Add(AttackData attack)
    {
        int index = attack.TotalFrames; Change(attack, "Add combat frame", () => attack.frames.Add(new AttackFrameData())); return index;
    }
    public static int Insert(AttackData attack, int index)
    {
        index = Mathf.Clamp(index, 0, attack.TotalFrames); int at = index;
        Change(attack, "Insert combat frame", () => attack.frames.Insert(at, new AttackFrameData())); return at;
    }
    public static int Duplicate(AttackData attack, int first, int last)
    {
        if (!attack || attack.TotalFrames == 0) return 0;
        first = Mathf.Clamp(first, 0, attack.TotalFrames - 1); last = Mathf.Clamp(last, first, attack.TotalFrames - 1);
        var copies = attack.frames.Skip(first).Take(last - first + 1).Select(Clone).ToList(); int at = last + 1;
        Change(attack, "Duplicate combat frames", () => attack.frames.InsertRange(at, copies)); return at;
    }
    public static int DuplicatePrevious(AttackData attack, int index)
    {
        if (!attack || attack.TotalFrames == 0 || index <= 0) return 0;
        index = Mathf.Clamp(index, 1, attack.TotalFrames); var copy = Clone(attack.frames[index - 1]); int at = index;
        Change(attack, "Duplicate previous combat frame", () => attack.frames.Insert(at, copy)); return at;
    }
    public static int Delete(AttackData attack, int first, int last)
    {
        if (!attack || attack.TotalFrames == 0) return 0;
        first = Mathf.Clamp(first, 0, attack.TotalFrames - 1); last = Mathf.Clamp(last, first, attack.TotalFrames - 1);
        int at = first, count = last - first + 1;
        Change(attack, "Delete combat frames", () => attack.frames.RemoveRange(at, count)); return Mathf.Min(at, Mathf.Max(0, attack.TotalFrames - 1));
    }
    // Destination is an insertion boundary in the original timeline, not a replacement index.
    public static int Move(AttackData attack, int first, int last, int destination)
    {
        if (!attack || attack.TotalFrames == 0) return 0;
        first = Mathf.Clamp(first, 0, attack.TotalFrames - 1); last = Mathf.Clamp(last, first, attack.TotalFrames - 1);
        destination = Mathf.Clamp(destination, 0, attack.TotalFrames);
        if (destination >= first && destination <= last + 1) return first;
        int count = last - first + 1, source = first, at = destination > last ? destination - count : destination;
        var moved = attack.frames.GetRange(first, count);
        Change(attack, "Move combat frames", () => { attack.frames.RemoveRange(source, count); attack.frames.InsertRange(at, moved); }); return at;
    }
    public static void Copy(AttackData attack, int first, int last)
    {
        if (!attack || attack.TotalFrames == 0) return;
        var clipboard = new ClipboardFrames();
        for (int i = Mathf.Max(0, first); i <= Mathf.Min(last, attack.TotalFrames - 1); i++)
        {
            var item = new ClipboardFrame { data = Clone(attack.frames[i]) };
            if (item.data.sprite) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.data.sprite, out item.spriteGuid, out item.spriteId);
            clipboard.frames.Add(item);
        }
        EditorGUIUtility.systemCopyBuffer = ClipboardPrefix + JsonUtility.ToJson(clipboard);
    }
    public static bool CanPaste => EditorGUIUtility.systemCopyBuffer.StartsWith(ClipboardPrefix, StringComparison.Ordinal);
    public static int Paste(AttackData attack, int index)
    {
        if (!attack || !CanPaste) return 0;
        ClipboardFrames clipboard;
        try { clipboard = JsonUtility.FromJson<ClipboardFrames>(EditorGUIUtility.systemCopyBuffer.Substring(ClipboardPrefix.Length)); }
        catch (ArgumentException) { return 0; }
        if (clipboard?.frames == null || clipboard.frames.Count == 0) return 0;
        var copies = new List<AttackFrameData>();
        foreach (var item in clipboard.frames)
        {
            var frame = Clone(item.data);
            if (!string.IsNullOrEmpty(item.spriteGuid))
            {
                frame.sprite = null;
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(item.spriteGuid)).OfType<Sprite>())
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id) && id == item.spriteId) { frame.sprite = sprite; break; }
            }
            copies.Add(frame);
        }
        int at = Mathf.Clamp(index, 0, attack.TotalFrames);
        Change(attack, "Paste combat frames", () => attack.frames.InsertRange(at, copies)); return copies.Count;
    }
    public static void SetSprite(AttackData attack, int first, int last, Sprite sprite) => Range(attack, first, last, "Assign sprites to combat frames", f => f.sprite = sprite);
    public static void SetHitboxes(AttackData attack, int first, int last, bool enabled, AttackHitboxData prototype)
    {
        Range(attack, first, last, enabled ? "Enable frame hitboxes" : "Clear frame hitboxes", f =>
        {
            if (f.hitboxes == null) f.hitboxes = new List<AttackHitboxData>();
            if (!enabled) f.hitboxes.Clear(); else if (f.hitboxes.Count == 0) f.hitboxes.Add(CloneBox(prototype));
        });
    }
    public static void SetMovement(AttackData attack, int first, int last, Vector2 movement) => Range(attack, first, last, "Set frame movement", f => f.movement = movement);
    public static void SetCancels(AttackData attack, int first, int last, bool normal, bool launcher, bool jump) => Range(attack, first, last, "Set frame cancels", f => { f.canCancelIntoAttack = normal; f.canCancelIntoLauncher = launcher; f.canCancelIntoJump = jump; });
    public static void Save(AttackData attack) { if (attack) AssetDatabase.SaveAssetIfDirty(attack); }
}
