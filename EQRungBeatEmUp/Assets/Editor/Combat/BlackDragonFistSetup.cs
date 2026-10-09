using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class BlackDragonFistSetup
{
    [MenuItem("Beat Em Up/Skills/Tune Black Dragon Fist impact")]
    public static void Build()
    {
        if(EditorApplication.isPlaying){Debug.LogWarning("Tune Black Dragon Fist outside Play mode.");return;}
        var cast=AssetDatabase.LoadAssetAtPath<AttackData>(ShadowDragonSetup.CastPath);
        var source=PrefabUtility.LoadPrefabContents(ShadowDragonSetup.ProjectilePath);
        try
        {
            var shot=source.GetComponent<CombatProjectile>();
            shot.hit.hitType=HitType.KnockDown;shot.hit.hitstopFrames=11;shot.hit.knockback=5;
            shot.hit.launchVelocity=new Vector2(5,2.6f);shot.hit.knockdownDurationFrames=-1;
            shot.hit.forceAirborneTargetDownward=true;
            PrefabUtility.SaveAsPrefabAsset(source,ShadowDragonSetup.ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(source); }
        cast.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/face_hit_finisher_73.wav");
        cast.feedback.impactVolume=.9f;cast.feedback.impactScale=.42f;cast.feedback.impactLifetime=.45f;
        cast.feedback.impactShakeStrength=.05f;cast.feedback.impactShakeDuration=.14f;
        var skill=AssetDatabase.LoadAssetAtPath<PlayerSkillData>(ShadowDragonSetup.SkillPath);skill.displayName="Black Dragon Fist";
        EditorUtility.SetDirty(cast);EditorUtility.SetDirty(skill);AssetDatabase.SaveAssets();
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        // Refresh compatibility using the existing catalog's indexing. Repeated tuning
        // stays deterministic and cannot reorder network sprite/attack identifiers.
        var paths=AssetDatabase.GetDependencies(new[]{ComboTrackingSetup.PlayerPath,HauntedLevelBuilder.LevelPath}.Concat(catalog.characters.Select(AssetDatabase.GetAssetPath)).ToArray(),true).OrderBy(p=>p,StringComparer.Ordinal);
        string contents="GhostFairProtocol5|"+string.Join("|",paths.Select(p=>p+":"+AssetDatabase.GetAssetDependencyHash(p)))+"|"+string.Join("|",catalog.sprites.Select(s=>AssetDatabase.GetAssetPath(s)+":"+s.name));
        contents+="|"+string.Join("|",catalog.selectionCharacters.Where(c=>c).Select(c=>AssetDatabase.GetAssetPath(c)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
        contents+="|"+string.Join("|",Directory.GetFiles("Assets/EQ_Rung_BeatEmUp/Scripts","*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+":"+File.ReadAllText(p)))+"|"+File.ReadAllText("Packages/manifest.json");
        using(var sha=SHA256.Create())catalog.contentHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-","");
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
}
