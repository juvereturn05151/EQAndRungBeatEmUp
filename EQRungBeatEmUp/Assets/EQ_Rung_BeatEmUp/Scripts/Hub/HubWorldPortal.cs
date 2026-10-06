using UnityEngine;

namespace BeatEmUp
{
    // Presentation only. PlayerHubController still owns interaction and stage entry.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HubWorldPortal : MonoBehaviour
    {
        public PlayerHubDefinition definition;
        [Tooltip("Visual availability only; no world-lock progression system.")]
        public bool unlocked=true;
        public GameObject runicAuraReference;
        public Transform aura;
        public TextMesh entranceLabel;
        public Vector3 vfxScale=new Vector3(.9f,.9f,.9f);
        [Min(0)] public float visualHeight=1.05f, labelHeight=2.25f;
        public string sortingLayer="Default";
        public int sortingOffset=-1;
        public Vector2 GroundPosition=>definition && definition.stations.Length>3 ? definition.stations[3] : (Vector2)transform.position;
        public float InteractionRadius=>definition ? definition.interactionRadius : 1.2f;
        void OnEnable()=>RefreshPresentation();
        void LateUpdate()=>RefreshPresentation();
        void OnValidate()=>RefreshPresentation();
        public void RefreshPresentation()
        {
            var ground=GroundPosition; transform.position=new Vector3(ground.x,ground.y,0);
            if(aura)
            {
                aura.gameObject.SetActive(unlocked);
                aura.localPosition=new Vector3(0,visualHeight,0);
                aura.localRotation=Quaternion.Euler(90,0,0);
                aura.localScale=vfxScale;
                int order=Mathf.RoundToInt(-ground.y*100)+sortingOffset;
                foreach(var renderer in aura.GetComponentsInChildren<ParticleSystemRenderer>(true))
                { renderer.sortingLayerName=sortingLayer; renderer.sortingOrder=order; }
            }
            if(entranceLabel)
            {
                entranceLabel.transform.localPosition=new Vector3(0,labelHeight,0);
                entranceLabel.text="WORLD 1\nENTRANCE";
                var renderer=entranceLabel.GetComponent<MeshRenderer>(); renderer.sortingLayerName=sortingLayer; renderer.sortingOrder=200;
            }
        }
        void OnDrawGizmos()
        {
            Gizmos.color=unlocked ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(GroundPosition,InteractionRadius);
            Gizmos.DrawLine(GroundPosition,(Vector3)GroundPosition+Vector3.up*visualHeight);
#if UNITY_EDITOR
            UnityEditor.Handles.Label((Vector3)GroundPosition+Vector3.up*(labelHeight+.35f),"WORLD 1 ENTRANCE");
#endif
        }
    }
}
