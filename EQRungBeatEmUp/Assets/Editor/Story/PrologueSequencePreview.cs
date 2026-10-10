using System.Collections.Generic;
using System.Linq;
using BeatEmUp.Story;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Edit-mode reconstruction: never runs gameplay, changes save flags or creates scene objects.
public static class PrologueSequencePreview
{
    public sealed class Span
    {
        public int index,lane;
        public float start, duration;
        public float End => start+duration;
    }
    public sealed class Actor
    {
        public string name;
        public Vector3 position;
        public Sprite sprite;
        public float scale=1;
        public bool visible=true, flip;
        public Color color=Color.white;
    }
    public sealed class Frame
    {
        public readonly Dictionary<string,Actor> actors=new Dictionary<string,Actor>();
        public readonly List<string> notes=new List<string>();
        public Sprite environment, illustration;
        public float environmentWidth, fade, zoom=2.35f;
        public Color fadeColor=Color.black;
        public Vector3 camera=new Vector3(0,1.15f,-10);
        public DialogueLine dialogue;
        public string conversation;
        public int dialogueLine;
        public readonly List<CutsceneEvent> effects=new List<CutsceneEvent>();
    }
    public static List<Span> Schedule(CutsceneSequence sequence,DialogueDatabase database,float dialogueSeconds)
    {
        var spans=new List<Span>();float time=0;
        if(!sequence)return spans;
        for(int i=0;i<sequence.events.Count;)
        {
            int group=sequence.events[i].parallelGroup;float length=0;int lane=0;
            do
            {
                var evt=sequence.events[i];
                float duration=evt.action==StoryAction.Dialogue ? Mathf.Max(1,database?.Find(evt.value)?.lines.Count ?? 1)*Mathf.Max(.1f,dialogueSeconds) : Mathf.Max(0,evt.duration);
                spans.Add(new Span{index=i,lane=group>0 ? lane++ : 0,start=time,duration=duration});length=Mathf.Max(length,duration);i++;
            } while(group>0 && i<sequence.events.Count && sequence.events[i].parallelGroup==group);
            time+=length;
        }
        return spans;
    }
    public static float Length(List<Span> schedule) => schedule.Count==0 ? 0 : schedule.Max(s=>s.End);
    static Actor Add(Frame frame,string name,Sprite sprite,Vector3 position,bool visible=true,float scale=1)
    {
        var actor=new Actor{name=name,sprite=sprite,position=position,visible=visible,scale=scale};frame.actors[name]=actor;return actor;
    }
    public static Frame Evaluate(PrologueDefinition definition,CutsceneSequence sequence,float seconds,float dialogueSeconds=3,bool finalEvents=true,CutsceneSequence contextSequence=null)
    {
        var frame=new Frame();if(!definition || !sequence)return frame;
        var context=contextSequence ? contextSequence : sequence;
        bool fair=context==definition.reunion || context==definition.attack || context==definition.cornered || context==definition.escape || context==definition.festivalChickenMemory || context==definition.festivalPepsiMemory;
        bool sanctuary=context==definition.sanctuaryIntro || context==definition.returnPresent;
        frame.environment=fair ? definition.schoolFair : sanctuary ? definition.sanctuary : definition.hideout;
        frame.environmentWidth=fair ? definition.festivalWidth : 11;
        if(fair)for(int i=0;i<2;i++)
        {
            var sprite=i==0 ? definition.festivalChickenStore : definition.festivalPepsiStore;
            if(sprite)Add(frame,i==0 ? "ร้านไก่ป๊อป" : "Pepsi",sprite,definition.FestivalStorePosition(i),true,definition.festivalStoreWidth/Mathf.Max(.01f,sprite.bounds.size.x));
        }
        if(context==definition.festivalChickenMemory || context==definition.festivalPepsiMemory)
        {
            int index=context==definition.festivalChickenMemory ? 0 : 1;
            frame.camera.x=definition.FestivalStoreGround(index).x;
        }
        Add(frame,"EQ",definition.eq ? definition.eq.idlePose : null,new Vector3(-.7f,0));
        Add(frame,"Rung",definition.rung ? definition.rung.idlePose : null,new Vector3(.55f,0));
        Add(frame,"Prapot",definition.prapot,new Vector3(1.4f,.3f),sanctuary);
        Add(frame,"Cream",definition.cream,new Vector3(2,0),false);
        Add(frame,"Chanai",definition.chanai,new Vector3(2,.2f),false);
        if(fair)for(int i=0;i<4;i++)
        {
            var sprites=i==0 ? definition.studentIceCream : i==1 ? definition.studentDrink : i==2 ? definition.studentPhone : definition.studentWave;
            Add(frame,"Student"+i,sprites!=null && sprites.Length>0 ? sprites[0] : definition.student,new Vector3(-2.5f+i*1.4f,.25f+(i%2)*.08f),true,.75f).color=new Color(.88f,.77f,.70f);
        }
        var schedule=Schedule(sequence,definition.dialogue,dialogueSeconds);
        foreach(var span in schedule)
        {
            if(seconds<span.start)continue;
            var evt=sequence.events[span.index];float local=Mathf.Clamp(seconds-span.start,0,span.duration);
            float progress=span.duration<=0 ? 1 : Mathf.Clamp01(local/span.duration);
            Apply(frame,definition,evt,progress,Mathf.Max(0,seconds-span.start),seconds<span.End,dialogueSeconds);
        }
        if(finalEvents && seconds>=Length(schedule))foreach(var evt in sequence.finalEvents)Apply(frame,definition,evt,1,0,false,dialogueSeconds);
        return frame;
    }
    static void Apply(Frame frame,PrologueDefinition definition,CutsceneEvent evt,float progress,float local,bool active,float dialogueSeconds)
    {
        frame.actors.TryGetValue(evt.target ?? "",out var actor);
        switch(evt.action)
        {
            case StoryAction.Environment:
                frame.environment=evt.value=="Fair" ? definition.schoolFair : evt.value=="Sanctuary" ? definition.sanctuary : definition.hideout;
                frame.environmentWidth=evt.value=="Fair" ? definition.festivalWidth : 11;break;
            case StoryAction.Spawn:
                if(evt.prefab)
                {
                    var renderer=evt.prefab.GetComponentInChildren<SpriteRenderer>(true);
                    actor=Add(frame,evt.target,renderer ? renderer.sprite : evt.sprite,evt.position,true,evt.prefab.transform.localScale.x);
                }
                else if(actor!=null) { actor.position=evt.position;actor.visible=true;if(evt.sprite)actor.sprite=evt.sprite;else if(evt.target=="Cream")actor.sprite=definition.cream; }
                else if(evt.sprite)actor=Add(frame,evt.target,evt.sprite,evt.position);
                break;
            case StoryAction.Despawn: if(actor!=null)actor.visible=false;break;
            case StoryAction.Move:
                if(actor!=null)
                {
                    float delta=evt.position.x-actor.position.x;
                    actor.position=Vector3.Lerp(actor.position,evt.position,evt.easeMovement ? Mathf.SmoothStep(0,1,progress) : progress);
                    if(!evt.preserveFacing && Mathf.Abs(delta)>.001f)actor.flip=delta<0;
                    var character=evt.target=="EQ" ? definition.eq : evt.target=="Rung" ? definition.rung : null;
                    if(character)actor.sprite=active ? WalkSprite(character.locomotion,local*evt.walkPlaybackSpeed) ?? character.idlePose : character.idlePose;
                }
                break;
            case StoryAction.Face: if(actor!=null && Mathf.Abs(evt.amount)>.01f)actor.flip=evt.amount<0;break;
            case StoryAction.Pose:
                if(actor!=null)
                {
                    if(evt.sprite)actor.sprite=evt.sprite;
                    if(evt.frames!=null && evt.frames.Length>0)
                    {
                        int index=(int)(local*Mathf.Max(.01f,evt.framesPerSecond));
                        actor.sprite=evt.frames[evt.holdLastFrame ? Mathf.Min(index,evt.frames.Length-1) : index%evt.frames.Length];
                    }
                }
                break;
            case StoryAction.Camera:
                frame.camera=Vector3.Lerp(frame.camera,evt.position,progress);frame.zoom=Mathf.Lerp(frame.zoom,Mathf.Max(.1f,evt.amount),progress);break;
            case StoryAction.Fade: frame.fade=Mathf.Lerp(frame.fade,evt.amount,progress);frame.fadeColor=evt.color;break;
            case StoryAction.Dialogue:
                if(active)
                {
                    var conversation=definition.dialogue ? definition.dialogue.Find(evt.value) : null;
                    if(conversation!=null && conversation.lines.Count>0)
                    {
                        frame.conversation=conversation.id;frame.dialogueLine=Mathf.Clamp((int)(local/Mathf.Max(.1f,dialogueSeconds)),0,conversation.lines.Count-1);
                        frame.dialogue=conversation.lines[frame.dialogueLine];
                    }
                    else frame.notes.Add("Missing conversation: "+evt.value);
                }
                break;
            case StoryAction.Vfx: if(active)frame.effects.Add(evt);break;
            case StoryAction.Signal:
                if(evt.value=="HideHeroes" || evt.value=="ShowHeroes")foreach(string hero in new[]{"EQ","Rung"})frame.actors[hero].visible=evt.value=="ShowHeroes";
                else if(evt.value=="ShowEscape")frame.illustration=definition.escapeConceptArt;
                else if(evt.value=="HideEscape")frame.illustration=null;
                else if(evt.value=="Silhouette" && actor!=null)actor.color=new Color(.025f,.015f,.04f);
                else frame.notes.Add("Gameplay signal (Play Mode): "+evt.value);
                break;
            case StoryAction.Flag: frame.notes.Add("Flag: "+evt.value);break;
            case StoryAction.Sound: if(active)frame.notes.Add("Sound: "+(evt.sound ? evt.sound.name : "none"));break;
            case StoryAction.Music: frame.notes.Add("Music: "+(evt.sound ? evt.sound.name : "stop"));break;
            case StoryAction.Gameplay: frame.notes.Add(evt.amount<=0 ? "Gameplay locked" : "Gameplay enabled");break;
            case StoryAction.StageTransition: frame.notes.Add("Stage transition: "+evt.amount);break;
            case StoryAction.CameraShake: if(active)frame.notes.Add("Camera shake: "+evt.amount);break;
        }
    }
    public static Sprite WalkSprite(RuntimeAnimatorController controller,float seconds)
    {
        if(!controller)return null;
        AnimatorOverrideController overrides=controller as AnimatorOverrideController;
        var baseController=(overrides ? overrides.runtimeAnimatorController : controller) as AnimatorController;
        if(!baseController || baseController.layers.Length==0)return null;
        var clip=FindWalk(baseController.layers[0].stateMachine);
        if(overrides && clip)clip=overrides[clip.name];
        if(!clip)return null;
        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if(binding.type!=typeof(SpriteRenderer) || binding.propertyName!="m_Sprite")continue;
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);if(keys.Length==0)continue;
            float time=clip.length>0 ? Mathf.Repeat(seconds,clip.length) : 0;Sprite result=keys[0].value as Sprite;
            foreach(var key in keys) { if(key.time>time)break;result=key.value as Sprite; }return result;
        }
        return null;
    }
    static AnimationClip FindWalk(AnimatorStateMachine machine)
    {
        foreach(var state in machine.states)if(state.state.name=="Walk")return state.state.motion as AnimationClip;
        foreach(var child in machine.stateMachines) { var clip=FindWalk(child.stateMachine);if(clip)return clip; }return null;
    }
}
