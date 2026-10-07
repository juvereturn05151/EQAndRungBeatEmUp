using UnityEngine;

namespace BeatEmUp
{
    // Uses the existing combat timeline and accepted-hit history, never Animation Events.
    [DisallowMultipleComponent]
    public sealed class AttackFeedback : MonoBehaviour
    {
        // Client cosmetics are triggered once from accepted authoritative cues, not simulated hits.
        static readonly System.Collections.Generic.Dictionary<int, AudioSource> remoteWarnings = new System.Collections.Generic.Dictionary<int, AudioSource>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRemoteWarnings() => remoteWarnings.Clear();
        public static void PlayRemote(AttackData attack, Vector2 point, int facing, bool impact, string signal = null, int sourceId = 0)
        {
            PlayRemoteFeedback(attack ? attack.feedback : null, point, facing, impact, signal, sourceId);
        }
        public static void PlayRemoteFeedback(AttackFeedbackData data, Vector2 point, int facing, bool impact, string signal = null, int sourceId = 0)
        {
            if(data==null) return;
            if (signal == "StopArea" || signal == "Scream" || signal == "Telegraph")
            {
                if (remoteWarnings.TryGetValue(sourceId, out var warning) && warning) { warning.Stop(); Destroy(warning.gameObject); }
                remoteWarnings.Remove(sourceId);
                if (signal == "StopArea") return;
            }
            bool diveStart = signal == "DiveWhoosh", landing = signal == "DiveLanding";
            bool swing = !impact && (signal == "Swing" || string.IsNullOrEmpty(signal));
            var clip=landing ? data.landingSound : signal=="Telegraph" ? data.telegraphSound : signal=="Scream" ? data.screamSound : impact ? data.impactSound : data.swingSound;
            if(clip)
            {
                var sound=new GameObject("Network combat sound"); sound.transform.position=point;
                var source=sound.AddComponent<AudioSource>(); source.spatialBlend=0; source.volume=landing ? data.landingVolume : diveStart ? data.swingVolume : !string.IsNullOrEmpty(signal) && signal!="Swing" ? data.areaVolume : impact ? data.impactVolume : data.swingVolume;
                source.clip=clip; source.Play(); Destroy(sound,clip.length+.1f);
                if (signal == "Telegraph") remoteWarnings[sourceId] = source;
            }
            var prefab = landing ? data.landingPrefab : diveStart ? data.diveStartPrefab : impact ? data.impactPrefab : swing ? data.swingPrefab : null;
            if (!prefab) return;
            float scale = landing ? data.landingScale : diveStart ? data.diveStartScale : swing ? data.swingScale : data.impactScale;
            float lifetime = landing ? data.landingLifetime : diveStart ? data.diveStartLifetime : swing ? data.swingLifetime : data.impactLifetime;
            float sortingY = point.y;
            if (swing) point += new Vector2(data.swingOffset.x * facing, data.swingOffset.y);
            var container=new GameObject("Network combat impact"); container.SetActive(false); container.transform.position=point;
            container.AddComponent<NetworkFeedbackVisual>();
            var effect=Instantiate(prefab,container.transform);
            effect.transform.localPosition=Vector3.zero; effect.transform.localScale*=scale;
            effect.transform.localRotation=Quaternion.Euler(0,0,swing ? data.swingRotation : !landing && data.impactRotateWithFacing && facing<0 ? 180 : 0);
            if (swing && data.swingMirrorWithFacing && facing < 0) container.transform.localScale = new Vector3(-1, 1, 1);
            foreach(var cfx in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
            { if(cfx.cameraShake!=null) cfx.cameraShake.enabled=false; cfx.animatedLights=new CartoonFX.CFXR_Effect.AnimatedLight[0]; }
            foreach(var light in effect.GetComponentsInChildren<Light>(true)) light.enabled=false;
            foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=particles.main; main.loop=false; main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                var renderer=particles.GetComponent<ParticleSystemRenderer>(); if(renderer) renderer.sortingOrder=Mathf.RoundToInt(-sortingY*100)+2;
            }
            container.SetActive(true); Destroy(container,lifetime);
        }
        AttackPlayer player;
        AttackHitbox hitbox;
        ComboController defender;
        AttackData lastAttack;
        int lastSwingFrame = -1, lastImpactFrame = -1;
        public int DiveStartCount { get; private set; }
        public int LandingCount { get; private set; }
        int lastDiveStartFrame = -1, lastLandingFrame = -1;
        public int SwingCount { get; private set; }
        public int ImpactCount { get; private set; }
        public GameObject LastImpact { get; private set; }
        public GameObject LastSwing { get; private set; }
        public AudioSource LastSound { get; private set; }
        public SpriteRenderer WarningVisual { get; private set; }
        public SpriteRenderer WaveVisual { get; private set; }
        public int TelegraphCount { get; private set; }
        public int ScreamCount { get; private set; }
        public int ParryCount { get; private set; }
        AudioSource warningSound;
        SpriteRenderer activeBoundary;
        SpriteRenderer waveMirror;
        static Sprite pixelRing;
        void OnEnable()
        {
            player = GetComponent<AttackPlayer>(); hitbox = GetComponent<AttackHitbox>();
            defender = GetComponent<ComboController>(); if (defender) defender.DefenseImpact += DefenseImpact;
            player.FrameEvent += FrameEvent; hitbox.HitConfirmed += HitConfirmed; player.Finished += Finished; player.Started += Finished;
            player.Stopped += Finished; player.FrameApplied += UpdateArea;
        }
        void OnDisable()
        {
            if (player) { player.FrameEvent -= FrameEvent; player.Finished -= Finished; player.Started -= Finished; }
            if (player) { player.Stopped -= Finished; player.FrameApplied -= UpdateArea; }
            if (hitbox) hitbox.HitConfirmed -= HitConfirmed;
            if (defender) defender.DefenseImpact -= DefenseImpact;
            ResetHistory(); ClearArea();
        }
        void Finished(AttackData _) { ResetHistory(); ClearArea(); }
        void ClearWarningSound()
        {
            if (warningSound) { warningSound.Stop(); Destroy(warningSound.gameObject); warningSound = null; }
        }
        void ClearArea()
        {
            ClearWarningSound();
            if (WarningVisual) { WarningVisual.gameObject.SetActive(false); Destroy(WarningVisual.gameObject); WarningVisual = null; }
            if (WaveVisual) { WaveVisual.gameObject.SetActive(false); Destroy(WaveVisual.gameObject); WaveVisual = null; }
            if (activeBoundary) { activeBoundary.gameObject.SetActive(false); Destroy(activeBoundary.gameObject); activeBoundary = null; }
            if (waveMirror) { waveMirror.gameObject.SetActive(false); Destroy(waveMirror.gameObject); waveMirror = null; }
        }
        static Sprite Ring()
        {
            if (pixelRing) return pixelRing;
            // Code-native, point-filtered pixel outline, shared by every area feedback instance.
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Combat pixel ring" };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                texture.SetPixel(x, y, radius >= .91f && radius <= 1 ? Color.white : Color.clear);
            }
            texture.Apply();
            return pixelRing = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        }
        SpriteRenderer CreateRing(string label, Sprite sprite)
        {
            var go = new GameObject(label); go.transform.SetParent(player.motor.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite ? sprite : Ring();
            renderer.sortingLayerID = player.motor.sprite ? player.motor.sprite.sortingLayerID : 0;
            return renderer;
        }
        public void UpdateArea()
        {
            var attack = player.CurrentAttack;
            if (attack && attack.feedback != null && attack.feedback.directionalWaveWarning) { UpdateDirectionalWarning(attack); return; }
            if (!attack || attack.feedback == null || !attack.feedback.areaWarning || attack.FirstActiveFrame < 0) { ClearArea(); return; }
            var frame = player.CurrentFrame;
            var box = attack.frames[attack.FirstActiveFrame].hitboxes.Find(h => h != null && h.groundArea);
            if (box == null || frame > attack.LastActiveFrame) { ClearArea(); return; }
            var data = attack.feedback;
            bool warning = frame < attack.FirstActiveFrame;
            if (warning && !WarningVisual) WarningVisual = CreateRing("Telegraph warning", data.areaRingSprite);
            if (!warning && WarningVisual) { WarningVisual.gameObject.SetActive(false); Destroy(WarningVisual.gameObject); WarningVisual = null; }
            if (!warning && !activeBoundary) activeBoundary = CreateRing("Active area boundary", data.areaRingSprite);
            var boundary = warning ? WarningVisual : activeBoundary;
            var position = new Vector3(box.offset.x * player.Facing, box.offset.y, -.05f);
            boundary.transform.localPosition = position;
            var bounds = boundary.sprite.bounds.size;
            boundary.transform.localScale = new Vector3(box.size.x / bounds.x, Mathf.Min(box.size.y, box.laneTolerance * 2) / bounds.y, 1);
            var color = warning ? data.warningColor : data.waveColor;
            // Pulse opacity only; the warning boundary continues to describe the complete damage area.
            color.a *= warning ? .65f + .35f * Mathf.Sin(frame * Mathf.PI / 8) : .65f;
            boundary.color = color;
            boundary.sortingOrder = (player.motor.sprite ? player.motor.sprite.sortingOrder : 0) + 1;
            if (warning) return;
            ClearWarningSound();
            if (!WaveVisual) WaveVisual = CreateRing("Active sound wave", data.areaRingSprite);
            WaveVisual.transform.localPosition = position;
            float progress = (frame - attack.FirstActiveFrame + 1f) / Mathf.Max(1, attack.LastActiveFrame - attack.FirstActiveFrame + 1);
            if (data.areaWaveSprites != null && data.areaWaveSprites.Length > 0)
            {
                var sprite = data.areaWaveSprites[Mathf.Min(data.areaWaveSprites.Length - 1, Mathf.FloorToInt(progress * data.areaWaveSprites.Length))];
                if (sprite)
                {
                    WaveVisual.sprite = sprite;
                    if (!waveMirror) waveMirror = CreateRing("Mirrored active sound wave", sprite);
                    waveMirror.sprite = sprite; waveMirror.flipX = true;
                }
            }
            var waveSize = WaveVisual.sprite.bounds.size;
            WaveVisual.transform.localScale = new Vector3(box.size.x / waveSize.x, Mathf.Min(box.size.y, box.laneTolerance * 2) / waveSize.y, 1) * Mathf.Lerp(.15f, 1, progress);
            WaveVisual.color = data.waveColor; WaveVisual.sortingOrder = boundary.sortingOrder + 1;
            if (waveMirror)
            {
                waveMirror.transform.localPosition = position; waveMirror.transform.localScale = WaveVisual.transform.localScale;
                waveMirror.color = data.waveColor; waveMirror.sortingOrder = WaveVisual.sortingOrder;
            }
        }
        void UpdateDirectionalWarning(AttackData attack)
        {
            var spawn = GetComponent<EnemyProjectileAttack>();
            int release = spawn ? attack.frames.FindIndex(f => f != null && f.events.Contains(spawn.releaseEvent)) : -1;
            if (!spawn || !spawn.projectilePrefab || release < 0 || player.CurrentFrame >= release) { ClearArea(); return; }
            var shot = spawn.projectilePrefab;
            float travel = shot.speed * shot.lifetimeFrames * CombatClock.FrameSeconds;
            if (shot.maximumTravelDistance > 0) travel = Mathf.Min(travel, shot.maximumTravelDistance);
            float start = Mathf.Max(.01f, spawn.releaseOffset.x - shot.waveSize.x * .5f);
            float end = spawn.releaseOffset.x + travel + shot.waveSize.x * .5f;
            if (!WarningVisual) WarningVisual = CreateRing("Directional scream warning", attack.feedback.areaRingSprite);
            WarningVisual.transform.localPosition = new Vector3((start + end) * .5f * player.Facing, 0, -.05f);
            var bounds = WarningVisual.sprite.bounds.size;
            WarningVisual.transform.localScale = new Vector3((end - start) / bounds.x, shot.waveSize.y / bounds.y, 1);
            WarningVisual.flipX = player.Facing < 0;
            var color = attack.feedback.warningColor; color.a *= .7f + .3f * Mathf.Sin(player.CurrentFrame * Mathf.PI / 8);
            WarningVisual.color = color; WarningVisual.sortingOrder = (player.motor.sprite ? player.motor.sprite.sortingOrder : 0) + 1;
        }
        void ResetHistory() { lastAttack = null; lastSwingFrame = lastImpactFrame = lastDiveStartFrame = lastLandingFrame = -1; }
        void Prepare()
        {
            // Started resets repeated assets; this also handles an explicitly rewound timeline.
            if (lastAttack != player.CurrentAttack || player.CurrentFrame < Mathf.Max(lastSwingFrame, lastImpactFrame))
            { ResetHistory(); lastAttack = player.CurrentAttack; }
        }
        void FrameEvent(string signal)
        {
            if ((signal == "Telegraph" || signal == "Scream") && player.CurrentAttack && !CombatClock.IsPaused)
            {
                var area = player.CurrentAttack.feedback;
                if (area == null) return;
                if (signal == "Telegraph") { TelegraphCount++; PlaySound(area.telegraphSound, area.areaVolume, player.motor.transform.position); warningSound = area.telegraphSound ? LastSound : null; }
                else { ClearWarningSound(); ScreamCount++; PlaySound(area.screamSound, area.areaVolume, player.motor.transform.position); }
                return;
            }
            if ((signal == "DiveWhoosh" || signal == "DiveLanding") && player.CurrentAttack && !CombatClock.IsPaused)
            {
                Prepare();
                bool landing = signal == "DiveLanding";
                if ((landing ? lastLandingFrame : lastDiveStartFrame) == player.CurrentFrame) return;
                var cue = player.CurrentAttack.feedback;
                if (cue == null) return;
                if (landing) { lastLandingFrame = player.CurrentFrame; LandingCount++; }
                else { lastDiveStartFrame = player.CurrentFrame; DiveStartCount++; }
                var point = (Vector2)player.motor.transform.position + new Vector2(0, landing ? 0 : player.motor.Height + .3f);
                PlaySound(landing ? cue.landingSound : cue.swingSound, landing ? cue.landingVolume : cue.swingVolume, point);
                LastImpact = SpawnImpact(point, landing ? cue.landingPrefab : cue.diveStartPrefab,
                    landing ? cue.landingScale : cue.diveStartScale, landing ? cue.landingLifetime : cue.diveStartLifetime,
                    !landing && cue.impactRotateWithFacing);
                return;
            }
            if (signal != "Swing" || !player.CurrentAttack || CombatClock.IsPaused) return;
            Prepare();
            if (lastSwingFrame == player.CurrentFrame) return;
            lastSwingFrame = player.CurrentFrame;
            var data = player.CurrentAttack.feedback;
            if (data == null) return;
            SwingCount++; PlaySound(data.swingSound, data.swingVolume, player.motor.transform.position);
            if (data.swingPrefab)
            {
                var point = (Vector2)player.motor.transform.position + new Vector2(data.swingOffset.x * player.Facing, data.swingOffset.y);
                LastSwing = SpawnImpact(point, data.swingPrefab, data.swingScale, data.swingLifetime, false, data.swingRotation, data.swingMirrorWithFacing);
            }
        }
        void HitConfirmed(Vector2 point, CombatHitOutcome outcome)
        {
            if (outcome != CombatHitOutcome.Hit || !player.CurrentAttack || CombatClock.IsPaused) return;
            Prepare();
            // Multiple enemies/colliders hit on one frame share one cue to avoid a loud visual pile-up.
            if (lastImpactFrame == player.CurrentFrame) return;
            lastImpactFrame = player.CurrentFrame;
            var data = player.CurrentAttack.feedback;
            if (data == null) return;
            ImpactCount++; PlaySound(data.impactSound, data.impactVolume, point);
            SpawnImpact(data, point);
        }
        void DefenseImpact(DefenseFeedback feedback)
        {
            if (feedback != DefenseFeedback.Parry || CombatClock.IsPaused || !defender.defenseData) return;
            ParryCount++;
            var data = defender.defenseData.parryFeedback; if (data == null) return;
            var point = (Vector2)player.motor.transform.position + new Vector2(player.motor.Facing * .45f, .65f);
            PlaySound(data.impactSound, data.impactVolume, point); SpawnImpact(data, point);
        }
        void SpawnImpact(AttackFeedbackData data, Vector2 point)
            => LastImpact = SpawnImpact(point, data.impactPrefab, data.impactScale, data.impactLifetime, data.impactRotateWithFacing);
        GameObject SpawnImpact(Vector2 point, GameObject prefab, float scale, float lifetime, bool rotate, float rotation = 0, bool mirror = false)
        {
            if (!prefab) return null;
            var container = new GameObject("Combat impact (temporary)"); container.SetActive(false);
            container.transform.position = new Vector3(point.x, point.y, -.1f);
            var effect = Instantiate(prefab, container.transform);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.Euler(0, 0, rotation + (rotate && player.motor.Facing < 0 ? 180 : 0));
            if (mirror && player.Facing < 0) container.transform.localScale = new Vector3(-1, 1, 1);
            effect.transform.localScale *= scale;
            // Configure before activation; disable library camera shake and scene lights per instance.
            foreach (var cfx in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
            {
                if (cfx.cameraShake != null) cfx.cameraShake.enabled = false;
                cfx.animatedLights = new CartoonFX.CFXR_Effect.AnimatedLight[0];
            }
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main; main.loop = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                if (renderer)
                {
                    renderer.sortingLayerID = player.motor.sprite ? player.motor.sprite.sortingLayerID : 0;
                    renderer.sortingOrder = (player.motor.sprite ? player.motor.sprite.sortingOrder : 0) + 2;
                }
            }
            container.SetActive(true);
            Destroy(container, lifetime);
            return container;
        }
        void PlaySound(AudioClip clip, float volume, Vector3 point)
        {
            if (!clip || volume <= 0) return;
            var sound = new GameObject("Combat sound (temporary)"); sound.transform.position = point;
            var source = sound.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0; source.volume = Mathf.Clamp01(volume); source.clip = clip;
            LastSound = source; source.Play(); Destroy(sound, clip.length + .1f);
        }
    }
}
