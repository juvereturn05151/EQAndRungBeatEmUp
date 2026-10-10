using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp.Story
{
    [Serializable] public sealed class StoryProgress
    {
        public const string SaveKey = "EQRung.Story.v1";
        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);
        public int version = 1, checkpoint, tutorialStep;
        public List<string> flags = new List<string>();
        public bool Has(string flag) => flags.Contains(flag);
        public void Set(string flag) { if(!string.IsNullOrEmpty(flag) && !Has(flag)) flags.Add(flag); }
        public static StoryProgress Load()
        {
            try { var data=JsonUtility.FromJson<StoryProgress>(PlayerPrefs.GetString(SaveKey,"")); if(data!=null && data.version==1) { data.flags ??= new List<string>(); data.checkpoint=Mathf.Clamp(data.checkpoint,0,15); data.tutorialStep=Mathf.Clamp(data.tutorialStep,0,9); return data; } }
            catch(Exception e) { Debug.LogWarning("Story checkpoint could not be read: "+e.Message); }
            return new StoryProgress();
        }
        public void Save() { PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static void Reset() { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
    }
    [Serializable] public sealed class StorySnapshot
    {
        public bool active, cinematic, paused;
        public int checkpoint, tutorialStep, line=-1, revision;
        public string conversation, instruction, objective;
        public Vector3 camera;
        public float zoom, fade;
        public Color fadeColor;
        public StoryProgress progress;
        public float bossHp, bossMax;
        public StoryMagicCue[] magic = Array.Empty<StoryMagicCue>();
    }
    [Serializable] public sealed class StoryMagicCue
    {
        public int id;
        public Vector3 position;
        public Color color;
        public float duration, size, expires;
    }
}
