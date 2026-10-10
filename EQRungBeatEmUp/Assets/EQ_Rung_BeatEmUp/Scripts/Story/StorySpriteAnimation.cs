using UnityEngine;

namespace BeatEmUp.Story
{
    public sealed class StorySpriteAnimation : MonoBehaviour
    {
        public Sprite[] frames;
        public float framesPerSecond=12;
        public bool holdLastFrame;
        float elapsed;
        Sprite restSprite;
        bool hasRestSprite;
        public void Play(Sprite[] sprites,float speed=12,bool hold=false)
        {
            var renderer=GetComponent<SpriteRenderer>();
            if(!hasRestSprite) { restSprite=renderer.sprite;hasRestSprite=true; }
            frames=sprites;framesPerSecond=Mathf.Max(.01f,speed);holdLastFrame=hold;elapsed=0;
            if(frames!=null && frames.Length>0)renderer.sprite=frames[0];
        }
        public void Stop(bool restore=true)
        {
            frames=null;elapsed=0;
            if(restore && hasRestSprite)GetComponent<SpriteRenderer>().sprite=restSprite;
            hasRestSprite=false;
        }
        public void Advance(float seconds)
        {
            if(frames==null || frames.Length==0 || PrologueDirector.Active?.Cutscenes?.Paused==true)return;
            elapsed+=Mathf.Max(0,seconds);
            int index=(int)(elapsed*framesPerSecond);
            GetComponent<SpriteRenderer>().sprite=frames[holdLastFrame ? Mathf.Min(index,frames.Length-1) : index%frames.Length];
        }
        void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }
    }
}
