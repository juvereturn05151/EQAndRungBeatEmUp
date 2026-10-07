using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    [RequireComponent(typeof(StageFlowController))]
    public sealed class RunUpgradeController : MonoBehaviour
    {
        public UpgradePool pool;
        [Tooltip("0 uses a fresh random seed; nonzero reproduces card draws for tuning.")]
        public int seed;
        public bool showBuildButton = true;
        public RunBuildState Build { get; private set; }
        public bool IsChoosing { get; private set; }
        public bool IsWorldChoosing { get; private set; }
        public IReadOnlyList<UpgradeDefinition> Choices => choices;
        public string ChoiceMessage { get; private set; }
        private readonly List<UpgradeDefinition> choices = new List<UpgradeDefinition>();
        private System.Random random;
        private Action selected;
        private StageFlowController flow;
        private float previousTimeScale;
        private bool buildVisible, debugVisible;
        private int focused, debugStage;
        private Vector2 buildScroll, debugScroll;
        private GUIStyle title, cardTitle, body, small;
        private MenuNavigationInput menuInput;
        readonly ImmediateMenuNavigation debugNavigation=new ImmediateMenuNavigation();
        public bool MenuOpen => buildVisible || debugVisible || IsChoosing && !IsWorldChoosing;
        private void OnDestroy() => menuInput?.Dispose();
        public void Initialize()
        {
            if (!flow) flow = GetComponent<StageFlowController>();
            if (!Build && flow.player) { Build = flow.player.GetComponent<RunBuildState>(); if (!Build) Build = flow.player.gameObject.AddComponent<RunBuildState>(); }
            if (random == null) random = seed == 0 ? new System.Random() : new System.Random(seed);
        }
        public void NewRun()
        {
            Initialize(); CloseChoice(); Build?.ResetRun();
            random = seed == 0 ? new System.Random() : new System.Random(seed);
        }
        public bool Offer(Action onSelected = null, bool worldSelection = false)
        {
            Initialize(); if (IsChoosing || !Build || !pool) return false;
            choices.Clear(); choices.AddRange(pool.Generate(Build, random));
            if (choices.Count == 0) { ChoiceMessage = "All available upgrades are exhausted."; return false; }
            if (worldSelection) { int available = choices.Count; while (choices.Count < 3) choices.Add(choices[choices.Count % available]); }
            selected = onSelected; focused = 0; buildVisible = debugVisible = false; IsChoosing = true;
            IsWorldChoosing = worldSelection;
            ChoiceMessage = choices.Count == 3 ? "Choose one upgrade for this run" : "Choose one — remaining eligible upgrades";
            if (!IsWorldChoosing) { previousTimeScale = Time.timeScale; Time.timeScale = 0; CombatClock.SetPaused(this, true); }
            flow.player.GetComponent<ComboController>()?.ResetCombo(); Build.ClearTransient();
            if (!IsWorldChoosing) flow.player.MoveInput = Vector2.zero;
            flow.player.GetComponent<ComboController>()?.RequestGuard(false);
            return true;
        }
        public bool Choose(int index)
        {
            if (IsWorldChoosing && (!flow.WorldRewards || !flow.WorldRewards.CanChoose(index))) return false;
            if (!IsChoosing || index < 0 || index >= choices.Count || !Build.Acquire(choices[index])) return false;
            var callback = selected; CloseChoice(); callback?.Invoke(); return true;
        }
        public void CloseChoice()
        {
            selected = null; choices.Clear();
            if (!IsChoosing) return;
            if (!IsWorldChoosing) { Time.timeScale = previousTimeScale; CombatClock.SetPaused(this, false); }
            IsChoosing = IsWorldChoosing = false;
        }
        private void OnDisable() => CloseChoice();
        public void DebugForceChoice() { if (!Application.isEditor && !Debug.isDebugBuild) return; Offer(); }
        public bool DebugGive(UpgradeDefinition upgrade) { Initialize(); return (Application.isEditor || Debug.isDebugBuild) && Build && Build.Acquire(upgrade); }
        public void DebugClearBuild() { if (!Application.isEditor && !Debug.isDebugBuild) return; Initialize(); Build.ResetRun(); if (IsChoosing) DebugReroll(); }
        public void DebugReroll()
        {
            if (!IsChoosing || (!Application.isEditor && !Debug.isDebugBuild)) return;
            choices.Clear(); choices.AddRange(pool.Generate(Build, random)); focused = 0;
            if (choices.Count == 0) { var callback = selected; CloseChoice(); callback?.Invoke(); }
            else if (IsWorldChoosing && flow.WorldRewards) flow.WorldRewards.RefreshChoices();
        }
        public void DebugJumpToReward(int index)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (!flow.level || index < 0 || index >= flow.level.stages.Count) return;
            CloseChoice(); flow.RestartAt(index); Offer();
        }
        private void Update()
        {
            var key = Keyboard.current; var pad = Gamepad.current;
            Initialize();
            if(menuInput==null && flow.player) menuInput=new MenuNavigationInput(flow.player.GetComponent<PlayerInput>().actions);
            var input=menuInput?.Read() ?? default;
            if (key != null && key.tabKey.wasPressedThisFrame) buildVisible = !buildVisible;
            if (pad?.startButton.wasPressedThisFrame==true && !IsChoosing) buildVisible = !buildVisible;
            if ((Application.isEditor || Debug.isDebugBuild) && key != null && key.f9Key.wasPressedThisFrame) debugVisible = !debugVisible;
            if(debugVisible)
            {
                if(input.Cancel) debugVisible=false;
                else { debugNavigation.Read(input); debugScroll.y=Mathf.Max(0,debugScroll.y-input.Navigate.y*32); }
                return;
            }
            if(buildVisible)
            {
                if(input.Cancel || input.Confirm) buildVisible=false;
                else buildScroll.y=Mathf.Max(0,buildScroll.y-input.Navigate.y*80);
                return;
            }
            if (!IsChoosing || IsWorldChoosing) return;
            if (key != null)
            {
                if (key.digit1Key.wasPressedThisFrame) { Choose(0); return; }
                if (key.digit2Key.wasPressedThisFrame) { Choose(1); return; }
                if (key.digit3Key.wasPressedThisFrame) { Choose(2); return; }
            }
            if(input.Navigate!=Vector2.zero) focused=(focused+(input.Navigate.x>0 || input.Navigate.y<0 ? 1 : choices.Count-1))%choices.Count;
            if(input.Confirm) Choose(focused);
        }
        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 15 };
        }
        public void DrawUI()
        {
            if (!Build) return; Styles(); var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            try
            {
                if (showBuildButton && !IsChoosing && GUI.Button(new Rect(1050, 12, 218, 36), "Current build  [Tab]")) buildVisible = !buildVisible;
                if (IsChoosing && !IsWorldChoosing)
                {
                    var old = GUI.color; GUI.color = new Color(.025f, .035f, .055f, .97f); GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture); GUI.color = old;
                    GUI.Label(new Rect(60, 50, 1160, 54), "STAGE CLEARED", title);
                    GUI.Label(new Rect(100, 110, 1080, 40), ChoiceMessage, new GUIStyle(body) { alignment = TextAnchor.MiddleCenter });
                    for (int i = 0; i < choices.Count; i++)
                    {
                        var u = choices[i]; float x = 90 + i * 380;
                        GUI.Box(new Rect(x, 190, 340, 370), GUIContent.none);
                        Color rarity = u.rarity == UpgradeRarity.Epic ? new Color(.82f, .45f, 1) : u.rarity == UpgradeRarity.Rare ? new Color(.35f, .7f, 1) : new Color(.65f, .9f, .72f);
                        GUI.color = rarity; GUI.DrawTexture(new Rect(x + 18, 210, 304, 4), Texture2D.whiteTexture); GUI.color = old;
                        if (u.icon) GUI.DrawTexture(new Rect(x + 22, 232, 48, 48), u.icon.texture, ScaleMode.ScaleToFit);
                        else GUI.Label(new Rect(x + 22, 232, 65, 45), "[ " + (i + 1) + " ]", cardTitle);
                        GUI.Label(new Rect(x + 86, 238, 235, 38), u.rarity.ToString().ToUpperInvariant(), small);
                        GUI.Label(new Rect(x + 22, 296, 296, 64), u.displayName, cardTitle);
                        GUI.Label(new Rect(x + 22, 370, 296, 104), u.description, body);
                        GUI.Label(new Rect(x + 22, 470, 296, 30), "Stacks " + Build.Stacks(u) + " → " + (Build.Stacks(u) + 1) + " / " + u.maxStacks, small);
                        if (GUI.Button(new Rect(x + 22, 508, 296, 34), (focused == i ? "► " : "") + "Choose  [" + (i + 1) + "]")) { Choose(i); break; }
                    }
                    GUI.Label(new Rect(110, 608, 1060, 45), "Arrows / WASD / stick / D-pad: move • Enter / A: choose • 1 / 2 / 3: quick choose", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                }
                if (buildVisible) DrawBuild();
                if (debugVisible && (Application.isEditor || Debug.isDebugBuild)) DrawDebug();
            }
            finally { GUI.matrix = previousMatrix; }
        }
        private void OnGUI() => DrawUI();
        private void DrawBuild()
        {
            GUI.Box(new Rect(830, 65, 438, 620), GUIContent.none);
            GUILayout.BeginArea(new Rect(850, 80, 398, 585));
            GUILayout.Label("CURRENT RUN BUILD", cardTitle);
            if (GUILayout.Button("► Close  [Enter / Escape / A / B / Tab / Start]")) buildVisible = false;
            buildScroll = GUILayout.BeginScrollView(buildScroll);
            if (Build.Acquired.Count == 0) GUILayout.Label("No upgrades yet. Clear a reward stage to choose your first.", body);
            foreach (var stack in Build.Acquired) { GUILayout.Label(stack.upgrade.displayName + " ×" + stack.count, cardTitle); GUILayout.Label(stack.upgrade.description, small); GUILayout.Space(12); }
            GUILayout.Label("Second Wind uses: " + Build.LethalSavesUsed, small);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawDebug()
        {
            debugNavigation.Begin();
            GUI.Box(new Rect(12, 140, 400, 555), GUIContent.none); GUILayout.BeginArea(new Rect(24, 152, 376, 530));
            GUILayout.Label("RUN UPGRADE DEBUG  [F9]", cardTitle); debugScroll = GUILayout.BeginScrollView(debugScroll);
            if (debugNavigation.Button("Force upgrade choice now")) DebugForceChoice();
            if (IsChoosing && debugNavigation.Button("Reroll cards (debug)")) DebugReroll();
            if (debugNavigation.Button("Clear current build")) DebugClearBuild();
            if (debugNavigation.Button("Print current modifiers")) Debug.Log(Build.DescribeModifiers());
            GUILayout.Label("Jump to reward in stage " + (debugStage + 1));
            if(debugNavigation.Button("Next reward stage")) debugStage=(debugStage+1)%flow.level.stages.Count;
            if (debugNavigation.Button("Jump and force reward choice")) DebugJumpToReward(debugStage);
            if (pool) foreach (var upgrade in pool.upgrades) if (upgrade && debugNavigation.Button("Give: " + upgrade.displayName + " (" + Build.Stacks(upgrade) + ")")) DebugGive(upgrade);
            GUILayout.EndScrollView(); GUILayout.EndArea();
            debugNavigation.End();
        }
    }
}
