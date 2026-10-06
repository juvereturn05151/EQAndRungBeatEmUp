using UnityEngine;
namespace BeatEmUp
{
    [RequireComponent(typeof(DestructibleObject))]
    public sealed class BossTotem : MonoBehaviour, ICombatFrameListener
    {
        public TotemBossController Boss { get; private set; }
        public BossEncounterData Data { get; private set; }
        public DestructibleObject Prop { get; private set; }
        public int RespawnRemaining { get; private set; }
        public float BreakRadius => Prop.totemBreakRadius > 0 ? Prop.totemBreakRadius : Data.breakWaveRadius;
        public int FrameOrder => 4;
        TotemBreakWave wave;
        bool stopped;
        void Awake() { Prop = GetComponent<DestructibleObject>(); }
        void OnEnable() { Prop.Broken += Broken; CombatClock.Register(this); }
        void OnDisable() { Prop.Broken -= Broken; CombatClock.Unregister(this); StopEncounter(); }
        public void Bind(TotemBossController boss, BossEncounterData data) { Boss = boss; Data = data; stopped = false; }
        void Broken(DestructibleObject _)
        {
            if (stopped || !Boss || !Data) return;
            if (Data.totemBreakFeedback)
            {
                AttackFeedback.PlayRemote(Data.totemBreakFeedback, transform.position, 1, true);
                MultiplayerSession.Active?.QueueEncounterFeedback(Data.totemBreakFeedback, transform.position, 1, GetInstanceID());
            }
            var go = new GameObject("Totem break wave"); go.transform.SetParent(transform.parent, false); go.transform.position = transform.position;
            wave = go.AddComponent<TotemBreakWave>(); wave.Initialize(this);
            RespawnRemaining = Data.totemMode == BossTotemMode.Respawn ? Mathf.Max(1, Data.totemRespawnFrames) : 0;
        }
        public void CombatFrame()
        {
            if (CombatClock.IsPaused || stopped || !Data || !Boss || Boss.State == BossEncounterState.Dead) return;
            if (RespawnRemaining > 0 && --RespawnRemaining == 0) Prop.Respawn(Data.totemRespawnHP);
        }
        public void StopEncounter() { stopped = true; RespawnRemaining = 0; if (wave) { wave.gameObject.SetActive(false); Destroy(wave.gameObject); } }
#if UNITY_EDITOR
        public void DebugBreak(CharacterMotor attacker) { if (Application.isPlaying) Prop.Receive(new AttackHitboxData { damage = Prop.maximumHealth, laneTolerance = 100 }, 1, attacker); }
#endif
    }
}
