using UnityEngine;
using UnityEngine.UI;
namespace BeatEmUp
{
    public sealed class CharacterPortraitUI : MonoBehaviour
    {
        public static Color[] PlayerColors => PlayerGroundIndicatorStyle.SharedPlayerColors;
        public Image PortraitImage, SelectionBorder;
        public Text PlayerMarker, ReadyIndicator;
        public GameObject LockedOverlay;
        public Image[] PlayerBorders;
        public Text[] PlayerMarkers;
        float punchUntil;
        bool selected, confirmed;
        int playerIndex;
        public void SetCharacter(CharacterDefinition character)
        {
            PortraitImage.sprite = character ? character.Portrait : null;
            PortraitImage.enabled = PortraitImage.sprite;
            SetLocked(!character || !character.IsUnlocked || !character.Prefab);
        }
        public void SetSelected(bool value) { selected = value; SelectionBorder.enabled = value; }
        public void SetConfirmed(bool value) { confirmed = value; ReadyIndicator.enabled = value; }
        public void SetLocked(bool value) => LockedOverlay.SetActive(value);
        public void SetPlayerIndex(int value) { playerIndex = Mathf.Clamp(value,0,3); SelectionBorder.color = PlayerColors[playerIndex]; PlayerMarker.text = "P" + (playerIndex + 1); }
        public void Punch(bool strong) { punchUntil = Time.unscaledTime + (strong ? .2f : .12f); }
        public void Render(System.Collections.Generic.List<LobbySlot> slots, int characterIndex)
        {
            selected = confirmed = false;
            for(int i = 0; i < 4; i++)
            {
                var slot = slots.Find(s => s.slot == i && s.character == characterIndex);
                bool active = characterIndex >= 0 && slot != null;
                PlayerBorders[i].enabled = active; PlayerBorders[i].color = PlayerColors[i];
                PlayerMarkers[i].enabled = active; PlayerMarkers[i].text = "P" + (i+1) + (active && slot.ready ? " READY" : "");
                selected |= active; confirmed |= active && slot.ready;
            }
            SelectionBorder.enabled = false; PlayerMarker.enabled = false;
            ReadyIndicator.enabled = confirmed;
        }
        void Update()
        {
            float scale = Time.unscaledTime < punchUntil ? 1.09f : selected && !confirmed ? 1.025f + .008f * Mathf.Sin(Time.unscaledTime * 8) : 1;
            transform.localScale = Vector3.one * scale;
            for(int i = 0; i < PlayerBorders.Length; i++)
                if(PlayerBorders[i].enabled) PlayerBorders[i].color = confirmed && Time.unscaledTime < punchUntil ? Color.Lerp(PlayerColors[i],Color.white,.65f) : PlayerColors[i];
        }
    }
}
