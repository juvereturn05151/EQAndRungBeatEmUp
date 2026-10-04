using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    [CreateAssetMenu(menuName = "Beat Em Up/Upgrade Pool")]
    public sealed class UpgradePool : ScriptableObject
    {
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        [Min(0)] public float commonWeight = 70, rareWeight = 25, epicWeight = 5;
        [Range(0, 1)] public float synergyBias = .15f;
        public float RarityWeight(UpgradeRarity rarity) => rarity == UpgradeRarity.Common ? commonWeight : rarity == UpgradeRarity.Rare ? rareWeight : epicWeight;
        public List<UpgradeDefinition> Generate(RunBuildState build, System.Random random, int count = 3)
        {
            // Draw a rarity tier first, then a definition. Tier odds are independent
            // of how many cards the author placed in each tier.
            var candidates = upgrades.FindAll(u => u && build.CanAcquire(u) && u.weight > 0);
            var result = new List<UpgradeDefinition>();
            while (result.Count < count && candidates.Count > 0)
            {
                float total = 0; var available = new float[3];
                foreach (UpgradeRarity rarity in Enum.GetValues(typeof(UpgradeRarity)))
                    if (candidates.Exists(u => u.rarity == rarity)) { available[(int)rarity] = RarityWeight(rarity); total += available[(int)rarity]; }
                if (total <= 0) break;
                double tierRoll = random.NextDouble() * total; int tier = 0;
                for (; tier < 2; tier++) { tierRoll -= available[tier]; if (tierRoll < 0) break; }
                var tierCards = candidates.FindAll(u => (int)u.rarity == tier);
                float cardTotal = 0;
                foreach (var u in tierCards) cardTotal += CardWeight(u, build);
                double roll = random.NextDouble() * cardTotal; UpgradeDefinition chosen = tierCards[tierCards.Count - 1];
                foreach (var u in tierCards) { roll -= CardWeight(u, build); if (roll < 0) { chosen = u; break; } }
                result.Add(chosen); candidates.RemoveAll(u => u == chosen || u.id == chosen.id);
            }
            return result;
        }
        private float CardWeight(UpgradeDefinition upgrade, RunBuildState build) => upgrade.weight * (1 + (upgrade.tags.Exists(build.HasTag) ? synergyBias : 0));
    }
}
