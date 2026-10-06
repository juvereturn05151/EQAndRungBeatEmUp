using UnityEngine;
namespace BeatEmUp
{
    public sealed class HubLandmarks : MonoBehaviour
    {
        public PlayerHubDefinition definition;
        public bool editorPreview;
        void Awake() { if(editorPreview) gameObject.SetActive(false); }
        void OnDrawGizmosSelected()
        {
            if(!definition) return;
            Gizmos.color=Color.green; Gizmos.DrawWireCube(new Vector3(0,.125f,0),new Vector3(definition.width-1,1.05f,0));
            Gizmos.color=Color.cyan; Gizmos.DrawWireCube(new Vector3(0,1.36f,0),new Vector3(definition.width,4,0));
            Gizmos.DrawWireSphere(definition.spawn,.25f);
            foreach(var station in definition.stations) { Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(station,definition.interactionRadius); }
        }
    }
}
