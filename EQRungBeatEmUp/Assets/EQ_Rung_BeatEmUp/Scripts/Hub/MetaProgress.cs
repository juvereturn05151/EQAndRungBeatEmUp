using System;
using System.IO;
using UnityEngine;
namespace BeatEmUp
{
    public static class MetaSave
    {
        public static string DirectoryOverride;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateTests()
        { if(Array.IndexOf(Environment.GetCommandLineArgs(),"-isolated-meta-tests")>=0) DirectoryOverride=Path.Combine(Application.temporaryCachePath,"MetaValidation-"+Guid.NewGuid().ToString("N")); }
        public static string PathFor(int slot)=>Path.Combine(DirectoryOverride ?? Application.persistentDataPath,"PlayerMeta"+Mathf.Clamp(slot,0,3)+".json");
        public static MetaProfile Load(int slot,PlayerHubDefinition settings)
        {
            var path=PathFor(slot);
            foreach(var candidate in new[]{path,path+".bak"})
                try { if(File.Exists(candidate)) { var profile=JsonUtility.FromJson<MetaProfile>(File.ReadAllText(candidate)); if(profile!=null && profile.version==1) { profile.Validate(settings); return profile; } } }
                catch(Exception ex) when(ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException) { Debug.LogWarning("Meta save could not be read: "+ex.Message); }
            return new MetaProfile{essence=settings ? settings.startingEssence : 60};
        }
        public static bool Save(int slot,MetaProfile profile)
        {
            var path=PathFor(slot);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path+".tmp",JsonUtility.ToJson(profile,true));
                if(File.Exists(path)) File.Replace(path+".tmp",path,path+".bak"); else File.Move(path+".tmp",path);
                return true;
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException) { Debug.LogWarning("Meta save failed: "+ex.Message); return false; }
        }
    }
    [DisallowMultipleComponent] public sealed class MetaProgress : MonoBehaviour
    {
        public PlayerHubDefinition settings;
        public MetaProfile profile;
        public int saveSlot;
        public bool persistLocally=true;
        public int OpenStation { get; set; }=-1;
        public float HealthBonus=>settings && profile!=null ? profile.healthLevel*settings.healthPerLevel : 0;
        public float DamageReduction=>settings && profile!=null ? Mathf.Clamp01(profile.defenseLevel*settings.reductionPerLevel) : 0;
        public float MeterMultiplier=>settings && profile!=null ? 1+profile.meterLevel*settings.meterGainPerLevel : 1;
        public void Initialize(PlayerHubDefinition definition,MetaProfile data,int slot,bool persist)
        { settings=definition; profile=data ?? new MetaProfile{essence=definition ? definition.startingEssence : 60}; profile.Validate(settings); saveSlot=slot; persistLocally=persist; }
        public float DamageMultiplier(bool skill)=>settings && profile!=null ? (1+profile.attackLevel*settings.attackPerLevel)*(skill ? 1+profile.SkillLevel*settings.skillPowerPerLevel : 1) : 1;
        public bool Upgrade(int stat,bool skill=false)
        {
            if(!settings || profile==null || stat<0 || stat>3) return false;
            int level=skill ? profile.SkillLevel : profile.Level(stat), cost=settings.Cost(level);
            if(level>=settings.maximumLevel || profile.essence<cost) return false;
            var previous=JsonUtility.ToJson(profile);
            profile.essence-=cost; if(skill) profile.IncreaseSkill(); else profile.Increase(stat);
            if(persistLocally && !MetaSave.Save(saveSlot,profile)) { profile=JsonUtility.FromJson<MetaProfile>(previous); return false; }
            if(!skill && stat==0) GetComponent<CharacterHealth>().Heal(settings.healthPerLevel); return true;
        }
        public void Save() { if(persistLocally && profile!=null) MetaSave.Save(saveSlot,profile); }
    }
}
