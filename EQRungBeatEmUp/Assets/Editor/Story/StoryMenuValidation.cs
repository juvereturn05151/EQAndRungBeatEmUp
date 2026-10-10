using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using BeatEmUp.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class StoryMenuValidation
{
    const string Pending="StoryMenu.Validation";
    static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    static readonly List<string> results=new List<string>();
    static float deadline;
    static StoryMenuValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Beat Em Up/Story/Validate New Game and Continue (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
        SessionState.SetString(Pending+".Backup",PlayerPrefs.GetString(StoryProgress.SaveKey,""));
        StoryProgress.Reset();MultiplayerSetup.Build();
        EditorSceneManager.OpenScene(MultiplayerSetup.MenuPath);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception(message);results.Add("PASS: "+message);
    }
    static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name && b.gameObject.activeInHierarchy);
    static IEnumerator Until(Func<bool> condition,string message)
    {
        float end=Time.realtimeSinceStartup+15;
        while(!condition()) { if(Time.realtimeSinceStartup>end)throw new Exception(message);yield return null; }
    }
    static void Poll()
    {
        if(File.Exists("Temp/StoryMenuValidation.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        { File.Delete("Temp/StoryMenuValidation.request");Run();return; }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(SessionState.GetBool(Pending,false) && MultiplayerSession.Active && Object.FindFirstObjectByType<MultiplayerMenu>())
            {
                SessionState.SetBool(Pending,false);Application.runInBackground=true;
                results.Clear();stack.Clear();deadline=Time.realtimeSinceStartup+90;stack.Push(Exercise());
            }
            if(stack.Count==0)return;
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Story menu validation timed out");
            var routine=stack.Peek();if(!routine.MoveNext())stack.Pop();else if(routine.Current is IEnumerator nested)stack.Push(nested);
            if(stack.Count==0)Finish();
        }
        catch(Exception error) { results.Add("FAIL: "+error);Debug.LogException(error);Finish(); }
    }
    static IEnumerator Exercise()
    {
        yield return null;yield return null;
        var session=MultiplayerSession.Active;var menu=Object.FindFirstObjectByType<MultiplayerMenu>();
        Check(Button("NEW GAME").interactable && !Button("CONTINUE GAME").interactable,"New Game available; Continue disabled without a story save");
        var saved=new StoryProgress{checkpoint=1};saved.Set("PrologueStarted");saved.Set("ValidationResumeMarker");saved.Save();
        menu.Refresh();Check(Button("CONTINUE GAME").interactable,"Continue enabled after saving story progress");
        string original=PlayerPrefs.GetString(StoryProgress.SaveKey);
        Button("CONTINUE GAME").onClick.Invoke();
        Check(!session.NewGameRequested && PlayerPrefs.GetString(StoryProgress.SaveKey)==original,"Continue preserves the existing save");
        session.BeginLocal(true);session.Ready(0);Check(session.CanStart,"Continue enters the existing single-player character flow");session.StartGame();
        yield return Until(()=>PrologueDirector.Active && PrologueDirector.Active.ActiveStory && PrologueDirector.Active.Phase==1,"Continue loads the saved fair-walk checkpoint");
        Check(StoryProgress.Load().Has("ValidationResumeMarker"),"Continue retains saved story flags");
        session.LeaveToMenu();yield return Until(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","Return to the main menu");yield return null;
        menu.Refresh();Button("NEW GAME").onClick.Invoke();
        string beforeNewGame=PlayerPrefs.GetString(StoryProgress.SaveKey);
        Check(session.NewGameRequested && StoryProgress.Load().Has("ValidationResumeMarker"),"Selecting New Game defers reset until gameplay starts");
        Button("BACK").onClick.Invoke();Check(PlayerPrefs.GetString(StoryProgress.SaveKey)==beforeNewGame,"Backing out of New Game keeps the old save");
        Button("NEW GAME").onClick.Invoke();session.BeginLocal(true);session.Ready(0);session.StartGame();
        yield return Until(()=>PrologueDirector.Active && PrologueDirector.Active.ActiveStory && PrologueDirector.Active.Cutscenes.Current==PrologueDirector.Active.Definition.reunion,"New Game starts the opening reunion cutscene");
        Check(!StoryProgress.Load().Has("ValidationResumeMarker") && PrologueDirector.Active.Progress.checkpoint==0 && !session.NewGameRequested,"New Game resets only the story and consumes the restart request");
        session.LeaveToMenu();yield return Until(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","New game can return to the main menu");yield return null;
        Directory.CreateDirectory("Documentation/ProloguePreview");ScreenCapture.CaptureScreenshot("Documentation/ProloguePreview/NewGameContinueMenu.png");yield return null;
    }
    static void Finish()
    {
        stack.Clear();string original=SessionState.GetString(Pending+".Backup","");
        if(string.IsNullOrEmpty(original))StoryProgress.Reset();else { PlayerPrefs.SetString(StoryProgress.SaveKey,original);PlayerPrefs.Save(); }
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/StoryMenuValidationResults.txt",results);
        Debug.Log(string.Join("\n",results));EditorApplication.ExitPlaymode();
    }
}
