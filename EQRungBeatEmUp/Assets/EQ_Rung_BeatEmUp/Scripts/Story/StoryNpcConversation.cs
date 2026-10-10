using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp.Story
{
    public sealed class StoryNpcConversation : MonoBehaviour
    {
        public PrologueDirector director;
        public string conversation;
        void Update()
        {
            if(!director || director.ActiveStory || director.InputLocked || !director.Authority)return;
            var flow=director.GetComponent<StageFlowController>();gameObject.GetComponent<SpriteRenderer>().enabled=flow.CurrentStage?.hub;
            if(!flow.CurrentStage?.hub)return;
            var nearby=flow.Players.FirstOrDefault(p=>p && Vector2.Distance(p.transform.position,transform.position)<.8f);
            string key=nearby ? nearby.GetComponent<PlayerInput>()?.actions.FindAction("Player/Interact")?.GetBindingDisplayString() : "";
            director.UI.SetInstruction(nearby ? (director.UI.Thai ? "พูดคุยกับอาจารย์ประพจน์: " : "Talk to Professor Prapot: ")+key : "");
        }
        public static bool TryInteract(CharacterMotor player)
        {
            var director=PrologueDirector.Active;
            if(!director || !director.Authority || director.ActiveStory || director.InputLocked || !player || !player.IsGrounded || !director.GetComponent<StageFlowController>().CurrentStage?.hub)return false;
            var npc=FindObjectsByType<StoryNpcConversation>().FirstOrDefault(n=>n.director==director && Vector2.Distance(player.transform.position,n.transform.position)<.8f);
            if(!npc)return false;director.Talk(npc.conversation);return true;
        }
    }
}
