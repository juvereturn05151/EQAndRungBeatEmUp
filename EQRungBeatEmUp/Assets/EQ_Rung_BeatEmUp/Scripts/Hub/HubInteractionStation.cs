using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    // A real foreground object; interaction still belongs to PlayerHubController / StageFlow.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CircleCollider2D))]
    public sealed class HubInteractionStation : MonoBehaviour
    {
        static readonly HashSet<HubInteractionStation> stations = new HashSet<HubInteractionStation>();
        public PlayerHubDefinition definition;
        [Range(0,3)] public int stationIndex;
        public string displayName;
        public SpriteRenderer body, outline, groundCue, indicator;
        public BoxCollider2D footprint;
        [Min(.1f)] public float indicatorHeight = 1.65f;
        [Range(0,1)] public float idleOutlineAlpha = .22f, nearbyOutlineAlpha = .85f;
        public Color cueColor = new Color(.35f,1,.9f,1);
        public bool HasNearbyPlayer { get; private set; }
        public Vector2 GroundPosition => definition && stationIndex < definition.stations.Length ? definition.stations[stationIndex] : (Vector2)transform.position;
        public float InteractionRadius => definition ? definition.interactionRadius : 1.2f;
        MaterialPropertyBlock outlineProperties;
        CircleCollider2D trigger;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => stations.Clear();
        void OnEnable() { stations.Add(this); RefreshPresentation(); }
        void OnDisable() { stations.Remove(this); HasNearbyPlayer=false; }
        public static HubInteractionStation Find(PlayerHubDefinition definition,int index) => stations.FirstOrDefault(s=>s && s.isActiveAndEnabled && s.definition==definition && s.stationIndex==index);
        public bool Contains(Vector2 point) => isActiveAndEnabled && Vector2.Distance(point,GroundPosition)<=InteractionRadius;
        public bool CanInteract(CharacterMotor actor)
        {
            if(!actor || !Contains(actor.transform.position) || !actor.IsGrounded || CombatClock.IsPaused || actor.attackPlayer && actor.attackPlayer.CurrentAttack) return false;
            var health=actor.GetComponent<CharacterHealth>();
            return health && !health.IsDead && actor.GetComponent<MetaProgress>()?.OpenStation<0;
        }
        public bool TryInteract(CharacterMotor actor)
        {
            if(!CanInteract(actor)) return false;
            var hub=FindFirstObjectByType<PlayerHubController>();
            return hub && hub.definition==definition && hub.InHub && hub.Interact(actor);
        }
        void LateUpdate() => RefreshPresentation();
        public void RefreshPresentation()
        {
            transform.position=GroundPosition;
            if(!trigger) trigger=GetComponent<CircleCollider2D>();
            trigger.isTrigger=true; trigger.radius=InteractionRadius; trigger.offset=Vector2.zero;
            HasNearbyPlayer=false;
            if(Application.isPlaying)
            {
                var hub=FindFirstObjectByType<PlayerHubController>();
                if(hub && hub.InHub && hub.definition==definition)
                    HasNearbyPlayer=FindObjectsByType<CharacterMotor>().Any(actor=>actor.GetComponent<ComboController>() && CanInteract(actor));
            }
            float time=Application.isPlaying ? Time.time : 0;
            float pulse=.85f+.15f*Mathf.Sin(time*2.2f);
            int order=Mathf.RoundToInt(-(GroundPosition.y+.24f)*100);
            if(body) body.sortingOrder=order;
            if(outline)
            {
                outline.sortingOrder=order-1;
                if(outlineProperties==null) outlineProperties=new MaterialPropertyBlock();
                var color=cueColor; color.a=(HasNearbyPlayer?nearbyOutlineAlpha:idleOutlineAlpha)*pulse;
                outlineProperties.SetColor("_OutlineColor",color); outline.SetPropertyBlock(outlineProperties);
            }
            if(groundCue)
            {
                groundCue.sortingOrder=order-2;
                var color=cueColor;color.a=HasNearbyPlayer?.7f:.2f;groundCue.color=color;
            }
            if(indicator)
            {
                indicator.enabled=HasNearbyPlayer;
                indicator.sortingOrder=order+3;
                indicator.transform.localPosition=new Vector3(0,indicatorHeight+.025f*Mathf.Sin(time*3),0);
            }
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=cueColor;Gizmos.DrawWireSphere(GroundPosition,InteractionRadius);
        }
    }
}
