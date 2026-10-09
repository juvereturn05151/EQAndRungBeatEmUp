using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerGroundIndicatorValidation
{
    const string Pending = "PlayerGroundIndicator.Validation";
    static readonly List<string> results = new List<string>();
    static GameObject root; static MultiplayerSession session; static StageFlowController flow; static Camera camera;
    static PlayerGroundIndicatorStyle style; static CombatClock clock;
    static readonly List<PlayerIdentity> players = new List<PlayerIdentity>();
    static PlayerGroundIndicatorValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Players/Validate ground indicators (Play Mode)")]
    public static void BuildAndValidate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        PlayerGroundIndicatorSetup.Build(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); results.Add("PASS: " + message); }
    static void Set(object instance, string property, object value) => instance.GetType().GetProperty(property).SetValue(instance, value);
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear(); bool passed = false;
        try { Fixture(); PlayersAndAerials(); InspectorAndStability(); StagesAndPreview(); Network(); passed = true; }
        catch (Exception e) { results.Add("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            foreach (var player in players) if (player) Object.DestroyImmediate(player.gameObject); players.Clear();
            if (session) Object.DestroyImmediate(session.gameObject); if (root) Object.DestroyImmediate(root);
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerGroundIndicatorValidationResults.txt", results);
            Debug.Log("PLAYER GROUND INDICATOR VALIDATION " + (passed ? "PASSED" : "FAILED"));
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
        }
    }
    static void Fixture()
    {
        root = new GameObject("Ground indicator validation");
        var view = new GameObject("Camera"); view.transform.SetParent(root.transform); camera = view.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2.2f; camera.transform.position = new Vector3(0, 1.2f, -10);
        flow = root.AddComponent<StageFlowController>(); flow.enabled = false; flow.showHud = false;
        flow.level = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath));
        flow.background = Renderer("Background"); flow.floor = Renderer("Floor");
        var sessionRoot = new GameObject("Session validation"); session = sessionRoot.AddComponent<MultiplayerSession>(); session.enabled = false;
        Set(session, "Mode", SessionMode.Local); Set(session, "Flow", flow);
        style = AssetDatabase.LoadAssetAtPath<PlayerGroundIndicatorStyle>(PlayerGroundIndicatorSetup.StylePath);
        for (int slot = 0; slot < 4; slot++)
        {
            var selection = new LobbySlot { slot = slot, character = slot % session.catalog.characters.Length, owner = (ulong)(100 + slot), name = "P" + (slot + 1) };
            session.Lobby.slots.Add(selection);
            typeof(MultiplayerSession).GetMethod("SpawnPlayer", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { selection });
            var player = PlayerRoster.Players.Single(p => p.slot == slot); players.Add(player);
            var expected = GameplayPlayerSpawner.ResolvePrefab(session.catalog, selection);
            Check(player.SpawnedPrefab == expected, "P" + (slot + 1) + " uses the existing selected-prefab spawner");
            player.Motor.ResetForStage(new Vector2(-2.4f + slot * 1.6f, 0)); player.Motor.Simulate(0);
        }
        flow.player = players[0].Motor; Set(flow, "StageIndex", 1);
        clock = Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
    }
    static SpriteRenderer Renderer(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); return go.AddComponent<SpriteRenderer>();
    }
    static PlayerGroundIndicator Indicator(PlayerIdentity player) => player.GetComponentInChildren<PlayerGroundIndicator>();
    static void Grounded(PlayerIdentity player, string action)
    {
        var indicator = Indicator(player); indicator.Refresh(); var expected = player.Motor.transform.position + (Vector3)style.groundOffset;
        Check((indicator.shadow.transform.position - expected).sqrMagnitude < .000001f && indicator.ring.transform.position == indicator.shadow.transform.position, "P" + (player.slot + 1) + " shadow/ring share ground anchor during " + action);
        Check(indicator.shadow.sortingOrder < player.Motor.sprite.sortingOrder && indicator.ring.sortingOrder < indicator.shadow.sortingOrder && indicator.ring.sortingOrder > -900, "P" + (player.slot + 1) + " retains body/shadow/ring/floor hierarchy during " + action);
    }
    static void PlayersAndAerials()
    {
        Check(style.playerColors.Length == 4 && style.playerColors.Distinct().Count() == 4, "Four distinct blue/red/green/yellow slot colors are configured");
        Check(style.playerColors[0].b > style.playerColors[0].r && style.playerColors[1].r > style.playerColors[1].b && style.playerColors[2].g > style.playerColors[2].r && style.playerColors[3].r > .9f && style.playerColors[3].g > .7f, "Player slot color order matches requested palette");
        foreach (var player in players)
        {
            var indicator = Indicator(player); var body = player.Motor.sprite;
            Check(indicator && player.GetComponentsInChildren<PlayerGroundIndicator>().Length == 1, "P" + (player.slot + 1) + " has exactly one reusable indicator");
            Check(!indicator.transform.IsChildOf(player.Motor.visual) && indicator.GetComponentsInChildren<Collider2D>().Length == 0, "Indicator is outside the airborne visual subtree and has no collision components");
            var color = style.ColorForSlot(player.slot); color.a *= style.ringOpacity; indicator.Refresh();
            Check(indicator.PlayerIndex == player.slot && indicator.ring.color == color, "P" + (player.slot + 1) + " color comes from PlayerIdentity.slot");
            Check(CharacterPortraitUI.PlayerColors[player.slot] == style.ColorForSlot(player.slot), "Character selection uses the same P" + (player.slot + 1) + " palette");
            var menuFixture = new GameObject("Palette fixture"); menuFixture.SetActive(false);
            var menuColors = (Color[])typeof(MultiplayerMenu).GetProperty("colors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(menuFixture.AddComponent<MultiplayerMenu>());
            Object.DestroyImmediate(menuFixture);
            Check(menuColors[player.slot] == style.ColorForSlot(player.slot), "Session HUD uses the same slot palette");
            Grounded(player, "idle"); player.Motor.MoveInput = Vector2.right; player.Motor.Simulate(.05f); Grounded(player, "walking"); player.Motor.MoveInput = Vector2.zero;
            Check(player.Motor.Jump(), "Existing jump still accepts P" + (player.slot + 1));
            for (int f = 0; f < 8; f++) clock.StepFrame(); Grounded(player, "jump");
            Check(body.transform.position.y > indicator.ring.transform.position.y + .1f, "P" + (player.slot + 1) + " body rises while indicator stays on floor");
            var combo = player.GetComponent<ComboController>(); combo.RequestAttack(); clock.StepFrame();
            Check(combo.State == CombatState.AirAttack, "P" + (player.slot + 1) + " existing aerial attack still enters normally"); Grounded(player, "air attack");
            player.Motor.Launch(5, 1); clock.StepFrame(); Grounded(player, "launch/recoil");
            player.Motor.Face(-1); Grounded(player, "left facing"); Check(!indicator.ring.flipX && !indicator.shadow.flipX, "Facing does not flip or shift symmetric ground art");
            var position = player.Motor.transform.position; player.Motor.ResetForStage(position); player.Motor.Simulate(0); Grounded(player, "stage reset");
            var next = session.catalog.characters[(player.character + 1) % session.catalog.characters.Length]; player.GetComponent<PlayerCharacterLoadout>().Apply(next); indicator.Refresh();
            Check(indicator.ring.color == color && indicator.PlayerIndex == player.slot, "Changing selected character preserves player slot color");
        }
    }
    static void InspectorAndStability()
    {
        var indicator = Indicator(players[0]); var original = indicator.style; var custom = Object.Instantiate(style); indicator.style = custom;
        custom.shadowSize = new Vector2(.8f, .3f); custom.ringSize = new Vector2(1.1f, .4f); custom.shadowOpacity = .2f; custom.ringOpacity = .5f;
        custom.playerColors[0] = Color.cyan;
        foreach (int thickness in new[] { 1, 2, 3, 4 }) { custom.ringThicknessPixels = thickness; indicator.Refresh(); Check(indicator.ring.sprite == custom.ringSprites[thickness - 1], "Inspector ring thickness " + thickness + " selects a prebuilt sprite"); }
        Check(Mathf.Abs(indicator.shadow.bounds.size.x - .8f) < .001f && Mathf.Abs(indicator.ring.bounds.size.x - 1.1f) < .001f && indicator.shadow.color.a == .2f && indicator.ring.color.a == .5f && indicator.ring.color.g == 1, "Inspector size, opacity and palette update the shared visual");
        indicator.style = original; Object.DestroyImmediate(custom); indicator.Refresh();
        int objects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        var material = indicator.ring.sharedMaterial; var texture = indicator.ring.sprite.texture; var sprites = indicator.GetComponentsInChildren<SpriteRenderer>();
        var state = players[0].GetComponent<ComboController>().State; var position = players[0].transform.position;
        for (int f = 0; f < 600; f++) indicator.Refresh();
        Check(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length == objects && indicator.ring.sharedMaterial == material && indicator.ring.sprite.texture == texture && indicator.GetComponentsInChildren<SpriteRenderer>().SequenceEqual(sprites), "600 visual refreshes reuse the same objects, material, renderers and texture");
        Check(players[0].transform.position == position && players[0].GetComponent<ComboController>().State == state, "Visual refresh cannot mutate movement or combat state");
        indicator.enabled = false; Check(!indicator.ring.enabled && !indicator.shadow.enabled, "Disabling indicator hides both renderers"); indicator.enabled = true; Grounded(players[0], "reenable");
    }
    static void StagesAndPreview()
    {
        for (int i = 0; i < flow.level.stages.Count; i++)
        {
            var stage = flow.level.stages[i]; flow.EnterStage(i);
            foreach (var player in players) { player.Motor.Simulate(0); Grounded(player, stage.stageName + " entry"); }
            Check(string.IsNullOrEmpty(flow.Failure), "Existing stage entry and art load successfully for " + stage.stageName);
            Check(players.All(p => Indicator(p).shadow.enabled && Indicator(p).ring.enabled && Indicator(p).ring.sortingOrder > -900), "Indicators remain visible and above floor in " + stage.stageName);
        }
        int entrance = flow.level.stages.FindIndex(s => s.stageId == "Stage01_EntranceGate"); flow.EnterStage(entrance);
        foreach (var player in players) { player.Motor.ResetForStage(new Vector2(-2.4f + player.slot * 1.6f, -.1f)); player.Motor.Simulate(0); }
        players[1].Motor.SetAuthoredHeight(1.1f); players[2].Motor.SetAuthoredHeight(.6f);
        foreach (var player in players) Indicator(player).Refresh();
        Directory.CreateDirectory("Documentation/PlayerGroundIndicatorPreview");
        var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target; var picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); picture.Apply(); File.WriteAllBytes("Documentation/PlayerGroundIndicatorPreview/Stage1.png", picture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(picture); Object.DestroyImmediate(target);
    }
    static void Network()
    {
        var snapshot = session.CaptureSnapshot();
        foreach (var player in players)
        {
            var indicator = Indicator(player);
            foreach (var renderer in new[] { indicator.shadow, indicator.ring })
            {
                var state = snapshot.sprites.Single(s => s.id == renderer.GetInstanceID());
                Check(state.sprite >= 0 && session.catalog.SpriteAt(state.sprite) == renderer.sprite && state.position == renderer.transform.position && state.color == renderer.color && state.order == renderer.sortingOrder, "Host snapshot captures indexed grounded " + renderer.name + " for P" + (player.slot + 1));
            }
        }
        var received = JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(snapshot));
        Set(session, "Mode", SessionMode.Online); typeof(MultiplayerSession).GetField("replicaRoot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, new GameObject("Replica fixture"));
        session.ApplySnapshot(received);
        var replicas = (Dictionary<int, SpriteRenderer>)typeof(MultiplayerSession).GetField("replicas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
        foreach (var player in players)
        {
            var indicator = Indicator(player);
            foreach (var renderer in new[] { indicator.shadow, indicator.ring })
            {
                var replica = replicas[renderer.GetInstanceID()];
                Check(replica.sprite == renderer.sprite && replica.color == renderer.color && replica.transform.position == renderer.transform.position && replica.sortingOrder == renderer.sortingOrder, "Real client snapshot application preserves P" + (player.slot + 1) + " " + renderer.name + " color, ground position and hierarchy");
            }
            float bodyY = replicas[player.Motor.sprite.GetInstanceID()].transform.position.y;
            float ringY = replicas[indicator.ring.GetInstanceID()].transform.position.y;
            Check(Mathf.Abs(bodyY - player.transform.position.y - player.Motor.Height) < .0001f && Mathf.Abs(ringY - player.transform.position.y - style.groundOffset.y) < .0001f, "Client body and indicator use independent airborne and ground positions");
        }
        int count = replicas.Count; received.tick++; session.ApplySnapshot(received);
        Check(replicas.Count == count, "Repeated network snapshot reuses existing indicator replicas");
        int removeRing = Indicator(players[3]).ring.GetInstanceID(); received.sprites.RemoveAll(s => s.id == removeRing); received.tick++; session.ApplySnapshot(received);
        Check(!replicas.ContainsKey(removeRing), "Existing replica cleanup removes departed ground visuals");
        foreach (var replica in replicas.Values) if (replica) Object.DestroyImmediate(replica.gameObject);
        Object.DestroyImmediate((GameObject)typeof(MultiplayerSession).GetField("replicaRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session));
    }
}
