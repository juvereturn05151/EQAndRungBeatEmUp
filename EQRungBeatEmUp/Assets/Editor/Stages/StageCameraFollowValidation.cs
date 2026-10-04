using System;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class StageCameraFollowValidation
{
    const string Pending = "BeatEmUp.StageCameraFollowValidation";
    static readonly List<string> results = new List<string>();
    static StageFlowController flow;
    static StageFraming framing;
    static Camera camera;
    static RenderTexture target;
    static StageCameraFollowValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Stages/Validate camera safe-frame follow (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
    static void SetPlayer(float x) { flow.player.ResetForStage(new Vector2(x, 0)); }
    static void Step(int count = 1) { for (int i = 0; i < count; i++) framing.ApplyFraming(1f / 60); }
    static void Visible(string label)
    {
        var bounds = flow.player.sprite.bounds;
        Check(camera.WorldToViewportPoint(bounds.min).x >= -.002f && camera.WorldToViewportPoint(bounds.max).x <= 1.002f, label + $" (sprite X {bounds.min.x:0.###}..{bounds.max.x:0.###}, camera X {camera.transform.position.x:0.###}, half width {camera.orthographicSize * camera.aspect:0.###})");
    }
    static void Bounded(string label)
    {
        float half = camera.orthographicSize * camera.aspect;
        Check(camera.transform.position.x - half >= framing.stageLeft - .002f && camera.transform.position.x + half <= framing.stageRight + .002f, label);
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        LevelDefinition original = null, copy = null;
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); framing = flow.framing; camera = framing.GetComponent<Camera>();
            flow.enabled = false; UnityEngine.Object.FindFirstObjectByType<CombatClock>().enabled = false;
            flow.player.GetComponent<PlayerCombatInput>().enabled = false;
            original = flow.level; copy = UnityEngine.Object.Instantiate(original); flow.level = copy;
            target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; camera.ResetAspect();
            Check(framing.followEnabled && Near(framing.followSmoothTime, .12f), "Actual scene enables smooth safe-frame follow");
            flow.EnterStage(0); SetPlayer(0); framing.ApplyFraming(0, true);
            float center = camera.transform.position.x;
            Step(120); Check(Near(camera.transform.position.x, center), "TEST3: centered player does not cause camera drift");
            SetPlayer(3.05f); Step(60); Visible("TEST6: hub right boundary keeps player sprite visible"); Bounded("Hub right boundary keeps viewport inside art");
            SetPlayer(-3.05f); Step(60); Visible("Hub left boundary keeps player sprite visible"); Bounded("Hub left boundary keeps viewport inside art");
            Check(flow.player.GetComponent<CharacterHealth>().SafeStageProtection, "Hub protection remains enabled");
            copy.stages[0].artWidth = 18;
            copy.stages[0].movementMin = new Vector2(-8.3f, -.4f); copy.stages[0].movementMax = new Vector2(8.3f, .65f);
            flow.EnterStage(0); SetPlayer(0);
            camera.transform.position = new Vector3(0, camera.transform.position.y, -10); framing.ApplyFraming(0, true);
            SetPlayer(.2f); Step(120); Check(Near(camera.transform.position.x, 0), "Small motion inside safe frame stays stationary");
            SetPlayer(4); Step();
            Check(camera.transform.position.x > 0 && camera.transform.position.x < 3, "TEST1: right follow starts smoothly before leaving screen");
            Visible("Fast right movement stays visible during damping"); Step(120); Visible("Right follow settles with player visible");
            float settled = camera.transform.position.x; Step(120); Check(Near(camera.transform.position.x, settled), "Follow settles without drift");
            SetPlayer(-4); Step(); Check(camera.transform.position.x < settled, "TEST2: left follow responds immediately"); Visible("Left teleport cannot escape viewport"); Step(120); Visible("Left follow settles with player visible");
            SetPlayer(0); camera.transform.position = new Vector3(0, camera.transform.position.y, -10); framing.ApplyFraming(0, true);
            flow.player.MoveInput = Vector2.right; bool visibleWhileRunning = true;
            for (int i = 0; i < 180; i++)
            {
                flow.player.CombatFrame(); Step(); var bounds = flow.player.sprite.bounds;
                visibleWhileRunning &= camera.WorldToViewportPoint(bounds.min).x >= -.002f && camera.WorldToViewportPoint(bounds.max).x <= 1.002f;
            }
            flow.player.MoveInput = Vector2.zero;
            Check(visibleWhileRunning && camera.transform.position.x > 0, "Actual motor movement stays visible throughout run to boundary");
            SetPlayer(8.3f); Step(120); Bounded("TEST4: camera stops at stage right art limit"); Visible("Right art edge still includes complete player sprite");
            SetPlayer(-8.3f); Step(120); Bounded("Camera stops at stage left art limit"); Visible("Left art edge still includes complete player sprite");
            framing.followEnabled = false; SetPlayer(0); center = camera.transform.position.x; Step(60); Check(Near(camera.transform.position.x, center), "Follow toggle disables player tracking"); framing.followEnabled = true;
            flow.EnterStage(1); SetPlayer(5.73f); Step(120); Visible("Entrance Gate's existing extended X bounds remain visible"); Bounded("Entrance Gate follows without exposing empty sides");
            Check(!flow.ExitUnlocked, "TEST5: following in combat does not unlock encounter exit");
            Check(Near(framing.stageRight, copy.stages[1].artWidth * .5f), "Stage transition installs new art bounds");
            flow.EnterStage(7); SetPlayer(3.05f); Step(60); Visible("Boss stage supports bounded follow"); Check(!flow.ExitUnlocked, "Boss exit lock remains intact");
            flow.EnterStage(0); SetPlayer(0); framing.ApplyFraming(0, true);
            float y = camera.transform.position.y; flow.player.ResetForStage(new Vector2(0, .4f)); Step(60); Check(Near(camera.transform.position.y, y), "Small lane motion preserves vertical composition");
            foreach (var dimensions in new[] { new Vector2Int(1024,768), new Vector2Int(2560,1080) })
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                target = new RenderTexture(dimensions.x, dimensions.y, 24); camera.targetTexture = target;
                flow.EnterStage(7); SetPlayer(3.05f); Step(60); Bounded(dimensions + " respects art bounds"); Visible(dimensions + " keeps player visible");
                if (dimensions.x > 2000) Check(camera.rect.width < 1 && Near(camera.orthographicSize, framing.orthographicSize), "Ultrawide pillarboxing avoids blank art edges while preserving vertical size");
            }
            flow.EnterStage(0); SetPlayer(0); flow.player.Launch(12, 0);
            for (int i = 0; i < 20; i++) { flow.player.CombatFrame(); Step(); }
            Check(camera.orthographicSize > framing.orthographicSize, "Existing airborne zoom remains active"); Bounded("Airborne zoom keeps horizontal viewport inside stage");
            flow.level = original; flow.EnterStage(0);
            Finish(0);
        }
        catch (Exception ex) { results.Add("FAIL: " + ex); Debug.LogException(ex); if (flow && original) flow.level = original; Finish(1); }
        finally
        {
            if (camera) camera.targetTexture = null;
            if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (copy) UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    static void Finish(int code)
    {
        Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/StageCameraFollowValidationResults.txt", results);
        Debug.Log("STAGE CAMERA FOLLOW VALIDATION " + (code == 0 ? "PASSED" : "FAILED") + ": " + results.Count);
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
