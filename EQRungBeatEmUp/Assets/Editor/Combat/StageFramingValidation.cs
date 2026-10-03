using System;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class StageFramingValidation
{
    const string Pending = "BeatEmUp.StageFramingValidation";
    static readonly List<string> results = new List<string>();
    static ComboController player;
    static EnemyHitReaction enemy;
    static StageFraming framing;
    static Camera camera;
    static CombatClock clock;
    static float peakSize;
    static StageFramingValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate stage framing (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(StageFramingSetup.ScenePath);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            framing = UnityEngine.Object.FindFirstObjectByType<StageFraming>(); camera = framing.GetComponent<Camera>();
            player = framing.player.GetComponent<ComboController>(); enemy = UnityEngine.Object.FindFirstObjectByType<EnemyHitReaction>();
            player.GetComponent<PlayerCombatInput>().enabled = false; enemy.GetComponent<EnemyCombat>().enabled = false;
            clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            Check(camera.orthographic && camera.orthographicSize == 2, "Actual gameplay scene retains orthographic projection with size 2");
            Check(player.transform.localScale == Vector3.one && enemy.transform.localScale == Vector3.one && player.motor.visual.localScale == Vector3.one && enemy.motor.visual.localScale == Vector3.one, "Player/enemy world and visual scales remain unchanged");
            Check(player.motor.arenaMin == new Vector2(-6, -.4f) && player.motor.arenaMax == new Vector2(6, .65f), "Scene player has explicit lane bounds and unchanged horizontal progression");
            Check(enemy.motor.arenaMin.y == -.4f && enemy.motor.arenaMax.y == .65f, "Scene enemy shares the same lane bounds");
            foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(1600,900), new Vector2Int(1280,720), new Vector2Int(2560,1080), new Vector2Int(1024,768) })
            {
                camera.aspect = (float)resolution.x / resolution.y; framing.ApplyFraming(0, true);
                float height = OpaqueHeight(player.motor.sprite.sprite) * player.motor.sprite.transform.lossyScale.y / (2 * camera.orthographicSize);
                Check(height >= .25f && height <= .28f, resolution + " keeps standing art at " + (height * 100).ToString("F2") + "% of viewport height");
                Check(Mathf.Abs(camera.WorldToViewportPoint(new Vector3(0, framing.BackgroundBoundary)).y - .6f) < .0001f, resolution + " keeps the floor/background split at 40% from the top");
                Check(Mathf.Abs(camera.WorldToViewportPoint(player.transform.position).y - .16f) < .0001f, resolution + " places default feet 84% from the top");
                Check(Mathf.Abs(framing.floor.bounds.max.y - framing.BackgroundBoundary) < .0001f && framing.floor.bounds.size.x >= camera.orthographicSize * camera.aspect * 2, resolution + " floor fills viewport without stretching character transforms");
                Capture("Standing-" + resolution.x + "x" + resolution.y, resolution.x, resolution.y);
            }
            camera.aspect = 16f / 9;
            player.motor.MoveInput = Vector2.up; Step(90);
            Check(Mathf.Abs(player.transform.position.y - .65f) < .0001f, "Up movement clamps at the top lane rather than entering the background");
            Capture("TopLane", 1920, 1080);
            player.motor.MoveInput = Vector2.down; Step(90);
            Check(Mathf.Abs(player.transform.position.y + .4f) < .0001f && camera.WorldToViewportPoint(player.transform.position).y >= .05f, "Bottom lane retains a six-percent grounding margin");
            Visible(player.motor, "Bottom lane"); Capture("BottomLane", 1920, 1080);
            player.motor.MoveInput = Vector2.right; Step(150);
            Check(player.transform.position.x > 5 && camera.transform.position.x > 4, "Camera tracks horizontally while original stage X limits remain intact"); Visible(player.motor, "Horizontal edge");
            player.motor.MoveInput = Vector2.zero;
            var extras = new List<GameObject>();
            for (int i=0;i<3;i++)
            {
                var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab")); extras.Add(go);
                go.GetComponent<EnemyCombat>().enabled = false; go.transform.position = new Vector3(4.2f + i * .6f, -.2f + i * .3f);
                var motor = go.GetComponent<CharacterMotor>();
                Check(motor.arenaMin.y == -.4f && motor.arenaMax.y == .65f, "Runtime spawned enemy " + i + " receives stage lanes"); Visible(motor, "Spawned enemy " + i);
            }
            Capture("SeveralEnemies", 1920, 1080); foreach (var extra in extras) UnityEngine.Object.DestroyImmediate(extra);
            FullRoute(16f/9); FullRoute(2560f/1080); FullRoute(4f/3);
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/StageFramingValidationResults.txt", results);
            Debug.Log("STAGE FRAMING VALIDATION PASSED: " + results.Count + " checks; air peak size=" + peakSize);
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch(Exception ex)
        {
            results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/StageFramingValidationResults.txt", results); Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    static float OpaqueHeight(Sprite sprite)
    {
        var texture = new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels = texture.GetPixels32(); int low=texture.height, high=-1;
        for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) if(pixels[y*texture.width+x].a >= 128) { low=Mathf.Min(low,y); high=Mathf.Max(high,y); }
        UnityEngine.Object.DestroyImmediate(texture); return (high-low+1)/sprite.pixelsPerUnit;
    }
    static void Step(int count=1)
    {
        for(int i=0;i<count;i++) { Physics2D.SyncTransforms(); clock.StepFrame(); framing.ApplyFraming(1f/60); }
    }
    static void Visible(CharacterMotor motor, string label)
    {
        var bounds=motor.sprite.bounds; var min=camera.WorldToViewportPoint(bounds.min); var max=camera.WorldToViewportPoint(bounds.max);
        Check(min.x >= 0 && max.x <= 1 && min.y >= 0 && max.y <= 1, label + " complete sprite canvas remains visible");
    }
    static void Until(Func<bool> condition, string label)
    {
        for(int i=0;i<180 && !condition();i++)
        {
            Step(); Visible(player.motor, label + " player tick " + i); Visible(enemy.motor, label + " enemy tick " + i);
            if(camera.orthographicSize > peakSize)
            {
                peakSize=camera.orthographicSize;
                int width=Mathf.RoundToInt(camera.aspect*1080);
                Capture("AirPeak-"+width+"x1080",width,1080);
            }
        }
        Check(condition(), label);
    }
    static void FullRoute(float aspect)
    {
        camera.aspect = aspect;
        player.health.Restore(); enemy.health.Restore(); player.transform.position = new Vector3(-.7f,.65f); enemy.transform.position = new Vector3(.15f,.65f);
        player.motor.Face(1); enemy.motor.Face(-1); camera.transform.position=new Vector3(0,1.36f,-10); framing.ApplyFraming(0,true); peakSize=2;
        player.RequestAttack(); Step(3); player.RequestAttack(); Until(()=>player.CurrentAttack == player.groundCombo[1], "Punch1 to Punch2");
        Step(2); player.RequestLauncher(); Until(()=>player.CurrentAttack == player.launcher, "Punch2 to Launcher"); player.RequestJump();
        Until(()=>!enemy.motor.IsGrounded, "Launcher connects"); Until(()=>!player.motor.IsGrounded, "Jump cancel connects");
        player.RequestAttack(); Check(player.CurrentAttack == player.airCombo[0], "Jump enters AirPunch1");
        Until(()=>enemy.JuggleHits == 1, "AirPunch1 hits"); player.RequestAttack(); Until(()=>player.CurrentAttack == player.airCombo[1], "AirPunch2 starts");
        Until(()=>enemy.JuggleHits == 2, "AirPunch2 hits"); player.RequestAttack(); Until(()=>player.CurrentAttack == player.airCombo[2], "AirPunch3 starts");
        Until(()=>enemy.State == EnemyReaction.Falling, "AirPunch3 finisher connects"); Until(()=>enemy.motor.IsGrounded && player.motor.IsGrounded, "Both fighters land");
        Check(peakSize > 2, "Camera makes extra room for actual air combo at the highest lane: peak size " + peakSize.ToString("F3"));
        Step(180); Check(Mathf.Abs(camera.orthographicSize-2) < .0001f, "Camera smoothly restores standing composition after landing");
    }
    static void Capture(string name,int width,int height)
    {
        if(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var target=new RenderTexture(width,height,24); var previous=camera.targetTexture; var active=RenderTexture.active;
        try
        {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/StageFramingPreview"); File.WriteAllBytes("Documentation/StageFramingPreview/"+name+".png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=previous; RenderTexture.active=active; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
