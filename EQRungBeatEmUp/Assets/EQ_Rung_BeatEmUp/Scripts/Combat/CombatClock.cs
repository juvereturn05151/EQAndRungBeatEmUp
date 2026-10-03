using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public interface ICombatFrameListener
    {
        int FrameOrder { get; }
        void CombatFrame();
    }

    [DefaultExecutionOrder(-20)]
    public sealed class CombatClock : MonoBehaviour
    {
        [Min(1)] public int combatFPS = 60;
        public long FrameNumber { get; private set; }
        public static bool IsStepping { get; private set; }
        public static long CurrentTick => instance ? instance.FrameNumber : 0;
        private double accumulator;
        private static CombatClock instance;
        private static readonly List<ICombatFrameListener> listeners = new List<ICombatFrameListener>();
        public static float FrameSeconds => 1f / (instance ? Mathf.Max(1, instance.combatFPS) : 60);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; listeners.Clear(); IsStepping = false; }
        public static void Register(ICombatFrameListener listener)
        {
            if (!Application.isPlaying) return;
            if (!instance)
            {
                instance = FindFirstObjectByType<CombatClock>();
                if (!instance) instance = new GameObject("Combat Clock (60 FPS)").AddComponent<CombatClock>();
            }
            if (!listeners.Contains(listener)) listeners.Add(listener);
            listeners.Sort((a, b) => a.FrameOrder.CompareTo(b.FrameOrder));
        }
        public static void Unregister(ICombatFrameListener listener) => listeners.Remove(listener);
        private void Awake()
        {
            if (instance && instance != this) { enabled = false; Debug.LogWarning("Only one CombatClock can run.", this); return; }
            instance = this;
        }
        private void OnDestroy() { if (instance == this) instance = null; }
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            accumulator += Mathf.Max(0, seconds);
            double step = 1.0 / Mathf.Max(1, combatFPS);
            // Never discard accumulated combat frames during slow rendered frames.
            while (accumulator + 1e-9 >= step) { accumulator -= step; StepFrame(); }
        }
        public void StepFrame()
        {
            FrameNumber++;
            IsStepping = true;
            var snapshot = listeners.ToArray();
            foreach (var listener in snapshot)
                if (listener is AttackPlayer attack && attack && attack.isActiveAndEnabled) attack.PrepareFrame();
            foreach (var listener in snapshot)
                if (listener is MonoBehaviour component && component && component.isActiveAndEnabled) listener.CombatFrame();
            foreach (var listener in snapshot)
                if (listener is AttackPlayer attack && attack && attack.isActiveAndEnabled) attack.EndClockFrame();
            IsStepping = false;
        }
    }
}
