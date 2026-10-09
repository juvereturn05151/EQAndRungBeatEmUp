using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum WorldRewardState { Inactive, RewardPending, Choosing, Resolved }
    [DisallowMultipleComponent, RequireComponent(typeof(StageFlowController))]
    public sealed class RewardSelectionController : MonoBehaviour
    {
        public RewardChapelInteractable chapelPrefab;
        public RewardChoiceWorldObject choicePrefab;
        public WorldRewardState State { get; private set; }
        public bool IsPending => State == WorldRewardState.RewardPending || State == WorldRewardState.Choosing;
        public RewardChapelInteractable Chapel { get; private set; }
        public IReadOnlyList<RewardChoiceWorldObject> ChoiceObjects => cards;
        private readonly List<RewardChoiceWorldObject> cards = new List<RewardChoiceWorldObject>();
        private StageFlowController flow;
        private GameObject root;
        private Action completed;
        private int stageIndex;
        private List<Vector2> reachable;
        public bool BeginReward(Action onCompleted)
        {
            Cancel(); flow = GetComponent<StageFlowController>();
            if (flow.CurrentStage == null || !flow.player || !chapelPrefab) return false;
            Physics2D.SyncTransforms(); reachable = ReachableGround();
            if (!FindPoint(flow.CurrentStage.chapelSpawnPoint, new List<Vector2>(), 1.4f, out Vector2 point)) return false;
            stageIndex = flow.StageIndex; completed = onCompleted;
            root = new GameObject("World blessing reward"); root.transform.SetParent(transform, false);
            Chapel = Instantiate(chapelPrefab, point, Quaternion.identity, root.transform); Chapel.name = "Reward Chapel"; Chapel.Owner = this;
            var visual = Chapel.GetComponent<SpriteRenderer>(); if (visual) visual.sortingOrder = Mathf.RoundToInt(-point.y * 100);
            foreach (var enemy in flow.StageEnemies) if (enemy) enemy.gameObject.SetActive(false);
            flow.player.GetComponent<CharacterHealth>().SafeStageProtection = true;
            State = WorldRewardState.RewardPending; return true;
        }
        public bool Interact()
        {
            if (!IsPending || !flow || stageIndex != flow.StageIndex || CombatClock.IsPaused || !flow.player.IsGrounded || flow.player.MovementLocked || flow.player.GetComponent<CharacterHealth>().IsDead || flow.player.attackPlayer.CurrentAttack) return false;
            if (State == WorldRewardState.RewardPending)
            {
                if (!Chapel || Vector2.Distance(flow.player.transform.position, Chapel.transform.position) > flow.CurrentStage.rewardInteractRadius) return false;
                State = WorldRewardState.Choosing;
                if (!flow.RunUpgrades.Offer(Resolve, true))
                {
                    // Exhaustion still requires visiting the chapel; preserve the old heal fallback.
                    flow.player.GetComponent<CharacterHealth>().Heal(flow.player.GetComponent<CharacterHealth>().EffectiveMaximum * .25f); Resolve(); return true;
                }
                if (!RefreshChoices()) { flow.RunUpgrades.CloseChoice(); State = WorldRewardState.RewardPending; return false; }
                if (Chapel) Chapel.gameObject.SetActive(false);
                return true;
            }
            int closest = NearestChoice(); return closest >= 0 && flow.RunUpgrades.Choose(closest);
        }
        public bool CanChoose(int index) => State == WorldRewardState.Choosing && flow && stageIndex == flow.StageIndex && !CombatClock.IsPaused && flow.player.IsGrounded && !flow.player.MovementLocked && !flow.player.GetComponent<CharacterHealth>().IsDead && !flow.player.attackPlayer.CurrentAttack && index >= 0 && index < cards.Count && cards[index] && Vector2.Distance(flow.player.transform.position, cards[index].transform.position) <= flow.CurrentStage.rewardInteractRadius;
        int NearestChoice()
        {
            int result = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < cards.Count; i++) if (CanChoose(i)) { float d = Vector2.Distance(flow.player.transform.position, cards[i].transform.position); if (d < distance) { distance = d; result = i; } }
            return result;
        }
        public bool RefreshChoices()
        {
            ClearCards(); reachable = ReachableGround();
            var positions = new List<Vector2>(); var stage = flow.CurrentStage;
            for (int i = 0; i < flow.RunUpgrades.Choices.Count; i++)
            {
                var desired = stage.rewardChoiceCenter + Vector2.right * ((i - 1) * stage.rewardChoiceSpacing);
                if (!FindPoint(desired, positions, .75f, out Vector2 point)) { ClearCards(); return false; }
                positions.Add(point);
                var card = choicePrefab ? Instantiate(choicePrefab, point, Quaternion.identity, root.transform) : new GameObject("Blessing " + (i + 1)).AddComponent<RewardChoiceWorldObject>();
                card.transform.SetParent(root.transform, true); card.transform.position = point; card.name = "Blessing " + (i + 1);
                card.Configure(this, i, flow.RunUpgrades.Choices[i]); cards.Add(card);
            }
            return true;
        }
        void Resolve()
        {
            var callback = completed; completed = null; State = WorldRewardState.Resolved;
            ClearObjects(); if (flow && flow.player) flow.player.GetComponent<CharacterHealth>().SafeStageProtection = flow.CurrentStage.IsSafeStage;
            callback?.Invoke();
        }
        public void Cancel()
        {
            completed = null; if (flow && flow.RunUpgrades && flow.RunUpgrades.IsWorldChoosing) flow.RunUpgrades.CloseChoice();
            State = WorldRewardState.Inactive; ClearObjects();
            if (flow && flow.player && flow.CurrentStage != null) flow.player.GetComponent<CharacterHealth>().SafeStageProtection = flow.CurrentStage.IsSafeStage;
        }
        void ClearCards() { foreach (var card in cards) if (card) { card.gameObject.SetActive(false); Destroy(card.gameObject); } cards.Clear(); }
        void ClearObjects() { ClearCards(); if (root) { root.SetActive(false); Destroy(root); } root = null; Chapel = null; }
        private void OnDisable() => Cancel();
        bool FindPoint(Vector2 desired, List<Vector2> reserved, float playerDistance, out Vector2 result)
        {
            result = default; float best = float.PositiveInfinity;
            foreach (var point in reachable)
            {
                if (Vector2.Distance(point, flow.player.transform.position) < playerDistance || reserved.Exists(p => Vector2.Distance(p, point) < 1.5f) || !Free(point, true)) continue;
                float score = (point - desired).sqrMagnitude; if (score < best) { result = point; best = score; }
            }
            return !float.IsPositiveInfinity(best);
        }
        List<Vector2> ReachableGround()
        {
            var stage = flow.CurrentStage; Vector2 min = stage.movementMin + new Vector2(.3f, .12f), max = stage.movementMax - new Vector2(.3f, .12f);
            min.x = Mathf.Max(min.x, -stage.artWidth * .5f + .75f); max.x = Mathf.Min(max.x, stage.artWidth * .5f - .75f);
            var result = new List<Vector2>(); if (min.x > max.x || min.y > max.y) return result;
            int columns = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / .25f)), rows = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / .25f));
            var points = new Vector2[columns + 1, rows + 1]; var free = new bool[columns + 1, rows + 1];
            Vector2Int start = default; float nearest = float.PositiveInfinity;
            for (int x = 0; x <= columns; x++) for (int y = 0; y <= rows; y++)
            {
                var point = new Vector2(Mathf.Lerp(min.x, max.x, (float)x / columns), Mathf.Lerp(min.y, max.y, (float)y / rows)); points[x,y] = point; free[x,y] = Free(point, false);
                float distance = (point - (Vector2)flow.player.transform.position).sqrMagnitude;
                if (free[x,y] && distance < nearest) { nearest = distance; start = new Vector2Int(x,y); }
            }
            if (float.IsPositiveInfinity(nearest)) return result;
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start); free[start.x,start.y] = false;
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); result.Add(points[cell.x,cell.y]);
                foreach (var direction in directions) { var next = cell + direction; if (next.x < 0 || next.x > columns || next.y < 0 || next.y > rows || !free[next.x,next.y]) continue; free[next.x,next.y] = false; queue.Enqueue(next); }
            }
            return result;
        }
        bool Free(Vector2 point, bool avoidProps)
        {
            foreach (var collider in Physics2D.OverlapBoxAll(point + flow.player.wallCollisionOffset, flow.player.wallCollisionSize, 0, flow.player.wallCollisionMask))
            {
                var prop = collider.GetComponentInParent<DestructibleObject>();
                if ((!collider.isTrigger && collider.GetComponentInParent<CombatWall>()) || (avoidProps && prop && !prop.IsBroken)) return false;
            }
            return true;
        }
        private void OnGUI()
        {
            if (!IsPending || !flow || !flow.player.IsGrounded) return;
            bool near = State == WorldRewardState.RewardPending ? Chapel && Vector2.Distance(flow.player.transform.position, Chapel.transform.position) <= flow.CurrentStage.rewardInteractRadius : NearestChoice() >= 0;
            if (!near) return;
            GUI.Box(new Rect(Screen.width * .5f - 210, Screen.height - 64, 420, 44), State == WorldRewardState.RewardPending ? "E / L1 / LB: receive blessing" : "E / L1 / LB: choose this upgrade");
        }
    }
}
