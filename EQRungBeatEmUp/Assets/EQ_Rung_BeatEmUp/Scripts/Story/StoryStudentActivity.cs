using UnityEngine;

namespace BeatEmUp.Story
{
    // Identity persists when activity frames change, so possession retains gender.
    public sealed class StoryStudentActivity : MonoBehaviour
    {
        public bool schoolgirl;
        public Sprite[] frames;
        public float secondsPerPose=1.1f;
        public float Elapsed { get; private set; }
        SpriteRenderer visual;
        void Awake() { visual=GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if(frames==null || frames.Length==0 || PrologueDirector.Active?.Cutscenes.Paused==true)return;
            Elapsed+=Time.unscaledDeltaTime;
            visual.sprite=frames[(int)(Elapsed/secondsPerPose)%frames.Length];
        }
    }
}
