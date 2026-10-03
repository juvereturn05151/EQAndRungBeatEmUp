using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// Isolated Edit Mode checks: no demo rebuild, scene edits, controller edits or runtime input.
public static class AttackEditorValidation
{
    private static readonly List<string> results = new List<string>();
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        results.Add("PASS: " + label);
    }
    public static void Run()
    {
        const string tempPath = "Assets/__AttackEditorValidation.asset";
        string previousClipboard = EditorGUIUtility.systemCopyBuffer;
        results.Clear();
        AttackData asset = null;
        try
        {
            if (AssetDatabase.LoadMainAssetAtPath(tempPath)) throw new Exception("Validation asset already exists; refusing to overwrite it.");
            var source = AssetDatabase.LoadAssetAtPath<AttackData>(CombatDemoBuilder.Output + "/Attacks/Punch1.asset");
            var protectedAssets = Directory.GetFiles(CombatDemoBuilder.Output + "/Attacks", "*.asset").ToDictionary(p => p, File.ReadAllText);
            asset = ScriptableObject.CreateInstance<AttackData>();
            for (int i = 0; i < 6; i++)
                asset.frames.Add(new AttackFrameData { sprite = source.frames[i].sprite, movement = new Vector2(i * .1f, .02f), canCancelIntoAttack = i % 2 == 0, canCancelIntoLauncher = i == 4, canCancelIntoJump = i == 5, invulnerable = i == 2, superArmor = i == 3, events = new List<string> { "Frame " + i }, hitboxes = new List<AttackHitboxData> { new AttackHitboxData { offset = new Vector2(.5f + i * .01f, .55f), damage = i + 10 } } });
            AssetDatabase.CreateAsset(asset, tempPath);
            int original = asset.TotalFrames;
            var copy = AttackFrameAuthoring.Clone(asset.frames[2]);
            Check(copy.sprite == asset.frames[2].sprite && copy.events[0] == "Frame 2", "Clone preserves sprite references and events");
            Check(!ReferenceEquals(copy.hitboxes[0], asset.frames[2].hitboxes[0]), "Clone creates independent hitbox data");
            Undo.IncrementCurrentGroup(); AttackFrameAuthoring.SetMovement(asset, 1, 3, new Vector2(.4f, .1f));
            Check(asset.frames.Skip(1).Take(3).All(f => f.movement == new Vector2(.4f,.1f)) && asset.frames[0].movement.x == 0, "Range edits affect exactly the selected frames");
            Undo.PerformUndo(); Check(Mathf.Approximately(asset.frames[1].movement.x,.1f) && Mathf.Approximately(asset.frames[3].movement.x,.3f), "Undo restores complete range edit");
            Undo.PerformRedo(); Check(asset.frames[2].movement.x == .4f, "Redo restores range edit");
            var sprite = source.frames[5].sprite;
            AttackFrameAuthoring.SetSprite(asset, 0, 2, sprite); Check(asset.frames.Take(3).All(f => f.sprite == sprite), "Hold sprite explicitly assigns every selected combat frame");
            AttackFrameAuthoring.SetCancels(asset, 2, 4, true, true, false);
            Check(asset.frames.Skip(2).Take(3).All(f => f.canCancelIntoAttack && f.canCancelIntoLauncher && !f.canCancelIntoJump), "Bulk cancel permissions remain per-frame data");
            AttackFrameAuthoring.SetHitboxes(asset, 0, 2, false, null); Check(asset.frames.Take(3).All(f => f.hitboxes.Count == 0), "Bulk disable removes selected frame hitboxes");
            var prototype = new AttackHitboxData { damage = 23, size = new Vector2(.8f,.7f) };
            AttackFrameAuthoring.SetHitboxes(asset, 0, 2, true, prototype);
            Check(asset.frames.Take(3).All(f => f.hitboxes[0].damage == 23) && !ReferenceEquals(asset.frames[0].hitboxes[0], asset.frames[1].hitboxes[0]), "Bulk enable creates independent boxes using the chosen prototype");
            AttackFrameAuthoring.SetHitboxes(asset, 0, 2, true, new AttackHitboxData { damage = 99 });
            Check(asset.frames[0].hitboxes[0].damage == 23 && asset.frames[0].hitboxes.Count == 1, "Enabling existing hitboxes preserves authored geometry and avoids duplicate boxes");
            int at = AttackFrameAuthoring.Duplicate(asset, 2, 3);
            Check(at == 4 && asset.TotalFrames == original + 2 && asset.frames[4].events[0] == asset.frames[2].events[0], "Duplicate range inserts complete frames after the range");
            Check(!ReferenceEquals(asset.frames[4].events, asset.frames[2].events) && !ReferenceEquals(asset.frames[4].hitboxes, asset.frames[2].hitboxes), "Duplicated frame nested lists are independent");
            AttackFrameAuthoring.Copy(asset, 2, 3); Check(AttackFrameAuthoring.CanPaste, "Range copy creates a recognized combat-frame clipboard");
            int pasted = AttackFrameAuthoring.Paste(asset, 0);
            Check(pasted == 2 && asset.TotalFrames == original + 4 && asset.frames[0].sprite == asset.frames[4].sprite, "Paste inserts a range and resolves sprites by asset GUID/local ID");
            Check(!ReferenceEquals(asset.frames[0].hitboxes[0], asset.frames[4].hitboxes[0]), "Pasted hitboxes remain independent");
            AttackFrameAuthoring.Delete(asset, 0, 1); AttackFrameAuthoring.Delete(asset, 4, 5);
            Check(asset.TotalFrames == original, "Range delete removes complete frames");
            string first = JsonUtility.ToJson(asset.frames[1]), second = JsonUtility.ToJson(asset.frames[2]);
            int moved = AttackFrameAuthoring.Move(asset, 1, 2, asset.TotalFrames);
            Check(moved == 4 && JsonUtility.ToJson(asset.frames[4]) == first && JsonUtility.ToJson(asset.frames[5]) == second, "Moving frames later preserves every property and ordering");
            moved = AttackFrameAuthoring.Move(asset, 4, 5, 1);
            Check(moved == 1 && JsonUtility.ToJson(asset.frames[1]) == first && JsonUtility.ToJson(asset.frames[2]) == second, "Moving frames earlier restores the complete units");
            string beforeNoOp = JsonUtility.ToJson(asset); AttackFrameAuthoring.Move(asset, 1, 2, 2);
            Check(JsonUtility.ToJson(asset) == beforeNoOp, "Dropping a range inside itself is a safe no-op");
            int inserted = AttackFrameAuthoring.Insert(asset, 3); Check(inserted == 3 && asset.frames[3].sprite == null && asset.frames[3].gravityScale == 1, "Insert creates a clean runtime frame with proper defaults");
            AttackFrameAuthoring.DuplicatePrevious(asset, 4); Check(asset.frames[4].sprite == null && asset.frames[4].hitboxes.Count == 0, "Duplicate Previous copies the actual preceding frame");
            int added = AttackFrameAuthoring.Add(asset); Check(added == asset.TotalFrames - 1 && asset.frames[added].hitboxes.Count == 0, "Add appends an empty frame");
            var timeline = new AttackTimelineGUI(); Check(timeline.tracks.Count == 10, "Timeline exposes ten extensible combat tracks");
            Check(timeline.tracks[1].marker(asset.frames[0]) == "1" && timeline.tracks[1].marker(asset.frames[added]) == null, "Hitbox track markers derive from the actual frame boxes");
            var box = new AttackHitboxData { offset = new Vector2(.6f,.8f), size = new Vector2(1.2f,.7f) }; var origin = new Vector2(.3f,.2f);
            Rect right = AttackPreviewGUI.BoxRect(box, origin, false), left = AttackPreviewGUI.BoxRect(box, origin, true);
            Check(Mathf.Abs(right.center.x - origin.x - .6f) < .001f && Mathf.Abs(left.center.x - origin.x + .6f) < .001f, "Preview mirrors canonical hitbox offsets");
            left.position += new Vector2(-.2f,.1f); left.size = new Vector2(1.4f,.9f); AttackPreviewGUI.SetBoxFromWorldRect(box, left, origin, true);
            var roundTrip = AttackPreviewGUI.BoxRect(box, origin, true);
            Check(Vector2.Distance(roundTrip.center, left.center) < .001f && Vector2.Distance(roundTrip.size, left.size) < .001f, "Mirrored move/resize converts back to canonical saved data");
            box.size = Vector2.zero; AttackPreviewGUI.SetBoxFromWorldRect(box, new Rect(0,0,0,0), Vector2.zero, false);
            Check(box.size.x == .01f && box.size.y == .01f, "Preview resizing enforces positive minimum box sizes");
            Vector2 path = AttackPreviewGUI.FramePosition(asset, 2, false), mirroredPath = AttackPreviewGUI.FramePosition(asset, 2, true);
            Check(Mathf.Abs(path.x + mirroredPath.x) < .001f && path.y == mirroredPath.y, "Movement path mirrors only forward X");
            var window = ScriptableObject.CreateInstance<AttackDataEditorWindow>(); window.SetAttack(asset); window.SelectFrame(2); window.SelectFrame(4, true);
            Check(window.CurrentAttack == asset && window.CurrentFrame == 4, "Editor window uses the actual AttackData and selects ranges");
            string state = JsonUtility.ToJson(window);
            var restored = ScriptableObject.CreateInstance<AttackDataEditorWindow>(); JsonUtility.FromJsonOverwrite(state, restored);
            Check(restored.CurrentAttack == asset && restored.CurrentFrame == 4, "Window attack and playhead survive Unity serialization/recompile state");
            UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(restored);
            AttackFrameAuthoring.Save(asset);
            Check(File.ReadAllText(tempPath).Contains("frames:") && File.ReadAllText(tempPath).Contains("hitstopFrames:"), "Save writes the runtime frame/hitbox schema to the real ScriptableObject asset");
            Check(protectedAssets.All(pair => File.ReadAllText(pair.Key) == pair.Value), "Validation leaves all existing attack assets unchanged");
            File.WriteAllLines("AttackEditorValidationResults.txt", results); Debug.Log("ATTACK EDITOR VALIDATION PASSED: " + results.Count + " assertions");
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception); File.WriteAllLines("AttackEditorValidationResults.txt", results); Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
        finally
        {
            EditorGUIUtility.systemCopyBuffer = previousClipboard;
            if (asset) { Undo.ClearUndo(asset); AssetDatabase.DeleteAsset(tempPath); }
        }
    }
}
