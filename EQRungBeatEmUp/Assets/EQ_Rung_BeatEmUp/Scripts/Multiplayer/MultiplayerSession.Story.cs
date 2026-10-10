using System.Linq;
using BeatEmUp.Story;
using UnityEngine;

namespace BeatEmUp
{
    public sealed partial class MultiplayerSession
    {
        [System.Serializable] sealed class StoryRequest { public bool skip; public int revision; public int choice=-1; }
        void RegisterStoryMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"story",(sender,reader)=>
            {
                if(!IsAuthority || !InGame || !sceneReady || !Lobby.slots.Any(s=>s.owner==sender))return;
                var request=JsonUtility.FromJson<StoryRequest>(Read(reader));var story=Flow ? Flow.GetComponent<PrologueDirector>() : null;
                if(story && request!=null && request.revision==story.UI.Revision && !request.skip)
                { if(request.choice>=0)story.RequestChoice(request.choice);else story.RequestAdvance(); }
                // Whole-scene skips belong to the host and are mirrored by the next snapshot.
            });
        }
        public void StoryChoice(int choice,int revision)
        {
            if(!InGame)return;
            if(IsAuthority)Flow.GetComponent<PrologueDirector>()?.RequestChoice(choice);
            else Send(0,"story",JsonUtility.ToJson(new StoryRequest{choice=choice,revision=revision}));
        }
        public void StoryCommand(bool skip,int revision)
        {
            if(!InGame)return;
            if(IsAuthority) { var story=Flow.GetComponent<PrologueDirector>();if(skip)story.RequestSkip();else story.RequestAdvance(); }
            else if(!skip)Send(0,"story",JsonUtility.ToJson(new StoryRequest{skip=false,revision=revision}));
        }
    }
}
