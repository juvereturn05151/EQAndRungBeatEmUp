using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp.Story
{
    public sealed partial class PrologueDirector
    {
        public const string ChickenStoreVisited="FestivalChickenMemorySeen", PepsiStoreVisited="FestivalPepsiMemorySeen";
        readonly TextMeshPro[] festivalStoreLabels=new TextMeshPro[2];
        int pendingFestivalStore=-1;
        public bool FestivalStoresCompleted => Progress!=null && Progress.Has(ChickenStoreVisited) && Progress.Has(PepsiStoreVisited);
        static string StoreFlag(int index) => index==0 ? ChickenStoreVisited : PepsiStoreVisited;
        static string StoreName(int index) => index==0 ? "ร้านไก่ป๊อป" : "Pepsi";
        static string StoreActor(int index) => index==0 ? "FestivalChickenStore" : "FestivalPepsiStore";

        void RefreshFestivalStores(bool visible)
        {
            for(int i=0;i<2;i++)
            {
                var actor=Actor(StoreActor(i));var sprite=i==0 ? Definition.festivalChickenStore : Definition.festivalPepsiStore;
                if(visible && !actor && sprite)
                {
                    actor=SpawnActor(StoreActor(i),sprite,Definition.FestivalStorePosition(i));
                    actor.transform.localScale=Vector3.one*(Definition.festivalStoreWidth/Mathf.Max(.01f,sprite.bounds.size.x));
                    var text=new GameObject("Store name / interaction prompt").AddComponent<TextMeshPro>();
                    text.transform.SetParent(actor.transform,false);
                    // World text stays a readable size regardless of source-image resolution.
                    text.transform.localScale=Vector3.one/actor.transform.localScale.x;
                    float promptY=Definition.FestivalStoreGround(i).y+1.5f;
                    text.transform.localPosition=new Vector3(0,(promptY-actor.transform.position.y)/actor.transform.localScale.x,0);
                    text.font=Definition.font;text.fontSize=2.2f;text.alignment=TextAlignmentOptions.Center;
                    text.rectTransform.sizeDelta=new Vector2(4.5f,.8f);text.color=new Color(1,.95f,.75f);
                    text.renderer.sortingOrder=300;festivalStoreLabels[i]=text;
                }
                if(actor)actor.SetActive(visible);
            }
        }

        public int NearbyFestivalStore(CharacterMotor player)
        {
            if(!player || !player.IsGrounded || player.GetComponent<CharacterHealth>()?.IsDead==true)return -1;
            int nearest=-1;float distance=Mathf.Max(.2f,Definition.festivalStoreInteractRadius);
            for(int i=0;i<2;i++)
            {
                if(Progress.Has(StoreFlag(i)))continue;
                float candidate=Vector2.Distance(player.transform.position,Definition.FestivalStoreGround(i));
                if(candidate<=distance) { distance=candidate;nearest=i; }
            }
            return nearest;
        }

        public bool TryInteractFestivalStore(CharacterMotor player)
        {
            if(!Authority || !ActiveStory || Phase!=1 || InputLocked || UI.IsTalking || pendingFestivalStore>=0 || !Players.Contains(player))return false;
            int store=NearbyFestivalStore(player);if(store<0)return false;
            var sequence=store==0 ? Definition.festivalChickenMemory : Definition.festivalPepsiMemory;
            if(!sequence)return false;
            pendingFestivalStore=store;return true;
        }

        void UpdateFestivalStores()
        {
            if(!Authority || !ActiveStory || Phase>3)return;
            if(Phase==1 && !InputLocked && pendingFestivalStore<0 && !MultiplayerSession.Active)
                foreach(var player in Players)
                    if(player.GetComponent<PlayerInput>()?.actions.FindAction("Player/Interact")?.WasPressedThisFrame()==true)
                        if(TryInteractFestivalStore(player))break;
            for(int i=0;i<2;i++)
            {
                var label=festivalStoreLabels[i];if(!label)continue;
                label.gameObject.SetActive(Phase==1 && !InputLocked);
                bool visited=Progress.Has(StoreFlag(i));bool nearby=Phase==1 && !InputLocked && Players.Any(p=>NearbyFestivalStore(p)==i);
                label.text=visited ? (UI.Thai ? "คุยแล้ว" : "Visited") : nearby ? "["+Keys("Interact")+"] "+(UI.Thai ? "พูดคุย" : "Talk") : "";
                label.color=visited ? new Color(.65f,1,.75f) : nearby ? Color.white : new Color(1,.95f,.75f);
            }
            if(Phase==1 && !InputLocked)SetFestivalStoreInstruction();
        }

        void SetFestivalStoreInstruction()
        {
            int count=(Progress.Has(ChickenStoreVisited) ? 1 : 0)+(Progress.Has(PepsiStoreVisited) ? 1 : 0);
            int nearby=Players.Select(NearbyFestivalStore).Where(i=>i>=0).DefaultIfEmpty(-1).First();
            string key=Keys("Interact");
            SetText("แวะคุยที่ร้านไก่ป๊อปทางซ้ายและร้าน Pepsi ทางขวา ("+count+"/2)"+(nearby>=0 ? "\n["+key+"] พูดคุยที่ "+StoreName(nearby) : "\nเข้าใกล้ร้านแล้วกด "+key),
                "Visit Chicken Pop on the left and Pepsi on the right ("+count+"/2)"+(nearby>=0 ? "\n["+key+"] Talk at "+(nearby==0 ? "Chicken Pop" : "Pepsi") : "\nApproach a stall and press "+key));
        }

        IEnumerator VisitFestivalStores()
        {
            pendingFestivalStore=-1;Actor("Destination")?.SetActive(false);
            RefreshFestivalStores(true);SetFestivalStoreInstruction();
            while(!retry && !FestivalStoresCompleted)
            {
                if(pendingFestivalStore<0) { yield return null;continue; }
                int store=pendingFestivalStore;
                var sequence=store==0 ? Definition.festivalChickenMemory : Definition.festivalPepsiMemory;
                SetText("","");
                yield return Scene(sequence);
                Progress.Set(StoreFlag(store));Progress.Save();pendingFestivalStore=-1;
                SetFestivalStoreInstruction();
            }
        }
    }
}
