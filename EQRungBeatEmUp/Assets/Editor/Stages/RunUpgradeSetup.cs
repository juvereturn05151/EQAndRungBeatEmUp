using System;
using System.IO;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunUpgradeSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Upgrades";
    public const string PoolPath = Root + "/HauntedUpgradePool.asset";
    private static UpgradeEffect Effect(RunModifier modifier, float amount) => new UpgradeEffect { modifier = modifier, amount = amount };
    private static UpgradeDefinition Card(string id, string name, string description, string tag, UpgradeRarity rarity, int stacks, params UpgradeEffect[] effects)
    {
        string path = Root + "/" + id + ".asset";
        var card = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
        if (card) return card; // Preserve author tuning on rerun.
        card = ScriptableObject.CreateInstance<UpgradeDefinition>(); card.id = id; card.displayName = name; card.description = description;
        card.tags.Add(tag); card.rarity = rarity; card.maxStacks = stacks; card.effects.AddRange(effects);
        AssetDatabase.CreateAsset(card, path); return card;
    }
    [MenuItem("Beat Em Up/Upgrades/Set up stage run upgrades")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var cards = new List<UpgradeDefinition> {
            Card("HeavyHands", "Heavy Hands", "Ground punch damage +20% per stack.", "Brawler", UpgradeRarity.Common, 3, Effect(RunModifier.GroundDamage, .2f)),
            Card("ComboMomentum", "Combo Momentum", "Combo damage +10% per step after the first hit, per stack.", "Brawler", UpgradeRarity.Rare, 2, Effect(RunModifier.ComboDamagePerStep, .1f)),
            Card("Finisher", "Finisher", "Final ground combo attack damage +30% per stack.", "Brawler", UpgradeRarity.Common, 3, Effect(RunModifier.GroundFinisherDamage, .3f)),
            Card("JuggleMaster", "Juggle Master", "Damage against airborne enemies +20% per stack.", "Air", UpgradeRarity.Common, 3, Effect(RunModifier.AirborneTargetDamage, .2f)),
            Card("AirFinisher", "Air Finisher", "Final air punch damage and downward slam speed +25% per stack.", "Air", UpgradeRarity.Rare, 2, Effect(RunModifier.AirFinisherDamage, .25f), Effect(RunModifier.AirFinisherFallSpeed, .25f)),
            Card("HigherLauncherDamage", "Rising Force", "Launcher damage +25% per stack.", "Air", UpgradeRarity.Common, 3, Effect(RunModifier.LauncherDamage, .25f)),
            Card("HardHead", "Hard Head", "Diving headbutt damage +25% per stack.", "Dive", UpgradeRarity.Common, 3, Effect(RunModifier.DiveDamage, .25f)),
            Card("DiveShockwave", "Dive Shockwave", "A connected dive releases an 8-damage nearby shockwave on landing.", "Dive", UpgradeRarity.Epic, 1, Effect(RunModifier.DiveShockwaveDamage, 8)),
            Card("DiveReset", "Dive Reset", "Diving headbutt landing recovery is 3 frames shorter.", "Dive", UpgradeRarity.Rare, 1, Effect(RunModifier.DiveRecoveryReduction, 3)),
            Card("PerfectTiming", "Perfect Timing", "Parry window +2 frames per stack.", "Parry", UpgradeRarity.Rare, 2, Effect(RunModifier.ParryWindowBonus, 2)),
            Card("Counterattack", "Counterattack", "After a parry, your next attack deals +50% damage.", "Parry", UpgradeRarity.Rare, 1, Effect(RunModifier.ParryAttackBonus, .5f)),
            Card("GhostBreaker", "Ghost Breaker", "Parry stuns the attacker for 12 extra frames per stack.", "Parry", UpgradeRarity.Common, 2, Effect(RunModifier.ParryStunBonus, 12)),
            Card("QuickStep", "Quick Step", "Dodge recovery is 3 frames shorter per stack.", "Dodge", UpgradeRarity.Common, 2, Effect(RunModifier.DodgeRecoveryReduction, 3)),
            Card("LongStep", "Long Step", "Dodge distance +25% per stack.", "Dodge", UpgradeRarity.Common, 3, Effect(RunModifier.DodgeDistanceBonus, .25f)),
            Card("Afterimage", "Afterimage", "Dodge through a hit: your next attack deals +35% damage.", "Dodge", UpgradeRarity.Rare, 1, Effect(RunModifier.DodgeAttackBonus, .35f)),
            Card("StrongGuard", "Strong Guard", "Blockstun -25% per stack.", "Tank", UpgradeRarity.Common, 3, Effect(RunModifier.BlockstunReduction, .25f)),
            Card("IronBody", "Iron Body", "Maximum health +40 per stack. Gain that much HP immediately.", "Tank", UpgradeRarity.Common, 3, Effect(RunModifier.MaximumHealthBonus, 40)),
            Card("SecondWind", "Second Wind", "Survive one lethal hit at 1 HP this run.", "Tank", UpgradeRarity.Epic, 1, Effect(RunModifier.LethalSaves, 1))
        };
        var pool = AssetDatabase.LoadAssetAtPath<UpgradePool>(PoolPath);
        bool firstSetup = !pool;
        if (!pool) { pool = ScriptableObject.CreateInstance<UpgradePool>(); pool.upgrades = cards; AssetDatabase.CreateAsset(pool, PoolPath); }
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset");
        if (firstSetup && level)
        {
            foreach (var stage in level.stages) { stage.rewardAfterClear = stage.safeRoom ? StageReward.Heal : !stage.IsSafeStage && stage.encounters.Count > 0 && stage.completionMode != StageCompletion.BossDefeated ? StageReward.UpgradeChoice : StageReward.None; stage.rewardHealFraction = .5f; }
            EditorUtility.SetDirty(level);
        }
        var scene = EditorSceneManager.OpenScene("Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity");
        var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>();
        var controller = flow.GetComponent<RunUpgradeController>(); if (!controller) controller = flow.gameObject.AddComponent<RunUpgradeController>();
        if (!controller.pool) controller.pool = pool;
        EditorUtility.SetDirty(controller); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("RUN UPGRADE SETUP COMPLETE: 18 editable definitions; choices after stages1-5; shrine heal; current run only.");
    }
}
