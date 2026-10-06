using UnityEngine;
namespace BeatEmUp
{
    [CreateAssetMenu(menuName="Beat Em Up/Player Hub")]
    public sealed class PlayerHubDefinition : ScriptableObject
    {
        [Min(1)] public float width=38;
        [Min(.1f)] public float interactionRadius=1.2f, deathReturnDelay=1.8f;
        public Vector2 spawn=new Vector2(-16,0);
        public Vector2[] stations={new Vector2(-9,0),new Vector2(-1,0),new Vector2(7,0),new Vector2(16,0)};
        public Sprite[] panels;
        public Material panelBlendMaterial;
        [Min(0)] public int startingEssence=60, baseCost=10, costPerLevel=10, maximumLevel=10, roomReward=10;
        public float healthPerLevel=10, attackPerLevel=.05f, reductionPerLevel=.025f, meterGainPerLevel=.1f, skillPowerPerLevel=.1f;
        public int Cost(int level)=>Mathf.Max(1,baseCost+costPerLevel*level);
    }
    [System.Serializable] public sealed class MetaProfile
    {
        public int version=1, essence, healthLevel, attackLevel, defenseLevel, meterLevel;
        public string characterId="blue-shirt";
        public int blueSkillLevel, graySkillLevel;
        public int Level(int stat)=>stat==0 ? healthLevel : stat==1 ? attackLevel : stat==2 ? defenseLevel : meterLevel;
        public void Increase(int stat) { if(stat==0) healthLevel++; else if(stat==1) attackLevel++; else if(stat==2) defenseLevel++; else meterLevel++; }
        public int SkillLevel=>characterId=="gray-shirt" ? graySkillLevel : blueSkillLevel;
        public void IncreaseSkill() { if(characterId=="gray-shirt") graySkillLevel++; else blueSkillLevel++; }
        public void Validate(PlayerHubDefinition settings)
        {
            version=1; essence=Mathf.Clamp(essence,0,1000000);
            int max=settings ? Mathf.Max(0,settings.maximumLevel) : 10;
            healthLevel=Mathf.Clamp(healthLevel,0,max); attackLevel=Mathf.Clamp(attackLevel,0,max);
            defenseLevel=Mathf.Clamp(defenseLevel,0,max); meterLevel=Mathf.Clamp(meterLevel,0,max);
            blueSkillLevel=Mathf.Clamp(blueSkillLevel,0,max); graySkillLevel=Mathf.Clamp(graySkillLevel,0,max);
            if(characterId!="gray-shirt") characterId="blue-shirt";
        }
    }
}
