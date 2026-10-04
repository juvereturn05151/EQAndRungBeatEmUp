using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class RewardChoiceWorldObject : MonoBehaviour
    {
        public RewardSelectionController Owner { get; private set; }
        public int Index { get; private set; }
        public UpgradeDefinition Upgrade { get; private set; }
        public bool TryInteract() => Owner && Owner.CanChoose(Index) && Owner.Interact();
        private static Sprite square;
        public void Configure(RewardSelectionController owner, int index, UpgradeDefinition upgrade)
        {
            Owner = owner; Index = index; Upgrade = upgrade;
            if (!square) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            Color color = upgrade.rarity == UpgradeRarity.Epic ? new Color(.85f, .5f, 1) : upgrade.rarity == UpgradeRarity.Rare ? new Color(.35f, .75f, 1) : new Color(.65f, .95f, .7f);
            Plate("Pedestal", new Vector2(0, .04f), new Vector2(.7f, .1f), color, 200);
            Plate("Stem", new Vector2(0, .22f), new Vector2(.12f, .3f), color, 200);
            Plate("Blessing card", new Vector2(0, .82f), new Vector2(1.42f, .94f), new Color(.04f, .055f, .08f, .95f), 500);
            Plate("Rarity trim", new Vector2(0, 1.29f), new Vector2(1.42f, .025f), color, 501);
            Text("Title", Wrap(upgrade.displayName, 22), new Vector2(0, 1.23f), .026f, color);
            Text("Description", Wrap(upgrade.description, 30), new Vector2(0, 1.02f), .019f, Color.white);
            Text("Rarity / build", upgrade.rarity + " | " + string.Join(" / ", upgrade.tags), new Vector2(0, .53f), .018f, color);
        }
        void Plate(string label, Vector2 position, Vector2 size, Color color, int order)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false); child.transform.localPosition = position; child.transform.localScale = size;
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.color = color; renderer.sortingOrder = order;
        }
        void Text(string label, string value, Vector2 position, float size, Color color)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false); child.transform.localPosition = position;
            var text = child.AddComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 48; text.characterSize = size;
            text.anchor = TextAnchor.UpperCenter; text.alignment = TextAlignment.Center; text.color = color; text.text = value;
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingOrder = 502;
        }
        static string Wrap(string value, int width)
        {
            var lines = new List<string>(); string line = "";
            foreach (var word in value.Split(' ')) { if (line.Length + word.Length + 1 > width && line.Length > 0) { lines.Add(line); line = ""; } line += (line.Length == 0 ? "" : " ") + word; }
            lines.Add(line); return string.Join("\n", lines);
        }
    }
}
