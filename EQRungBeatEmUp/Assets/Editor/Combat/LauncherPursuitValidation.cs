using System;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class LauncherPursuitValidation
{
    static bool started;
    static CombatClock clock;
    static GameObject p, e;
    static ComboController player;
    static EnemyHitReaction enemy;
    static float pMax, eMax;
    static readonly List<string> lines = new List<string>();
    static LauncherPursuitValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate launcher pursuit (Play Mode)")]
    public static void Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling) return; started=false; SessionState.SetBool("PursuitTuning",true); EditorApplication.EnterPlaymode(); }

    static void Poll()
    {
        if (started || !SessionState.GetBool("PursuitTuning",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        started=true; SessionState.SetBool("PursuitTuning",false);
        try
        {
            lines.Clear();
            foreach(var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach(var existing in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) existing.enabled=false;
            Fixture(.85f,1);
            player.RequestJump(); while (!player.motor.IsGrounded) Step(); float jumpApex=pMax;
            Fixture(.85f,1); enemy.GetComponentInChildren<CombatHurtbox>().Receive(player.launcher.frames[player.launcher.FirstActiveFrame].hitboxes[0],1);
            while (!enemy.motor.IsGrounded) Step(); float launchApex=eMax;
            Debug.Log("APEX player="+jumpApex+" enemy="+launchApex+" ratio="+(launchApex/jumpApex));
            lines.Add("distance,facing,jumpDelay,attackDelay,cancelFrame,forwardInput,result,playerMax,enemyMax,startDx,startDy,hit1Dx,hit1Dy,hit2Dx,hit2Dy,finishDx,finishDy");
            int total=0, passed=0, stationary=0, moving=0;
            foreach(float distance in new[]{.8f,1f}) foreach(int facing in new[]{1,-1}) foreach(int jd in new[]{0,4,8,12}) foreach(int ad in new[]{0,4,8}) foreach(int cancel in new[]{5,8,10}) foreach(float forward in new[]{0f,1f})
            {
                string result = Route(distance,facing,jd,ad,cancel,forward);
                total++; if(result=="PASS") { passed++; if(forward==0) stationary++; else moving++; }
            }
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PursuitTuningMatrix.csv",lines);
            Debug.Log("PURSUIT MATRIX "+passed+"/"+total+" stationary="+stationary+"/144 moving="+moving+"/144");
            if(launchApex/jumpApex<1.05f || launchApex/jumpApex>1.2f) throw new Exception("Launch apex is outside the intended pursuit range");
            if(passed!=total) throw new Exception("Some pursuit timing variants failed");
            Safety();
            Debug.Log("LAUNCHER PURSUIT VALIDATION PASSED");
            if(Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch(Exception exception) { Debug.LogException(exception); if(Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode(); }
    }
    static void Safety()
    {
        Fixture(4,1); player.attackPlayer.Play(player.launcher); player.RequestJump();
        Wait(()=>!player.motor.IsGrounded); Wait(()=>player.motor.IsGrounded);
        if(enemy.health.Current!=500 || !enemy.motor.IsGrounded || player.CurrentAttack) throw new Exception("Whiff launcher has an unexpected result");
        Debug.Log("PASS: Whiff launcher allows only the requested normal jump and never moves the enemy");
        Fixture(4,1); if(player.motor.jumpForce!=7.5f || player.motor.gravity!=14) throw new Exception("Normal jump values changed");
        player.motor.MoveInput=Vector2.right; player.RequestJump(); Step();
        if(Mathf.Abs(p.transform.position.x-player.motor.moveSpeed/60)>.0001f) throw new Exception("Ordinary forward jump movement changed");
        player.RequestAttack(); float x=p.transform.position.x; Step();
        if(Mathf.Abs(p.transform.position.x-x-player.motor.moveSpeed*.3f/60)>.0001f) throw new Exception("Authored air movement scale was not respected");
        Wait(()=>!player.CurrentAttack); Wait(()=>player.motor.IsGrounded);
        if(player.State!=CombatState.Idle || enemy.health.Current!=500) throw new Exception("Standalone air attack has an unexpected result");
        Debug.Log("PASS: Normal jump keeps its height and movement; standalone air attack moves, recovers and lands");
    }
    static void Fixture(float distance,int facing)
    {
        if(p) UnityEngine.Object.DestroyImmediate(p); if(e) UnityEngine.Object.DestroyImmediate(e);
        p=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab"));
        e=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        player=p.GetComponent<ComboController>(); enemy=e.GetComponent<EnemyHitReaction>(); e.GetComponent<EnemyCombat>().enabled=false;
        p.transform.position=Vector3.zero; e.transform.position=new Vector3(distance*facing,0,0); player.motor.Face(facing); player.motor.MoveInput=Vector2.zero;
        enemy.health.maximumHealth=500; enemy.health.Restore(); clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false; clock.combatFPS=60;
        pMax=eMax=0;
    }
    static void Step() { clock.StepFrame(); pMax=Mathf.Max(pMax,player.motor.Height); eMax=Mathf.Max(eMax,enemy.motor.Height); }
    static bool Wait(Func<bool> condition,int max=120) { for(int i=0;!condition()&&i<max;i++) Step(); return condition(); }
    static void Live(int count) { for(int i=0;i<count;) { bool live=!player.attackPlayer.IsFrozen; Step(); if(live)i++; } }
    static string Gap(int facing) => ((e.transform.position.x-p.transform.position.x)*facing).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+(enemy.motor.Height-player.motor.Height).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
    static string Route(float distance,int facing,int jd,int ad,int cancel,float forward)
    {
        Fixture(distance,facing); string result="PASS", start="0,0", h1="0,0",h2="0,0",h3="0,0";
        player.RequestAttack(); Wait(()=>player.attackPlayer.CurrentFrame==7); player.RequestAttack(); Wait(()=>player.attackPlayer.CurrentFrame==7); player.RequestLauncher();
        if(!Wait(()=>!enemy.motor.IsGrounded)) result="Launcher miss";
        else
        {
            Live(jd); player.RequestJump();
            if(!Wait(()=>!player.motor.IsGrounded)) result="Jump fail";
            else
            {
                player.motor.MoveInput=new Vector2(facing*forward,0); Live(ad); player.RequestAttack(); start=Gap(facing);
                float hp=enemy.health.Current;
                if(!Wait(()=>enemy.health.Current<hp || !player.CurrentAttack) || enemy.health.Current==hp) result="Air1 miss";
                else
                {
                    h1=Gap(facing); Wait(()=>!player.attackPlayer.IsFrozen && player.attackPlayer.CurrentFrame>=cancel); player.RequestAttack();
                    if(player.CurrentAttack!=player.airCombo[1]) result="Air2 cancel fail";
                    else
                    {
                        hp=enemy.health.Current;
                        if(!Wait(()=>enemy.health.Current<hp || !player.CurrentAttack) || enemy.health.Current==hp) result="Air2 miss";
                        else
                        {
                            h2=Gap(facing); Wait(()=>!player.attackPlayer.IsFrozen && player.attackPlayer.CurrentFrame>=cancel); player.RequestAttack();
                            if(player.CurrentAttack!=player.airCombo[2]) result="Air3 cancel fail";
                            else
                            {
                                hp=enemy.health.Current;
                                if(!Wait(()=>enemy.health.Current<hp || !player.CurrentAttack) || enemy.health.Current==hp) result="Air3 miss";
                                else { h3=Gap(facing); if(enemy.motor.VerticalVelocity>-8)result="No downward finisher"; }
                            }
                        }
                    }
                }
            }
        }
        Wait(()=>player.motor.IsGrounded&&enemy.motor.IsGrounded&&enemy.CanAct,240);
        lines.Add(distance+","+facing+","+jd+","+ad+","+cancel+","+forward+","+result+","+pMax+","+eMax+","+start+","+h1+","+h2+","+h3);
        return result;
    }
}
