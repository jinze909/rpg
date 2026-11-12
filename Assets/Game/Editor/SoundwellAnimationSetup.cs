using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rpg.Editor
{
    /// <summary>
    /// Unity Editor is the sole writer of M4 AnimationClips and AnimatorController.
    /// Prepare creates game-owned assets once; Verify only checks saved assets.
    /// ORK/Makinom vendor content and existing scene serialization are untouched.
    /// </summary>
    public static class SoundwellAnimationSetup
    {
        private const string Root = "Assets/Game/Resources/Soundwell";
        private const string ClipsFolder = Root + "/Clips";
        private const string ControllerPath = Root + "/PlaceholderPlayer.controller";
        private static readonly string[] Names = { "Idle", "Move", "Action", "Hit" };

        public static void Prepare()
        {
            bool hasController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null;
            int clipsPresent = Names.Count(name =>
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name)) != null);
            if (hasController && clipsPresent == Names.Length)
            {
                Validate();
                return;
            }
            if (hasController || clipsPresent != 0)
                throw new InvalidOperationException("Refusing to overwrite partially saved M4 animation assets.");

            EnsureFolder("Assets/Game/Resources", "Soundwell");
            EnsureFolder(Root, "Clips");
            CreateClip("Idle", 0.8f, 0.012f, 0f, 1.015f, 1.008f, true);
            CreateClip("Move", 0.38f, 0.065f, 0f, 1.04f, 0.975f, true);
            CreateClip("Action", 0.40f, 0.025f, 0.16f, 1.12f, 0.87f, false);
            CreateClip("Hit", 0.32f, -0.065f, -0.13f, 0.84f, 1.10f, false);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (string name in Names)
            {
                var state = machine.AddState(name);
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                if (name == "Idle")
                    machine.defaultState = state;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Validate();
        }

        public static void Validate()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length != 1)
                throw new InvalidOperationException("M4 requires one saved game-owned AnimatorController.");
            var machine = controller.layers[0].stateMachine;
            if (machine.defaultState == null || machine.defaultState.name != "Idle" ||
                machine.states.Length != Names.Length)
                throw new InvalidOperationException("M4 controller must own exactly Idle/Move/Action/Hit.");

            foreach (string name in Names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                var states = machine.states.Where(child => child.state.name == name).ToArray();
                if (clip == null || clip.length < 0.3f || states.Length != 1 ||
                    states[0].state.motion != clip)
                    throw new InvalidOperationException("Missing or mismatched M4 clip: " + name);

                // The authored clips use continuous Idle/Move but finite
                // Action/Hit cues. A bad saved loop flag can leave the hero
                // stuck attacking or freeze exploration even if curves exist.
                var loop = new SerializedObject(clip)
                    .FindProperty("m_AnimationClipSettings.m_LoopTime");
                bool shouldLoop = name == "Idle" || name == "Move";
                if (loop == null || loop.boolValue != shouldLoop ||
                    !Mathf.Approximately(clip.frameRate, 30f) ||
                    !Mathf.Approximately(states[0].state.speed, 1f))
                    throw new InvalidOperationException(
                        "M4 saved animation playback settings changed: " + name);

                var bindings = AnimationUtility.GetCurveBindings(clip);
                if (!bindings.Any(binding => binding.propertyName == "m_LocalPosition.x") ||
                    !bindings.Any(binding => binding.propertyName == "m_LocalPosition.y") ||
                    !bindings.Any(binding => binding.propertyName == "m_LocalScale.x") ||
                    !bindings.Any(binding => binding.propertyName == "m_LocalScale.y"))
                    throw new InvalidOperationException("M4 clip has no authored transform motion: " + name);
            }
        }

        private static string ClipPath(string name) => ClipsFolder + "/" + name + ".anim";

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void CreateClip(string name, float length, float rise, float lunge,
            float peakScaleX, float peakScaleY, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = 30f };
            SetCurve(clip, "m_LocalPosition.y", length, 0f, rise, 0f);
            SetCurve(clip, "m_LocalPosition.x", length, 0f, lunge, 0f);
            SetCurve(clip, "m_LocalScale.x", length, 1f, peakScaleX, 1f);
            SetCurve(clip, "m_LocalScale.y", length, 1f, peakScaleY, 1f);
            AssetDatabase.CreateAsset(clip, ClipPath(name));
            var serialized = new SerializedObject(clip);
            var loopFlag = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopFlag != null)
            {
                loopFlag.boolValue = loop;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(clip);
        }

        private static void SetCurve(AnimationClip clip, string property, float duration,
            float start, float middle, float end)
        {
            clip.SetCurve("", typeof(Transform), property, new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(duration / 2f, middle),
                new Keyframe(duration, end)));
        }
    }
}
