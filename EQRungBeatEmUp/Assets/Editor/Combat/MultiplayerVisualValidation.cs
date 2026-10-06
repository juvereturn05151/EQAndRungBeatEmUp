using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class MultiplayerVisualValidation
{
    const string Pending="GhostFair.VisualValidation";
    static int step;
    static double due;
    static MultiplayerVisualValidation() { EditorApplication.update+=Poll; }
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorSceneManager.OpenScene(MultiplayerSetup.MenuPath); SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || !MultiplayerSession.Active) return;
        if(due==0) { due=EditorApplication.timeSinceStartup+1; step=0; }
        if(EditorApplication.timeSinceStartup<due) return;
        try
        {
            if(step==0)
            {
                Capture("MainMenu"); var session=MultiplayerSession.Active;
                InputSystem.AddDevice<Keyboard>(); session.BeginLocal();
                for(int i=0;i<3;i++) session.JoinDevice(InputSystem.AddDevice<Gamepad>());
                foreach(var slot in session.Lobby.slots) session.Ready(slot.slot);
                step=1; due=EditorApplication.timeSinceStartup+1;
            }
            else { Capture("LocalLobby4"); Finish(0); }
        }
        catch(System.Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Pending,false); due=0;
        if(Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
    static void Capture(string name)
    {
        var canvas=MultiplayerSession.Active.GetComponentInChildren<Canvas>(); var camera=Camera.main;
        var target=new RenderTexture(1280,720,24); var old=RenderTexture.active;
        try
        {
            camera.targetTexture=target; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/MultiplayerPreview"); File.WriteAllBytes("Documentation/MultiplayerPreview/"+name+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; RenderTexture.active=old; target.Release(); Object.DestroyImmediate(target); }
    }
}
