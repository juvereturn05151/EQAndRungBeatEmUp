using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class MultiplayerSession
    {
        readonly Dictionary<ulong,MetaProfile> remoteMeta=new Dictionary<ulong,MetaProfile>();
        string savedMeta;
        PlayerHubDefinition HubDefinition=>catalog && catalog.level && catalog.level.stages.Count>0 ? catalog.level.stages[0].hub : null;
        void InitializeHubPreference()
        {
            if(!catalog || !HubDefinition) return;
            var profile=MetaSave.Load(0,HubDefinition);
            int selected=System.Array.FindIndex(catalog.characters,c=>c.characterId==profile.characterId);
            PreferredCharacter=Mathf.Max(0,selected);
        }
        void RegisterHubMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"meta",(sender,reader)=>
            {
                if(!IsAuthority || Lobby.running || !Lobby.slots.Any(s=>s.owner==sender) || !HubDefinition) return;
                var profile=JsonUtility.FromJson<MetaProfile>(Read(reader)); if(profile==null) return; profile.Validate(HubDefinition); remoteMeta[sender]=profile;
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"hub",(sender,reader)=>
            {
                if(!IsAuthority || !InGame || !sceneReady) return;
                var request=JsonUtility.FromJson<HubRequest>(Read(reader));
                if(request==null || !characters.TryGetValue(request.slot,out var player) || player.owner!=sender) return;
                Flow.GetComponent<PlayerHubController>()?.Execute(player.Motor,request.action,request.index);
            });
        }
        void InitializePlayerMeta(PlayerIdentity player)
        {
            if(!HubDefinition) return;
            bool local=IsLocalOwner(player.owner);
            int slot=Mode==SessionMode.Local ? player.slot : 0;
            var profile=local ? MetaSave.Load(slot,HubDefinition) : remoteMeta.TryGetValue(player.owner,out var data) ? data : new MetaProfile{essence=HubDefinition.startingEssence};
            profile.characterId=catalog.CharacterAt(player.character).characterId;
            var meta=player.GetComponent<MetaProgress>() ?? player.gameObject.AddComponent<MetaProgress>();
            meta.Initialize(HubDefinition,profile,slot,local); player.Health.Restore(); meta.Save();
        }
        public void SetHubCharacter(PlayerIdentity player,int index)
        {
            var slot=Lobby.slots.Find(s=>s.slot==player.slot); if(slot!=null) slot.character=index;
            if(IsLocalOwner(player.owner)) PreferredCharacter=index;
        }
        public void HubCommand(int slot,HubAction action,int index)
        {
            var own=Lobby.slots.Find(s=>s.slot==slot && IsLocalOwner(s.owner)); if(own==null || !InGame) return;
            if(IsAuthority && characters.TryGetValue(slot,out var player)) Flow.GetComponent<PlayerHubController>()?.Execute(player.Motor,action,index);
            else Send(0,"hub",JsonUtility.ToJson(new HubRequest{slot=slot,action=action,index=index}));
        }
        void CaptureHubState(WorldSnapshot snapshot)
        {
            foreach(var state in snapshot.players)
                if(characters.TryGetValue(state.slot,out var player)) { var meta=player.GetComponent<MetaProgress>(); state.meta=meta?.profile; state.hubStation=meta?.OpenStation ?? -1; }
        }
        void SaveClientMeta(WorldSnapshot snapshot)
        {
            var own=snapshot.players.FirstOrDefault(p=>IsLocalOwner(p.owner)); if(own?.meta==null) return;
            own.meta.Validate(HubDefinition);
            string json=JsonUtility.ToJson(own.meta);
            if(json!=savedMeta && MetaSave.Save(0,own.meta)) savedMeta=json;
            PreferredCharacter=own.character;
        }
        public void ReturnToHub()
        { if(IsAuthority && InGame) Flow.GetComponent<PlayerHubController>()?.ReturnToHub(); }
    }
}
