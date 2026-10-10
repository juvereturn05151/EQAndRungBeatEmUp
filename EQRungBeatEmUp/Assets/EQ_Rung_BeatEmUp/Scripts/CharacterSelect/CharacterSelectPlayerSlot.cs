using UnityEngine;
using UnityEngine.UI;
namespace BeatEmUp
{
    public sealed class CharacterSelectPlayerSlot : MonoBehaviour
    {
        public Image ColorStrip;
        public Text PlayerLabel, StateLabel;
        Image characterPreview;
        Text characterName, archetype, description, joinHint;
        CharacterStatBar[] stats;
        GameObject characterDetails;
        CharacterDefinition displayedCharacter;
        float animationAt;
        int animationFrame;

        // Built from the existing slot so both saved and regenerated selection prefabs use this layout.
        public void ConfigureCoopCard(int index)
        {
            if(characterDetails) return;
            SetRect((RectTransform)transform, new Rect(60 + index * 455, 240, 440, 550));
            SetRect(ColorStrip.rectTransform, new Rect(0, 0, 440, 5));
            SetRect(PlayerLabel.rectTransform, new Rect(20, 18, 70, 40));
            SetRect(StateLabel.rectTransform, new Rect(105, 23, 315, 35));
            StateLabel.fontSize = 22;
            characterDetails = new GameObject("Character details", typeof(RectTransform));
            characterDetails.transform.SetParent(transform, false);
            SetRect((RectTransform)characterDetails.transform, new Rect(0, 0, 440, 550));
            characterPreview = CreateImage("Character preview", characterDetails.transform, new Rect(30, 65, 380, 195), Color.white);
            characterPreview.preserveAspect = true;
            characterName = CreateText("Character name", characterDetails.transform, new Rect(20, 268, 400, 38), 28, Color.white);
            characterName.resizeTextForBestFit = true; characterName.resizeTextMinSize = 22; characterName.resizeTextMaxSize = 28;
            archetype = CreateText("Archetype", characterDetails.transform, new Rect(20, 310, 400, 30), 23, new Color(1, .66f, .35f));
            string[] labels = { "POWER", "SPEED", "DEFENSE", "TECHNIQUE" };
            Color[] colors = { CharacterPortraitUI.PlayerColors[0], CharacterPortraitUI.PlayerColors[2], CharacterPortraitUI.PlayerColors[1], CharacterPortraitUI.PlayerColors[3] };
            stats = new CharacterStatBar[4];
            for(int row = 0; row < stats.Length; row++)
            {
                CreateText(labels[row], characterDetails.transform, new Rect(20, 350 + row * 30, 175, 32), 20, new Color(.88f, .85f, .82f));
                var bar = new GameObject(labels[row] + "Bar", typeof(RectTransform), typeof(CharacterStatBar));
                bar.transform.SetParent(characterDetails.transform, false);
                SetRect((RectTransform)bar.transform, new Rect(200, 350 + row * 30, 220, 28));
                stats[row] = bar.GetComponent<CharacterStatBar>(); stats[row].FillColor = colors[row]; stats[row].Segments = new Image[5];
                for(int segment = 0; segment < 5; segment++)
                    stats[row].Segments[segment] = CreateImage("Segment " + segment, bar.transform, new Rect(segment * 43, 4, 37, 20), colors[row]);
            }
            description = CreateText("Description", characterDetails.transform, new Rect(20, 480, 400, 64), 20, new Color(.72f, .70f, .76f));
            joinHint = CreateText("Join hint", transform, new Rect(30, 220, 380, 110), 28, new Color(.65f, .62f, .68f));
            joinHint.text = "PRESS ENTER / A\nTO JOIN"; joinHint.alignment = TextAnchor.MiddleCenter;
        }

        public void RenderCharacter(LobbySlot slot, CharacterDefinition character)
        {
            if(!characterDetails) return;
            bool hasCharacter = slot != null && character;
            characterDetails.SetActive(hasCharacter);
            joinHint.gameObject.SetActive(slot == null);
            if(!hasCharacter) { displayedCharacter = null; return; }
            if(displayedCharacter != character)
            {
                displayedCharacter = character; animationFrame = 0; animationAt = Time.unscaledTime;
                characterPreview.sprite = character.Preview;
            }
            characterPreview.enabled = characterPreview.sprite;
            characterName.text = character.DisplayName;
            archetype.text = character.Archetype;
            description.text = character.IsUnlocked ? character.Description : "LOCKED";
            stats[0].SetValue(character.Power); stats[1].SetValue(character.Speed);
            stats[2].SetValue(character.Defense); stats[3].SetValue(character.Technique);
        }

        void Update()
        {
            if(!displayedCharacter || !characterDetails || !characterDetails.activeSelf) return;
            var frames = displayedCharacter.CharacterSelectAnimation;
            if(frames == null || frames.Length == 0 || Time.unscaledTime < animationAt) return;
            characterPreview.sprite = frames[animationFrame++ % frames.Length];
            characterPreview.enabled = characterPreview.sprite;
            animationAt = Time.unscaledTime + .13f;
        }

        internal static void SetRect(RectTransform rect, Rect area)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(area.x, -area.y); rect.sizeDelta = area.size;
        }
        Image CreateImage(string name, Transform parent, Rect area, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); SetRect(image.rectTransform, area); image.color = color; image.raycastTarget = false;
            return image;
        }
        Text CreateText(string name, Transform parent, Rect area, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); SetRect(text.rectTransform, area);
            text.text = name; text.font = PlayerLabel.font; text.fontStyle = FontStyle.Bold; text.fontSize = size; text.color = color; text.raycastTarget = false;
            return text;
        }
        public void Render(int index, LobbySlot slot)
        {
            ColorStrip.color = CharacterPortraitUI.PlayerColors[index];
            PlayerLabel.text = "P" + (index + 1); PlayerLabel.color = ColorStrip.color;
            StateLabel.text = slot == null ? characterDetails ? "WAITING FOR PLAYER" : "PRESS ENTER / A\nTO JOIN" : slot.ready ? "READY" : slot.character < 0 ? "NO FREE FIGHTER" : "SELECTING";
            StateLabel.color = slot != null && slot.ready ? ColorStrip.color : Color.white;
        }
    }
}
