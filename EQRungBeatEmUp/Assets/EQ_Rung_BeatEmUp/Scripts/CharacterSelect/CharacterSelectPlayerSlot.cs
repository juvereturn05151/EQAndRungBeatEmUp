using UnityEngine;
using UnityEngine.UI;
namespace BeatEmUp
{
    public sealed class CharacterSelectPlayerSlot : MonoBehaviour
    {
        public Image ColorStrip;
        public Text PlayerLabel, StateLabel;
        public void Render(int index, LobbySlot slot)
        {
            ColorStrip.color = CharacterPortraitUI.PlayerColors[index];
            PlayerLabel.text = "P" + (index + 1); PlayerLabel.color = ColorStrip.color;
            StateLabel.text = slot == null ? "PRESS ENTER / A\nTO JOIN" : slot.ready ? "READY" : slot.character < 0 ? "NO FREE FIGHTER" : "SELECTING";
            StateLabel.color = slot != null && slot.ready ? ColorStrip.color : Color.white;
        }
    }
}
