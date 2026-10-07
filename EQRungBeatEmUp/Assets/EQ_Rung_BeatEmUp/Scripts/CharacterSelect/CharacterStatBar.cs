using UnityEngine;
using UnityEngine.UI;
namespace BeatEmUp
{
    public sealed class CharacterStatBar : MonoBehaviour
    {
        public Image[] Segments;
        public Color FillColor = Color.white;
        public void SetValue(int value)
        {
            for(int i = 0; i < Segments.Length; i++) Segments[i].color = i < value ? FillColor : new Color(.14f, .14f, .18f);
        }
    }
}
