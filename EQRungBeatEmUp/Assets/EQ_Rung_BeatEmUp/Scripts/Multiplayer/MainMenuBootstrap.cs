using UnityEngine;

namespace BeatEmUp
{
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        public MultiplayerCatalog catalog;
        void Awake()
        {
            if(MultiplayerSession.Active) return;
            var root=new GameObject("Ghost Fair session"); root.AddComponent<MultiplayerSession>().catalog=catalog;
            root.AddComponent<MultiplayerMenu>();
        }
    }
}
