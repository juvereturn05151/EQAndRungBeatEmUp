using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;

[InitializeOnLoad]
public static class ThaiEnemyPositionValidation
{
    const string Pending = "BeatEmUp.ThaiEnemyPositionValidation";
    const string Saved = Pending + ".Scenes";
    [Serializable] sealed class SceneBackup { public string[] paths; public bool[] loaded, active; }
    static readonly List<string> results = new List<string>();
    static ThaiEnemyPositionValidation()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += Restore;
    }
    [MenuItem("Beat Em Up/Enemies/Validate Thai enemy attack positioning")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        for(int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
            { Debug.LogWarning("Save open scenes before validating enemy positioning."); return; }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        SessionState.SetString(Saved, JsonUtility.ToJson(new SceneBackup { paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(), active = setup.Select(s => s.isActive).ToArray() }));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void Restore(PlayModeStateChange state)
    {
        if(state != PlayModeStateChange.EnteredEditMode) return;
        string json = SessionState.GetString(Saved, ""); if(string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(Saved);
        var backup = JsonUtility.FromJson<SceneBackup>(json);
        EditorApplication.delayCall += () => EditorSceneManager.RestoreSceneManagerSetup(backup.paths.Select((path, i) => new SceneSetup { path = path, isLoaded = backup.loaded[i], isActive = backup.active[i] }).ToArray());
    }
    static void Check(bool condition, string message)
    {
        if(!condition) throw new Exception(message);
        results.Add("PASS: " + message);
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(File.Exists("Temp/ThaiEnemyPosition.validate-request") && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete("Temp/ThaiEnemyPosition.validate-request"); Run(); return; }
        if(!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            foreach(string path in new[] { "Assets/EQ_Rung_BeatEmUp/Story/ThaiDelinquent.prefab", "Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab" })
                foreach(Vector2 start in new[] { new Vector2(0, -.4f), new Vector2(0, .4f), new Vector2(-2, -.4f), new Vector2(2, .4f) })
                    Validate(path, start);
            results.Add("RESULT: PASS");
        }
        catch(Exception error) { results.Add("RESULT: FAIL " + error); Debug.LogException(error); }
        finally
        {
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/ThaiEnemyPositionValidationResults.txt", results);
            EditorApplication.ExitPlaymode();
        }
    }
    static void Validate(string path, Vector2 start)
    {
        var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var target = new GameObject("Positioning test target");
        try
        {
            var brain = enemy.GetComponent<EnemyCombat>(); brain.passiveTrainingDummy = false; brain.target = target.transform;
            target.transform.position = Vector3.zero; brain.motor.ResetForStage(start);
            var clock = UnityEngine.Object.FindAnyObjectByType<CombatClock>(); clock.enabled = false;
            for(int frame = 0; frame < 240 && !brain.attackPlayer.CurrentAttack; frame++) clock.StepFrame();
            Vector2 delta = target.transform.position - enemy.transform.position;
            Check(brain.attackPlayer.CurrentAttack && Mathf.Abs(delta.x) >= brain.minimumAttackRange && Mathf.Abs(delta.x) <= brain.attackRange && Mathf.Abs(delta.y) < brain.laneRange,
                enemy.name + " approaches from " + start + " and punches beside the target on the same lane (gap " + delta + ")");
            int limit = 0;
            while(brain.attackPlayer.CurrentAttack && limit++ < 600) clock.StepFrame();
            Check(!brain.attackPlayer.CurrentAttack, "Attack finishes within its authored timeline");
            Vector2 position = enemy.transform.position;
            for(int frame = 0; frame < 15; frame++) clock.StepFrame();
            Check(Vector2.Distance(position, enemy.transform.position) < .001f && !brain.attackPlayer.CurrentAttack,
                "Enemy holds its aligned position during attack cooldown");
            target.transform.position += Vector3.up * .4f;
            clock.StepFrame();
            Check(brain.motor.MoveInput.y > 0 && Mathf.Abs(brain.motor.MoveInput.x) < .001f && !brain.attackPlayer.CurrentAttack,
                "Enemy follows a lane change without closing the horizontal punching gap");
            if(path.EndsWith("ThaiDelinquent.prefab"))
            {
                var controller = brain.animationDriver.animator.runtimeAnimatorController as AnimatorController;
                Check(controller, "Thai enemy uses its own animator controller");
                foreach(var state in controller.layers[0].stateMachine.states)
                {
                    brain.animationDriver.ReleaseReactionControl(); brain.animationDriver.Play(state.state.name, true);
                    brain.animationDriver.animator.Update(0);
                    Check(AssetDatabase.GetAssetPath(brain.motor.sprite.sprite).Contains("/NPCs/ThaiBadBoy/"),
                        state.state.name + " displays the Thai character at runtime");
                }
                brain.animationDriver.ReleaseReactionControl(); brain.animationDriver.Play("Idle", true);
                Check(brain.reaction.EnterStun(30), "Thai enemy enters its parry-stun reaction");
                for(int frame = 0; frame < 12; frame++) clock.StepFrame();
                Check(AssetDatabase.GetAssetPath(brain.motor.sprite.sprite).EndsWith("ThaiBadBoy_Reactions.png"),
                    "Parry stun displays the newly drawn Thai sprites instead of Rusher");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(enemy); UnityEngine.Object.DestroyImmediate(target); }
    }
}
