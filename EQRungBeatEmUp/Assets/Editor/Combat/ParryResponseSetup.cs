using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ParryResponseSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse";
    public const string DeflectPath = Root + "/Attacks/Projectile_DeflectFeedback.asset";
    static ParryResponseSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ParryResponseSetup.request")) return;
        File.Delete("Temp/ParryResponseSetup.request"); Configure();
    }
    static Sprite[] Sprites(string folder) => Directory.GetFiles(folder, "*.png")
        .Where(p => System.Text.RegularExpressions.Regex.IsMatch(p, @"_\d{2}\.png$"))
        .OrderBy(p => p).Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).Where(s => s).ToArray();
    [MenuItem("Beat Em Up/Configure parry stun and projectile deflection")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) return;
        AssetDatabase.Refresh();
        var defense = AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(PlayerStunSetup.DefensePath);
        defense.parryAttackerStunFrames = 90; EditorUtility.SetDirty(defense);
        foreach (var name in new[] { "Rusher", "Thrower", "Screamer", "GrapplerBruiser", "Ambusher", "Prefect" })
        {
            string phase = name == "Screamer" ? "Stunned" : "Hurt_Light";
            var sprites = Sprites("Assets/ArtAssets/Characters/Enemies/" + name + "/Animations/" + phase);
            string path = Root + "/Attacks/" + name + "_Stunned.asset";
            var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<AttackData>(); AssetDatabase.CreateAsset(data, path); }
            data.attackName = name + " stun sprite loop"; data.frames.Clear();
            // Hold the slumped hurt poses instead of repeatedly replaying an impact recoil.
            var poses = name == "Screamer" ? sprites : sprites.Skip(Mathf.Max(0, sprites.Length - 2)).ToArray();
            foreach (var sprite in poses) for (int frame=0; frame<6; frame++) data.frames.Add(new AttackFrameData { sprite=sprite, movementInputScale=0 });
            data.artworkNotes = name == "Screamer" ? "Existing dedicated Stunned sprites, looped by EnemyHitReaction." : "PLACEHOLDER stun loop using this enemy's last Hurt_Light poses. Replace frame sprites with dedicated dizzy art; overhead stun VFX differentiates status from hitstun.";
            EditorUtility.SetDirty(data);
            ConfigureEnemy(Root + "/Prefabs/" + name + ".prefab", data, defense);
        }
        ConfigureEnemy("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab", null, defense);
        ConfigureEnemy(Root + "/Prefabs/WhiteGhostBoss.prefab", null, defense);
        var feedback = AssetDatabase.LoadAssetAtPath<AttackData>(DeflectPath);
        if (!feedback) { feedback=ScriptableObject.CreateInstance<AttackData>(); AssetDatabase.CreateAsset(feedback, DeflectPath); }
        feedback.attackName="Projectile deflect feedback";
        feedback.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/blade_hit_08.wav");
        feedback.feedback.impactVolume=.55f; EditorUtility.SetDirty(feedback);
        ConfigureProjectile(Root+"/Prefabs/NotebookProjectile.prefab", true, feedback);
        ConfigureProjectile(Root+"/Prefabs/ScreamWaveProjectile.prefab", false, null);
        AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        Debug.Log("PARRY RESPONSES CONFIGURED: 90f universal enemy stun; boss excluded; notebook reflects at 1.2x speed / 1x damage; scream wave cannot parry/reflect.");
    }
    static void ConfigureEnemy(string path, AttackData data, PlayerDefenseData defense)
    {
        var go=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var reaction=go.GetComponent<EnemyHitReaction>();
            reaction.canBeParryStunned=!go.GetComponent<TotemBossController>();
            reaction.stunAnimation=data; reaction.stunFallbackSprite=reaction.airborneSprite ? reaction.airborneSprite : reaction.downedSprite;
            reaction.stunVfx=defense.stunVfx; reaction.stunVfxHoldFrames=defense.stunVfxHoldFrames;
            reaction.stunVfxOffset=defense.stunVfxOffset; reaction.stunVfxScale=defense.stunVfxScale;
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
    static void ConfigureProjectile(string path, bool deflect, AttackData feedback)
    {
        var go=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var shot=go.GetComponent<CombatProjectile>(); shot.canBeDeflected=deflect; shot.hit.canBeParried=deflect;
            shot.maxDeflections=1; shot.canHitOriginalOwner=true; shot.deflectDamageMultiplier=1; shot.deflectSpeedMultiplier=1.2f;
            shot.deflectHitstunFrames=18; shot.deflectKnockback=1; shot.deflectFeedback=feedback;
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
}
