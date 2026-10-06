using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;
using System.IO;

[InitializeOnLoad]
public static class MultiplayerValidation
{
    const string Pending="GhostFair.LocalValidation";
    static bool running;
    static int activeCount;
    static MultiplayerValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Beat Em Up/Multiplayer/Validate local 2P full run (Play Mode)")]
    public static void Run2() => Run(2);
    public static void Run1() => Run(1);
    public static void GroundRegression() { EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); PunchPlaytestValidation.Run(); }
    public static void AirRegression() { EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); AirPunchPlaytestValidation.Run(); }
    public static void DefenseRegression() { EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); PlayerDefenseValidation.Run(); }
    [MenuItem("Beat Em Up/Multiplayer/Validate local 4P full run (Play Mode)")]
    public static void Run4() => Run(4);
    static void Run(int count)
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(MultiplayerSetup.MenuPath); SessionState.SetInt(Pending,count); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        int count=SessionState.GetInt(Pending,0);
        if(count>0 && MultiplayerSession.Active)
        {
            SessionState.SetInt(Pending,0); running=true; activeCount=count;
            new GameObject("Local multiplayer validation").AddComponent<MultiplayerSmokeTest>().StartLocal(count,"Documentation/MultiplayerLocal"+count+"Results.txt");
        }
        if(running && MultiplayerSmokeTest.Finished)
        {
            bool passed=MultiplayerSmokeTest.Passed;
            if(passed && activeCount>1)
            {
                var session=MultiplayerSession.Active;
                foreach(var player in PlayerRoster.Players)
                {
                    player.Health.SafeStageProtection=false;
                    // A run blessing may save one lethal hit; defeat the actual resulting health state.
                    for(int attempt=0;player.Living && attempt<4;attempt++) player.Health.Damage(10000);
                }
                bool allDefeated=session.CaptureSnapshot().gameOver;
                session.Retry();
                bool restarted=PlayerRoster.Players.All(p=>p.Living) && !session.CaptureSnapshot().gameOver && session.Flow.StageIndex==0;
                passed=allDefeated && restarted;
                File.AppendAllLines("Documentation/MultiplayerLocal"+activeCount+"Results.txt",new[]{(allDefeated ? "PASS: " : "FAIL: ")+"All defeated players produce party Game Over",(restarted ? "PASS: " : "FAIL: ")+"Host retry restores every player and restarts at hub"});
            }
            running=false; if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
        }
    }
    public static void BuildDevelopmentPlayer()
    {
        MultiplayerSetup.Build();
        var scenes=File.Exists(PlayerSanctuarySetup.ScenePath) ? new[]{MultiplayerSetup.MenuPath,PlayerSanctuarySetup.ScenePath,HauntedLevelBuilder.ScenePath} : new[]{MultiplayerSetup.MenuPath,HauntedLevelBuilder.ScenePath};
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName="E:/EQRungBeatEmUp/MultiplayerValidationBuild/GhostFair.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Multiplayer development build failed");
    }
}
