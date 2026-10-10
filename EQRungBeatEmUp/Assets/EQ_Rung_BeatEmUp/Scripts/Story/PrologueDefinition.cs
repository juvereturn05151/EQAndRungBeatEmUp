using UnityEngine;
using TMPro;

namespace BeatEmUp.Story
{
    [CreateAssetMenu(menuName="Beat Em Up/Story/Prologue")]
    public sealed class PrologueDefinition : ScriptableObject
    {
        public DialogueDatabase dialogue;
        public TMP_FontAsset font;
        public Sprite schoolFair, hideout, sanctuary, prapot, cream, chanai, student;
        public Sprite studentGirl;
        [Min(12)] public float festivalWidth=26;
        public float festivalDestination=9.5f;
        public float festivalLaneMin=-1.2f, festivalLaneMax=-.2f;
        public Vector2 FestivalArenaMin => new Vector2(-festivalWidth/2+.5f,Mathf.Min(festivalLaneMin,festivalLaneMax));
        public Vector2 FestivalArenaMax => new Vector2(festivalWidth/2-.5f,Mathf.Max(festivalLaneMin,festivalLaneMax));
        [Header("Festival stores")]
        public Sprite festivalChickenStore, festivalPepsiStore;
        public Vector2 festivalChickenPosition=new Vector2(-8,-.1f), festivalPepsiPosition=new Vector2(8,-.1f);
        [Min(.2f)] public float festivalStoreWidth=2.7f;
        [Min(.2f)] public float festivalStoreInteractRadius=1.1f;
        public CutsceneSequence festivalChickenMemory, festivalPepsiMemory;
        public Vector2 FestivalStorePosition(int index)
        {
            var point=index==0 ? festivalChickenPosition : festivalPepsiPosition;
            point.x=Mathf.Clamp(point.x,FestivalArenaMin.x,FestivalArenaMax.x);return point;
        }
        public Vector2 FestivalStoreGround(int index) => new Vector2(FestivalStorePosition(index).x,(FestivalArenaMin.y+FestivalArenaMax.y)/2);
        public Sprite[] studentIceCream, studentDrink, studentPhone, studentWave;
        public Sprite escapeConceptArt;
        [Header("Playable tutorial")]
        public Vector2 tutorialAreaMin=new Vector2(-3.1f,-1.2f);
        public Vector2 tutorialAreaMax=new Vector2(3.1f,-.2f);
        public Vector2 tutorialPlayerStart=new Vector2(-1,-.7f);
        public Vector2 tutorialPartnerStart=new Vector2(-.4f,-.7f);
        public Vector2 tutorialEnemyStart=new Vector2(1,-.7f);
        public Vector2 tutorialEnemySpacing=new Vector2(.5f,.25f);
        public float tutorialGoalX=1.8f;
        public Vector3 tutorialCamera=new Vector3(0,1.15f,-10);
        [Min(.1f)] public float tutorialZoom=2.35f;
        public Vector2 TutorialArenaMin => Vector2.Min(tutorialAreaMin,tutorialAreaMax);
        public Vector2 TutorialArenaMax => Vector2.Max(tutorialAreaMin,tutorialAreaMax);
        public Vector2 ClampTutorialPosition(Vector2 point) => new Vector2(Mathf.Clamp(point.x,TutorialArenaMin.x,TutorialArenaMax.x),Mathf.Clamp(point.y,TutorialArenaMin.y,TutorialArenaMax.y));
        public float TutorialGoal => Mathf.Clamp(tutorialGoalX,TutorialArenaMin.x,TutorialArenaMax.x);
        public Vector2 TutorialEnemyPosition(int index) => ClampTutorialPosition(tutorialEnemyStart+new Vector2(index*tutorialEnemySpacing.x,(index%2)*tutorialEnemySpacing.y));
        public Sprite destinationArrow;
        public PlayableCharacterData eq, rung;
        public GameObject delinquentPrefab, throwerPrefab;
        public GameObject possessedStudentPrefab;
        public GameObject possessedSchoolgirlPrefab;
        public CutsceneSequence reunion, attack, cornered, sanctuaryIntro, kidnapping, defeat, awakening, rescue, memorySpell, returnPresent;
        public CutsceneSequence escape;
    }
}
