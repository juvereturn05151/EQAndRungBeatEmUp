using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class CoopRewards : MonoBehaviour
    {
        public sealed class Selection
        {
            public PlayerIdentity player;
            public bool opened, done;
            public List<UpgradeDefinition> choices = new List<UpgradeDefinition>();
        }
        public bool Pending { get; private set; }
        public Transform Chapel { get; private set; }
        public readonly List<Selection> selections = new List<Selection>();
        StageFlowController flow;
        Action completed;
        System.Random random = new System.Random();
        public bool Begin(Action onComplete)
        {
            Cancel(); flow=GetComponent<StageFlowController>();
            if(!flow || !flow.RunUpgrades || !flow.RunUpgrades.pool || !flow.WorldRewards || !flow.WorldRewards.chapelPrefab) return false;
            // Reuse the existing reachable chapel-placement algorithm, then own only the multi-player choices.
            if(!flow.WorldRewards.BeginReward(null)) return false;
            var source=flow.WorldRewards.Chapel;
            Chapel=Instantiate(flow.WorldRewards.chapelPrefab,source.transform.position,Quaternion.identity,transform).transform;
            Chapel.name="Co-op reward chapel";
            flow.WorldRewards.Cancel();
            foreach(var p in PlayerRoster.Living)
            {
                var build=p.GetComponent<RunBuildState>();
                p.Health.SafeStageProtection=true;
                selections.Add(new Selection{player=p,choices=flow.RunUpgrades.pool.Generate(build,random)});
            }
            completed=onComplete; Pending=true; return true;
        }
        public bool Interact(PlayerIdentity player)
        {
            var choice=selections.Find(s=>s.player==player);
            if(!Pending || choice==null || choice.done || !player.Living || !player.Motor.IsGrounded || player.Motor.MovementLocked || player.Motor.attackPlayer.CurrentAttack || !Chapel ||
                Vector2.Distance(player.transform.position,Chapel.position)>flow.CurrentStage.rewardInteractRadius) return false;
            choice.opened=true;
            player.GetComponent<ComboController>().ResetCombo();
            if(choice.choices.Count==0) { player.Health.Heal(player.Health.EffectiveMaximum*.25f); choice.done=true; CheckComplete(); }
            return true;
        }
        public bool Choose(PlayerIdentity player,int index)
        {
            var s=selections.Find(c=>c.player==player);
            if(!Pending || s==null || !s.opened || s.done || !player.Living || index<0 || index>=s.choices.Count) return false;
            if(!player.GetComponent<RunBuildState>().Acquire(s.choices[index])) return false;
            s.done=true; CheckComplete(); return true;
        }
        public void CloseChoice(PlayerIdentity player)
        { var selection=selections.Find(s=>s.player==player); if(selection!=null && !selection.done) selection.opened=false; }
        public void CheckComplete()
        {
            if(!Pending || selections.Any(s=>s.player && s.player.Living && !s.done)) return;
            Pending=false;
            foreach(var s in selections) if(s.player) s.player.Health.SafeStageProtection=flow.CurrentStage.IsSafeStage;
            if(Chapel) { Chapel.gameObject.SetActive(false); Destroy(Chapel.gameObject); } Chapel=null;
            var callback=completed; completed=null; callback?.Invoke();
        }
        void Update() { if(Pending) CheckComplete(); }
        public void Cancel()
        {
            Pending=false; completed=null; selections.Clear();
            if(Chapel) { Chapel.gameObject.SetActive(false); Destroy(Chapel.gameObject); } Chapel=null;
        }
        void OnDisable() => Cancel();
    }
}
