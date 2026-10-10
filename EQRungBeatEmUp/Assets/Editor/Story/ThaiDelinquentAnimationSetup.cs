using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class ThaiDelinquentAnimationSetup
{
    const string Art = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations";
    const string Output = "Assets/EQ_Rung_BeatEmUp/Story/ThaiAnimations";
    static ThaiDelinquentAnimationSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ThaiAnimations.setup-request")) return;
        File.Delete("Temp/ThaiAnimations.setup-request");
        try { Repair(); }
        catch(Exception error) { Directory.CreateDirectory("Documentation"); File.WriteAllText("Documentation/ThaiAnimationValidationResults.txt", "FAIL: " + error); Debug.LogException(error); }
    }
    [MenuItem("Beat Em Up/Story/Repair Thai delinquent animations")]
    public static void Repair()
    {
        var path = PrologueSetup.Root + "/ThaiDelinquent.prefab";
        var go = PrefabUtility.LoadPrefabContents(path);
        try { Configure(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
        finally { PrefabUtility.UnloadPrefabContents(go); }
        AssetDatabase.SaveAssets();
        var dependencies = AssetDatabase.GetDependencies(path, true);
        if(dependencies.Any(p => p.Contains("/Enemies/Rusher/") || p.EndsWith("Rusher_Stunned.asset"))) throw new Exception("Thai prefab still depends on Rusher artwork");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var reaction = prefab.GetComponent<EnemyHitReaction>();
        var animator = prefab.GetComponent<CharacterAnimation>().animator;
        if(!(animator.runtimeAnimatorController is AnimatorController controller)) throw new Exception("Thai controller is missing");
        foreach(var state in controller.layers[0].stateMachine.states)
        {
            if(!(state.state.motion is AnimationClip clip)) throw new Exception("Missing motion: " + state.state.name);
            var keys = AnimationUtility.GetObjectReferenceCurveBindings(clip).SelectMany(b => AnimationUtility.GetObjectReferenceCurve(clip, b)).ToArray();
            if(keys.Length == 0 || keys.Any(k => !k.value || !AssetDatabase.GetAssetPath(k.value).StartsWith(Art + "/"))) throw new Exception("Incorrect character artwork: " + state.state.name);
        }
        if(!reaction.airborneSprite || !reaction.downedSprite || !reaction.stunAnimation || reaction.stunAnimation.frames.Any(f => !f.sprite || !AssetDatabase.GetAssetPath(f.sprite).StartsWith(Art + "/"))) throw new Exception("Incomplete Thai reaction artwork");
        Directory.CreateDirectory("Documentation");
        File.WriteAllText("Documentation/ThaiAnimationValidationResults.txt", "PASS: All " + controller.layers[0].stateMachine.states.Length + " animation states use Thai artwork. Airborne, downed, knockdown, get-up, and stun references use Thai assets. No Rusher artwork dependencies remain.");
        Debug.Log("Thai enemy animation repair and reference validation passed.");
    }
    public static void Configure(GameObject go)
    {
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        var rows = ImportSheet();
        Sprite[] idle = Existing("Idle"), walk = Existing("Walk"), hurt = Existing("Hurt"), knockdown = Existing("Knockdown"), getUp = Existing("GetUp"), punch = Existing("Attack1");
        var clips = new Dictionary<string, AnimationClip>();
        clips["Idle"] = Clip("Idle", idle, true, 8f / 12);
        clips["Walk"] = Clip("Walk", walk, true, 8f / 12);
        clips["Sprint"] = Clip("Sprint", walk, true, 8f / 18);
        clips["GroundHit"] = clips["Hurt_Light"] = clips["Hurt_Heavy"] = Clip("Hurt", hurt, false, .4f);
        clips["Knockdown"] = clips["Landing"] = Clip("Knockdown", knockdown, false, ExistingDuration(go.GetComponent<EnemyHitReaction>().knockdownClip, .5f));
        clips["GetUp"] = Clip("GetUp", getUp, false, ExistingDuration(go.GetComponent<EnemyHitReaction>().getUpClip, .5f));
        clips["Air_Hit"] = clips["AirHit"] = clips["Launched"] = clips["Falling"] = Clip("AirHit", rows[0], false, .5f);
        clips["Downed"] = Clip("Downed", new[] { rows[1][5], rows[1][5] }, true, .5f);
        clips["Defeated"] = Clip("Defeated", rows[1], false, .5f);
        clips["Stunned"] = Clip("Stunned", rows[2], true, .6f);
        clips["Recovery"] = clips["StunRecovery"] = Clip("Recovery", rows[3], false, .5f);
        clips["Attack_Slash1"] = clips["Attack_Slash2"] = clips["Attack_Lunge"] = Clip("Punch", punch, false, .8f);
        string controllerPath = Output + "/ThaiDelinquent.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach(var child in machine.states) machine.RemoveState(child.state);
        foreach(var pair in clips)
        {
            var state = machine.AddState(pair.Key); state.motion = pair.Value; state.writeDefaultValues = false;
            if(pair.Key == "Idle") machine.defaultState = state;
        }
        go.GetComponent<CharacterAnimation>().animator.runtimeAnimatorController = controller;
        go.GetComponent<CharacterMotor>().sprite.sprite = idle[0];
        var reaction = go.GetComponent<EnemyHitReaction>();
        reaction.knockdownClip = clips["Knockdown"]; reaction.getUpClip = clips["GetUp"];
        reaction.airborneSprite = rows[0][2]; reaction.downedSprite = rows[1][5]; reaction.stunFallbackSprite = rows[2][0];
        string stunPath = Output + "/ThaiDelinquent_Stunned.asset";
        var stun = AssetDatabase.LoadAssetAtPath<AttackData>(stunPath);
        if(!stun) { stun = ScriptableObject.CreateInstance<AttackData>(); AssetDatabase.CreateAsset(stun, stunPath); }
        stun.attackName = "Thai delinquent stun loop"; stun.frames.Clear();
        foreach(var sprite in rows[2]) for(int hold = 0; hold < 6; hold++) stun.frames.Add(new AttackFrameData { sprite = sprite });
        reaction.stunAnimation = stun; EditorUtility.SetDirty(stun); EditorUtility.SetDirty(controller);
        var attack = go.GetComponent<EnemyCombat>().attack;
        // Preserve combat timing and hitboxes, replacing only visual frames.
        for(int i = 0; i < attack.frames.Count; i++) attack.frames[i].sprite = punch[Mathf.Min(punch.Length - 1, i * punch.Length / attack.frames.Count)];
        attack.attackName = "Thai delinquent punch"; attack.artworkNotes = "Thai technical-school delinquent punch artwork; original combat timing and hitboxes preserved.";
        EditorUtility.SetDirty(attack);
    }
    static float ExistingDuration(AnimationClip clip, float fallback) => clip ? Mathf.Max(.1f, clip.length) : fallback;
    static Sprite[] Existing(string type) => Directory.GetFiles(Art + "/" + type, "ThaiBadBoy_" + type + "_*.png").Where(p => !p.EndsWith("_Sheet.png") && !p.EndsWith("_Review.png")).OrderBy(p => p).Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/'))).ToArray();
    static AnimationClip Clip(string name, Sprite[] sprites, bool loop, float duration)
    {
        if(sprites.Length == 0 || sprites.Any(s => !s)) throw new Exception("Missing Thai frames: " + name);
        string path = Output + "/Thai_" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.name = "Thai_" + name; clip.frameRate = 60;
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for(int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i * duration / sprites.Length, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = duration, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip); return clip;
    }
    static Sprite[][] ImportSheet()
    {
        string path = Art + "/Reactions/ThaiBadBoy_Reactions.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 200; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
        var names = new[] { "AirHit", "Defeated", "Stunned", "Recovery" };
        int[] top = { 0, 310, 520, 770 }, bottom = { 300, 510, 770, 1024 }, baseline = { 270, 480, 746, 1006 };
        var slices = new List<SpriteMetaData>();
        for(int row = 0; row < 4; row++) for(int column = 0; column < 6; column++)
            slices.Add(new SpriteMetaData { name = "Thai_" + names[row] + "_" + (column + 1).ToString("00"), rect = new Rect(column * 256, 1024 - bottom[row], 256, bottom[row] - top[row]), alignment = 9, pivot = new Vector2(.5f, (bottom[row] - baseline[row]) / (float)(bottom[row] - top[row])) });
        importer.spritesheet = slices.ToArray(); importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        return names.Select(name => sprites.Where(s => s.name.StartsWith("Thai_" + name + "_")).OrderBy(s => s.name).ToArray()).ToArray();
    }
}
