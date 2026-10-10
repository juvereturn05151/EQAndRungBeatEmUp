using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp.Story;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PrologueEditorValidation
{
    static PrologueEditorValidation() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists("Temp/PrologueEditor.validate-request"))return;
        File.Delete("Temp/PrologueEditor.validate-request");Run();
    }
    [MenuItem("Beat Em Up/Story/Validate prologue editor")]
    public static void Run()
    {
        var results=new List<string>();var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();
        var definition=ScriptableObject.CreateInstance<PrologueDefinition>();var dialogue=ScriptableObject.CreateInstance<DialogueDatabase>();
        string save=PlayerPrefs.GetString(StoryProgress.SaveKey,"" );
        int sceneCount=UnityEngine.SceneManagement.SceneManager.sceneCount;
        try
        {
            definition.reunion=sequence;definition.dialogue=dialogue;
            dialogue.conversations.Add(new Conversation{id="test",lines=new List<DialogueLine>{new DialogueLine{speaker="EQ",english="First"},new DialogueLine{speaker="Rung",english="Second"}}});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Spawn,target="EQ",position=new Vector3(-3,0),duration=0});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Spawn,target="Rung",position=new Vector3(3,0),duration=0});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Move,target="EQ",position=new Vector3(-.5f,0),duration=2,parallelGroup=1});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Move,target="Rung",position=new Vector3(.5f,0),duration=3,parallelGroup=1});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Dialogue,value="test"});
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Move,target="EQ",position=Vector3.zero,duration=1,parallelGroup=1});
            sequence.finalEvents.Add(new CutsceneEvent{action=StoryAction.Move,target="EQ",position=new Vector3(4,0)});
            var schedule=PrologueSequencePreview.Schedule(sequence,dialogue,2);
            Check(schedule[2].start==schedule[3].start && schedule[4].start==3,"Parallel block waits for its longest event",results);
            Check(schedule[5].start==7 && PrologueSequencePreview.Length(schedule)==8,"Dialogue estimates and nonconsecutive parallel groups schedule correctly",results);
            var first=PrologueSequencePreview.Evaluate(definition,sequence,1,2);
            Check(Mathf.Abs(first.actors["EQ"].position.x+1.75f)<.001f && first.actors["Rung"].position.x<3,"Scrubbing interpolates both simultaneous movements",results);
            Check(!first.actors["EQ"].flip && first.actors["Rung"].flip,"Approaching actors face each other",results);
            sequence.events[2].easeMovement=true;
            var eased=PrologueSequencePreview.Evaluate(definition,sequence,.5f,2);
            Check(Mathf.Abs(eased.actors["EQ"].position.x-(-3+2.5f*Mathf.SmoothStep(0,1,.25f)))<.001f,"Preview respects authored movement easing",results);
            Check(PrologueSequencePreview.Evaluate(definition,sequence,5.5f,2).dialogue.english=="Second","Scrubbing selects the correct dialogue line",results);
            Check(PrologueSequencePreview.Evaluate(definition,sequence,8,2).actors["EQ"].position.x==4,"Finish / skip preview applies final events instantly",results);
            Check(PrologueSequencePreview.Evaluate(definition,sequence,8,2,false).actors["EQ"].position.x==0,"Final events can be excluded from reconstruction",results);
            Undo.IncrementCurrentGroup();Undo.RecordObject(sequence,"Test prologue editor undo");sequence.events[2].position=new Vector3(9,0);Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Check(sequence.events[2].position.x==-.5f,"Event position edits support Undo",results);
            var data=new SerializedObject(sequence);data.Update();var events=data.FindProperty("events");events.MoveArrayElement(2,3);data.ApplyModifiedProperties();
            Check(sequence.events[2].target=="Rung","Serialized event reordering persists",results);
            var real=AssetDatabase.LoadAssetAtPath<PrologueDefinition>("Assets/EQ_Rung_BeatEmUp/Story/Prologue.asset");
            Check(real && real.eq && real.rung && PrologueSequencePreview.WalkSprite(real.eq.locomotion,.1f) && PrologueSequencePreview.WalkSprite(real.rung.locomotion,.1f),"Both existing hero controllers provide preview walk sprites",results);
            var before=PrologueSequencePreview.Evaluate(real,real.reunion,1.5f);
            PrologueSequencePreview.Evaluate(real,real.reunion,40);
            var after=PrologueSequencePreview.Evaluate(real,real.reunion,1.5f);
            Check(before.actors["EQ"].position==after.actors["EQ"].position && before.actors["Rung"].position==after.actors["Rung"].position,"Backward scrubbing reconstructs deterministic positions",results);
            Check(PlayerPrefs.GetString(StoryProgress.SaveKey,"")==save && UnityEngine.SceneManagement.SceneManager.sceneCount==sceneCount,"Preview preserves story save and open scenes",results);
            definition.festivalWidth=30;definition.festivalLaneMin=-2;definition.festivalLaneMax=-.5f;
            Check(definition.FestivalArenaMin==new Vector2(-14.5f,-2) && definition.FestivalArenaMax==new Vector2(14.5f,-.5f),"Authored festival width and lane values supply the runtime arena bounds",results);
            sequence.events[0].duration=.5f;sequence.events[1].duration=.5f;
            int instant=PrologueSequenceEditor.MakeOpeningInstant(sequence);
            Check(instant==2 && sequence.events[0].duration==0 && sequence.events[1].duration==0 && sequence.events[2].duration>0,"Instant opening tool changes only leading placement events",results);
            var dispatched=new List<CutsceneEvent>();
            int firstTimed=CutsceneController.ApplyInstantEvents(sequence.events,0,dispatched.Add);
            Check(firstTimed==2 && dispatched.Count==2 && dispatched[0].target=="EQ" && dispatched[1].target=="Rung","Runtime dispatches zero-duration placement in one synchronous batch",results);
            Check(!CutsceneController.IsInstantEvent(new CutsceneEvent{action=StoryAction.Dialogue,duration=0}),"Instant batching preserves dialogue input waits",results);
            definition.tutorialAreaMin=new Vector2(4,-.3f);definition.tutorialAreaMax=new Vector2(-4,-1.5f);
            definition.tutorialPlayerStart=new Vector2(-2,-1);definition.tutorialPartnerStart=new Vector2(-1.4f,-1);
            definition.tutorialEnemyStart=new Vector2(3.8f,-.5f);definition.tutorialGoalX=-2.5f;
            var tutorial=PrologueSequenceEditor.TutorialFrame(definition,8);
            Check(definition.TutorialArenaMin==new Vector2(-4,-1.5f) && definition.TutorialArenaMax==new Vector2(4,-.3f),"Tutorial bounds normalize reversed corners",results);
            Check(tutorial.actors.Count==5 && tutorial.actors["Enemy 3"].position.x==4 && tutorial.actors["Enemy 1"].position.y==-.5f,"Tutorial preview clamps the three-enemy wave to authored bounds",results);
            Check(PrologueSequenceEditor.TutorialFrame(definition,0).actors.Count==2 && PrologueSequenceEditor.TutorialFrame(definition,4).actors.Count==3,"Tutorial step preview shows the appropriate opponents",results);
            var actor=new GameObject("Tutorial layout validation");actor.SetActive(false);
            try
            {
                var motor=actor.AddComponent<CharacterMotor>();
                PrologueDirector.ConfigureTutorialPlayer(definition,motor,0);
                Check(motor.arenaMin==definition.TutorialArenaMin && motor.arenaMax==definition.TutorialArenaMax && motor.transform.position==tutorial.actors["Player start"].position,"Runtime tutorial player bounds and placement match the preview",results);
                PrologueDirector.ConfigureTutorialPlayer(definition,motor,1);
                Check(motor.transform.position==tutorial.actors["Partner start"].position,"Runtime second player uses the authored partner spawn",results);
            }
            finally { Object.DestroyImmediate(actor); }
            definition.tutorialGoalX=9;Check(definition.TutorialGoal==4,"Tutorial movement goal remains reachable inside the walking area",results);
            Undo.IncrementCurrentGroup();Undo.RecordObject(definition,"Test tutorial placement undo");definition.tutorialAreaMin=new Vector2(12,12);Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Check(definition.tutorialAreaMin==new Vector2(4,-.3f),"Tutorial walking area supports Undo",results);
            Check(real.festivalChickenStore && real.festivalPepsiStore && real.festivalChickenMemory && real.festivalPepsiMemory,"Festival stores have sprites and editable memory sequences",results);
            Check(real.dialogue.Find("festival-chicken-memory").lines.Any(l=>l.english.Contains("ditch school")) && real.dialogue.Find("festival-pepsi-memory").lines.Any(l=>l.speaker=="EQ" && l.english.Contains("3 baht") && l.english.Contains("10 baht")),"Store dialogue includes both requested memories in the shared database",results);
            var memory=PrologueSequencePreview.Evaluate(real,real.festivalChickenMemory,1);
            Check(memory.environment==real.schoolFair && Mathf.Abs(memory.camera.x-real.FestivalStoreGround(0).x)<.001f && memory.actors.ContainsKey("ร้านไก่ป๊อป") && memory.actors.ContainsKey("Pepsi"),"Memory preview displays both stores and focuses the selected stall",results);
            results.Add("ALL PROLOGUE EDITOR CHECKS PASSED");
        }
        catch(Exception error) { results.Add("FAIL: "+error);Debug.LogException(error); }
        finally
        {
            Undo.ClearUndo(sequence);Undo.ClearUndo(definition);Object.DestroyImmediate(sequence);Object.DestroyImmediate(definition);Object.DestroyImmediate(dialogue);
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/PrologueEditorValidationResults.txt",results);Debug.Log(string.Join("\n",results));
        }
    }
    static void Check(bool condition,string message,List<string> results)
    {
        if(!condition)throw new Exception(message);results.Add("PASS: "+message);
    }
}
