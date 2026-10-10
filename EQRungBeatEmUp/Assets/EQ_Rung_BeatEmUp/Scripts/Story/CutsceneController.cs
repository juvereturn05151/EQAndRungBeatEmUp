using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp.Story
{
    public sealed class CutsceneController : MonoBehaviour
    {
        public PrologueDirector director;
        public bool IsCutscenePlaying { get; private set; }
        public bool Paused { get; private set; }
        public int CurrentStep { get; private set; }
        public CutsceneSequence Current { get; private set; }
        public event Action<CutsceneSequence> OnCutsceneStarted, OnCutsceneFinished;
        bool skip;
        public void PauseCutscene() { Paused=true;director.UI.Paused=true; }
        public void ResumeCutscene() { Paused=false;director.UI.Paused=false; }
        public void SkipCutscene() { if(IsCutscenePlaying) { skip=true;director.UI.Hide(); } }
        public Coroutine PlayCutscene(CutsceneSequence sequence) => StartCoroutine(Play(sequence));
        public static bool IsInstantEvent(CutsceneEvent evt) => evt.duration<=0 && evt.action!=StoryAction.Dialogue;
        // Dispatch setup as one batch before yielding to the next rendered frame.
        public static int ApplyInstantEvents(IList<CutsceneEvent> events,int start,Action<CutsceneEvent> apply)
        {
            while(start<events.Count && IsInstantEvent(events[start]))apply(events[start++]);
            return start;
        }
        void ApplyImmediate(CutsceneEvent evt)
        {
            var actor=director.Actor(evt.target);
            if(evt.action==StoryAction.Move && actor)
            {
                if(!evt.preserveFacing)director.Face(actor,evt.position.x-actor.transform.position.x);
                director.SetActorWalking(actor,false);
            }
            director.Apply(evt);
        }
        public IEnumerator Play(CutsceneSequence sequence)
        {
            if(IsCutscenePlaying || !sequence || !director.Authority) yield break;
            Current=sequence;IsCutscenePlaying=true;skip=Paused=false;director.UI.Paused=false;CurrentStep=0;
            director.SetCinematic(true);OnCutsceneStarted?.Invoke(sequence);
            bool seen=sequence.playsOnce && director.Progress.Has("Seen:"+sequence.id);
            while(!seen && !skip && CurrentStep<sequence.events.Count)
            {
                if(Paused) { yield return null;continue; }
                int next=ApplyInstantEvents(sequence.events,CurrentStep,ApplyImmediate);
                if(next!=CurrentStep) { CurrentStep=next;continue; }
                var evt=sequence.events[CurrentStep];
                if(evt.parallelGroup>0)
                {
                    int pending=0, group=evt.parallelGroup;
                    while(CurrentStep<sequence.events.Count && sequence.events[CurrentStep].parallelGroup==group)
                    {
                        var grouped=sequence.events[CurrentStep++];
                        if(IsInstantEvent(grouped))ApplyImmediate(grouped);
                        else { pending++;StartCoroutine(Parallel(grouped,()=>pending--)); }
                    }
                    while(pending>0) yield return null;
                }
                else { yield return Execute(evt);CurrentStep++; }
            }
            foreach(var evt in sequence.finalEvents) director.Apply(evt);
            director.Progress.Set("Seen:"+sequence.id);director.Progress.Save();director.UI.Hide();
            IsCutscenePlaying=false;Paused=false;director.UI.Paused=false;Current=null;director.SetCinematic(false);OnCutsceneFinished?.Invoke(sequence);
        }
        IEnumerator Parallel(CutsceneEvent evt,Action done) { yield return Execute(evt);done(); }
        IEnumerator Execute(CutsceneEvent evt)
        {
            if(evt.action==StoryAction.Dialogue)
            {
                var conversation=director.Definition.dialogue.Find(evt.value);
                if(conversation==null) { Debug.LogError("Missing story dialogue: "+evt.value);yield break; }
                for(int i=0;i<conversation.lines.Count && !skip;i++)
                { director.UI.Show(evt.value,i);while(director.UI.IsTalking && director.UI.LineIndex==i && !skip) yield return null; }
                director.UI.Hide();yield break;
            }
            var actor=director.Actor(evt.target);var from=actor ? actor.transform.position : Vector3.zero;
            bool moving=evt.action==StoryAction.Move && actor && Vector3.Distance(from,evt.position)>.01f;
            if(evt.action==StoryAction.Move)director.SetActorWalking(actor,moving,evt.walkPlaybackSpeed);
            var camera=director.View;Vector3 cameraFrom=camera.transform.position;float zoomFrom=camera.orthographicSize,fadeFrom=director.UI.Fade;
            if(evt.action!=StoryAction.Move && evt.action!=StoryAction.Camera && evt.action!=StoryAction.Fade) director.Apply(evt);
            float elapsed=0;
            do
            {
                if(skip) { if(evt.action==StoryAction.CameraShake)camera.transform.position=cameraFrom;if(evt.action==StoryAction.Move)director.SetActorWalking(actor,false);yield break; }
                if(Paused) { if(moving)actor.GetComponent<CharacterAnimation>()?.SetFrozen(true);yield return null;continue; }
                if(moving)actor.GetComponent<CharacterAnimation>()?.SetFrozen(false);
                float progress=evt.duration<=0 ? 1 : Mathf.Clamp01(elapsed/evt.duration);
                if(evt.action==StoryAction.Move && actor)
                {
                    float travel=evt.easeMovement ? Mathf.SmoothStep(0,1,progress) : progress;
                    actor.transform.position=Vector3.Lerp(from,evt.position,travel);
                    if(!evt.preserveFacing)director.Face(actor,evt.position.x-from.x);
                    var animator=actor.GetComponent<CharacterAnimation>()?.animator;
                    if(moving && animator)animator.speed=evt.walkPlaybackSpeed*(evt.easeMovement ? Mathf.Clamp(6*progress*(1-progress),.25f,1.2f) : 1);
                }
                if(evt.action==StoryAction.Camera) { camera.transform.position=Vector3.Lerp(cameraFrom,evt.position,progress);camera.orthographicSize=Mathf.Lerp(zoomFrom,evt.amount,progress); }
                if(evt.action==StoryAction.CameraShake)camera.transform.position=cameraFrom+(Vector3)UnityEngine.Random.insideUnitCircle*evt.amount*(1-progress);
                if(evt.action==StoryAction.Fade) director.UI.SetFade(Mathf.Lerp(fadeFrom,evt.amount,progress),evt.color);
                elapsed+=Time.unscaledDeltaTime;
                if(progress>=1) break;
                yield return null;
            } while(true);
            if(evt.action==StoryAction.Move)director.SetActorWalking(actor,false);
            if(evt.action==StoryAction.CameraShake)camera.transform.position=cameraFrom;
        }
    }
}
