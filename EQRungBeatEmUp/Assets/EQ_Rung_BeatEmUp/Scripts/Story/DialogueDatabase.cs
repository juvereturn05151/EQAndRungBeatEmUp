using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp.Story
{
    [Serializable] public class DialogueChoice { public string thai, english, flag; }
    [Serializable] public class DialogueLine
    {
        public string speaker;
        public Sprite portrait;
        [TextArea(2,5)] public string thai, english;
        public float charactersPerSecond = 35, autoAdvanceSeconds;
        public AudioClip voiceBlip;
        public string callback;
        public DialogueChoice[] choices = Array.Empty<DialogueChoice>();
        public string Text(bool th) => th && !string.IsNullOrEmpty(thai) ? thai : english;
    }
    [Serializable] public class Conversation { public string id; public List<DialogueLine> lines = new List<DialogueLine>(); }
    [CreateAssetMenu(menuName="Beat Em Up/Story/Dialogue database")]
    public sealed class DialogueDatabase : ScriptableObject
    {
        public List<Conversation> conversations = new List<Conversation>();
        public Conversation Find(string id) => conversations.Find(c => c.id == id);
    }
}
