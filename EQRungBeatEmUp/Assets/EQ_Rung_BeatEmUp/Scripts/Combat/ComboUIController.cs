using UnityEngine;
using UnityEngine.UI;

namespace BeatEmUp
{
    [DisallowMultipleComponent]
    public sealed class ComboUIController : MonoBehaviour
    {
        [Min(0)] public float finalHoldSeconds = .8f;
        [Min(.01f)] public float fadeSeconds = .35f;
        [Min(0)] public float hitPopSeconds = .12f;
        public Vector2 topRightInset = new Vector2(24, 24);
        public Color hitColor = new Color(1, .78f, .3f);
        public ComboTracker tracker;
        public CanvasGroup Group { get; private set; }
        public Text HitText { get; private set; }
        public Text DamageText { get; private set; }
        GameObject canvasObject;
        RectTransform panel;
        float endElapsed = -1, popElapsed;
        void OnEnable()
        {
            if (!tracker) tracker = GetComponent<ComboTracker>();
            Build(); tracker.Changed += UpdateText; tracker.Ended += FinalizeText; tracker.Reset += Hide;
            if (tracker.IsActive) UpdateText(); else Hide();
        }
        void OnDisable()
        {
            if (tracker) { tracker.Changed -= UpdateText; tracker.Ended -= FinalizeText; tracker.Reset -= Hide; }
            if (canvasObject) canvasObject.SetActive(false);
        }
        void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
        void Build()
        {
            if (canvasObject) { canvasObject.SetActive(true); return; }
            canvasObject = new GameObject("Combo HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var box = new GameObject("Combo result", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            box.transform.SetParent(canvasObject.transform, false); panel = box.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.sizeDelta = new Vector2(300, 116); panel.anchoredPosition = -topRightInset;
            box.GetComponent<Image>().color = new Color(.04f, .045f, .065f, .78f); box.GetComponent<Image>().raycastTarget = false;
            Group = box.GetComponent<CanvasGroup>(); Group.interactable = Group.blocksRaycasts = false;
            HitText = Label("Hits", new Vector2(0,-12), 44, FontStyle.Bold);
            DamageText = Label("Damage", new Vector2(0,-65), 23, FontStyle.Normal);
            HitText.color = hitColor; DamageText.color = Color.white;
            var meter = GetComponent<PlayerMeter>();
            if (meter) BuildMeter(meter);
        }
        void BuildMeter(PlayerMeter meter)
        {
            var go = new GameObject("Skill meter", typeof(RectTransform), typeof(Image), typeof(MeterUIWidget));
            go.transform.SetParent(canvasObject.transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24, 24); rect.sizeDelta = new Vector2(390, 76);
            go.GetComponent<Image>().color = new Color(.04f, .045f, .065f, .85f); go.GetComponent<Image>().raycastTarget = false;
            var backing = new GameObject("Meter backing", typeof(RectTransform), typeof(Image)); backing.transform.SetParent(go.transform, false);
            var track = backing.GetComponent<RectTransform>(); track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0);
            track.pivot = new Vector2(.5f, 0); track.offsetMin = new Vector2(12, 12); track.offsetMax = new Vector2(-12, 28);
            backing.GetComponent<Image>().color = new Color(.16f, .12f, .22f); backing.GetComponent<Image>().raycastTarget = false;
            var filling = new GameObject("Meter fill", typeof(RectTransform), typeof(Image)); filling.transform.SetParent(backing.transform, false);
            var fr = filling.GetComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            var title = new GameObject("Meter label", typeof(RectTransform), typeof(Text)); title.transform.SetParent(go.transform, false);
            var tr = title.GetComponent<RectTransform>(); tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0,1);
            tr.anchoredPosition = new Vector2(12,-8); tr.sizeDelta = new Vector2(366,30);
            var text = title.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 21; text.color = Color.white; text.raycastTarget = false;
            var widget = go.GetComponent<MeterUIWidget>(); widget.fill = filling.GetComponent<Image>(); widget.fill.raycastTarget = false; widget.label = text; widget.Bind(meter);
        }
        Text Label(string name, Vector2 position, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(panel, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,1);
            rect.pivot = new Vector2(.5f,1); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(280,52);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = style; text.alignment = TextAnchor.UpperCenter; text.raycastTarget = false;
            return text;
        }
        void UpdateText()
        {
            HitText.text = tracker.HitCount + (tracker.HitCount == 1 ? " HIT" : " HITS");
            DamageText.text = tracker.TotalDamage.ToString("0.##") + " DAMAGE";
            HitText.color = tracker.HitCount >= 6 ? new Color(1,.4f,.22f) : hitColor;
            Group.alpha = 1; endElapsed = -1; popElapsed = 0;
        }
        void FinalizeText() { UpdateText(); endElapsed = 0; }
        void Hide() { if (Group) Group.alpha = 0; endElapsed = -1; if (panel) panel.localScale = Vector3.one; }
        void Update() => AdvanceDisplay(Time.unscaledDeltaTime);
        public void AdvanceDisplay(float seconds)
        {
            if (!Group || CombatClock.IsPaused) return;
            panel.anchoredPosition = -topRightInset;
            popElapsed += Mathf.Max(0, seconds);
            float pop = hitPopSeconds > 0 ? 1 - Mathf.Clamp01(popElapsed / hitPopSeconds) : 0;
            panel.localScale = Vector3.one * (1 + .08f * pop);
            if (endElapsed < 0) return;
            endElapsed += Mathf.Max(0, seconds);
            Group.alpha = 1 - Mathf.Clamp01((endElapsed - finalHoldSeconds) / Mathf.Max(.01f, fadeSeconds));
        }
    }
}
