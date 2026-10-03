using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Copy to an isolated Unity project's Assets/Editor with GetUp assets at Assets/GetUp.
public static class GetUpImportValidation
{
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/GetUp/ThaiBadBoy_GetUp.anim");
            Require(clip != null, "Clip imports");
            Debug.Log("Imported clip length=" + clip.length);
            if (Environment.GetCommandLineArgs().Contains("-buildGetUp"))
            {
                AssetDatabase.DeleteAsset("Assets/GetUp/ThaiBadBoy_GetUp.anim");
                clip = new AnimationClip { name = "ThaiBadBoy_GetUp", frameRate = 100 };
                AssetDatabase.CreateAsset(clip, "Assets/GetUp/ThaiBadBoy_GetUp.anim");
                float[] starts = { 0, .12f, .30f, .48f, .64f, .79f };
                var authoredKeys = new ObjectReferenceKeyframe[6];
                for (int i = 0; i < 6; i++) authoredKeys[i] = new ObjectReferenceKeyframe { time = starts[i], value = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/GetUp/ThaiBadBoy_GetUp_{Math.Min(i + 1, 5):00}.png") };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), authoredKeys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.stopTime = .8f;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
            }
            Require(Mathf.Abs(clip.length - .8f) < .001f, "Clip duration is 0.8 seconds; actual=" + clip.length);
            Require(!clip.isLooping, "Clip does not loop");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" && bindings[0].propertyName == "m_Sprite", "SpriteRenderer binding matches existing pack");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Require(keys.Length == 6, "Five poses and final hold key");
            var obj = new GameObject("GetUp validation");
            var renderer = obj.AddComponent<SpriteRenderer>();
            var animator = obj.AddComponent<Animator>();
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/GetUp/Test.controller");
            var state = controller.layers[0].stateMachine.AddState("GetUp");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            float[] times = { 0, .12f, .30f, .48f, .64f };
            for (int i = 0; i < 5; i++)
            {
                string asset = $"Assets/GetUp/ThaiBadBoy_GetUp_{i + 1:00}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
                Require(sprite != null && keys[i].value == sprite, "Frame sprite reference " + (i + 1));
                Require(sprite.rect.width == 160 && sprite.rect.height == 128, "Canvas " + (i + 1));
                Require(sprite.pivot == new Vector2(80, 8) && sprite.pixelsPerUnit == 100, "Pivot and scale " + (i + 1));
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "Pixel import settings " + (i + 1));
                animator.Play("GetUp", 0, (times[i] + .001f) / .8f);
                animator.Update(0);
                Require(renderer.sprite == sprite, "Clip samples pose " + (i + 1) + "; actual=" + (renderer.sprite ? renderer.sprite.name : "null"));
            }
            animator.Play("GetUp", 0, .799f / .8f);
            animator.Update(0);
            Require(renderer.sprite == keys[4].value, "Standing pose holds to end");
            UnityEngine.Object.DestroyImmediate(obj);
            string report = "PASS: five sprites import at 160x128, 100 PPU, pivot (80,8), point filtering, no mipmaps/compression.\nPASS: non-looping clip imports, lasts 0.8 seconds, resolves all five sprites and samples each pose plus the final standing hold.\n";
            File.WriteAllText("GetUpUnityValidation.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("GetUpUnityValidation.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
    private static void Require(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
    }
}
