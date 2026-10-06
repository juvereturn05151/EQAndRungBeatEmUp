using UnityEngine;
using UnityEngine.UI;

namespace BeatEmUp
{
    // Independent presentation binding, usable by any Canvas without owning the player resource.
    public sealed class MeterUIWidget : MonoBehaviour
    {
        public PlayerMeter meter;
        public Image fill;
        public Text label;
        public Color chargingColor = new Color(.48f, .25f, .85f), fullColor = new Color(.8f, .5f, 1);
        void OnEnable() { if (meter) meter.Changed += Refresh; Refresh(); }
        void OnDisable() { if (meter) meter.Changed -= Refresh; }
        public void Bind(PlayerMeter resource)
        {
            if (meter && isActiveAndEnabled) meter.Changed -= Refresh;
            meter = resource;
            if (meter && isActiveAndEnabled) meter.Changed += Refresh;
            Refresh();
        }
        public void Refresh()
        {
            if (!meter) return;
            if (fill) { fill.rectTransform.anchorMax = new Vector2(meter.Normalized, 1); fill.color = meter.Normalized >= 1 ? fullColor : chargingColor; }
            if (label) label.text = "SKILL  " + meter.CurrentMeter.ToString("0.##") + " / " + meter.MaxMeter.ToString("0.##") + " BAR   [ I / RT ]";
        }
    }
}
