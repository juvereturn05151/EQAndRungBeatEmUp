using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp.Story
{
    public enum StoryAction { Wait, Dialogue, Move, Face, Pose, Fade, Camera, Environment, Spawn, Despawn, Vfx, Sound, Flag, Signal, CameraShake, Music, Gameplay, StageTransition }
    [Serializable] public class CutsceneEvent
    {
        public StoryAction action;
        public string target, value;
        public Vector3 position;
        public float duration = .5f, amount = 1;
        [Tooltip("Ease into and out of a timeline walk.")] public bool easeMovement;
        [Min(.01f)] public float walkPlaybackSpeed = 1;
        [Tooltip("Keep the actor looking at a partner while moving backwards.")] public bool preserveFacing;
        public Color color = Color.white;
        public Sprite sprite;
        public Sprite[] frames;
        [Min(.01f)] public float framesPerSecond = 12;
        public bool holdLastFrame;
        public AudioClip sound;
        public AnimationClip animation;
        public GameObject prefab;
        [Tooltip("Consecutive events sharing a positive group run in parallel.")] public int parallelGroup;
    }
    [CreateAssetMenu(menuName="Beat Em Up/Story/Cutscene sequence")]
    public sealed class CutsceneSequence : ScriptableObject
    {
        public string id;
        public bool playsOnce = true;
        public List<CutsceneEvent> events = new List<CutsceneEvent>();
        [Tooltip("Applied on completion AND skip. Put required flags and final poses here.")]
        public List<CutsceneEvent> finalEvents = new List<CutsceneEvent>();
    }
}
