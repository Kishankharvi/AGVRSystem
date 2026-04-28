using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-shot editor script that fixes the MainMenu scene:
/// 1. Hand tracking: tracking origin → Floor Level, add OVRMeshRenderer + SkinnedMeshRenderer
/// 2. UI interactivity: fix Button targetGraphic references, widen poke thresholds
/// 3. Audio: wire UIAudioFeedback._audioSource to VoiceSource or create dedicated source
/// </summary>
public static class MainMenuSceneFixer
{
    [MenuItem("AGVRSystem/Fix MainMenu Scene")]
    public static void Execute()
    {
        // Ensure we're in the right scene
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.name.Contains("MainMenu"))
        {
            Debug.LogError("[MainMenuSceneFixer] Active scene is not MainMenu. Open MainMenu scene first.");
            return;
        }

        int fixCount = 0;

        // ============================================================
        // FIX 1: Tracking Origin → Floor Level
        // ============================================================
        var ovrManager = Object.FindFirstObjectByType<OVRManager>();
        if (ovrManager != null)
        {
            // Use SerializedObject to set the tracking origin type
            var so = new SerializedObject(ovrManager);
            var trackingProp = so.FindProperty("_trackingOriginType");
            if (trackingProp != null)
            {
                // OVRManager.TrackingOrigin.FloorLevel = 1
                if (trackingProp.enumValueIndex != 1)
                {
                    trackingProp.enumValueIndex = 1;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(ovrManager);
                    Debug.Log("[Fix 1] Set OVRManager tracking origin to Floor Level");
                    fixCount++;
                }
                else
                {
                    Debug.Log("[Fix 1] Tracking origin already Floor Level — skipped");
                }
            }
            else
            {
                Debug.LogWarning("[Fix 1] Could not find _trackingOriginType property on OVRManager");
            }
        }
        else
        {
            Debug.LogWarning("[Fix 1] OVRManager not found in scene");
        }

        // ============================================================
        // FIX 2: Add OVRMeshRenderer to hand visuals so hands are visible
        // ============================================================
        FixHandVisual("OVRCameraRig/TrackingSpace/LeftHandAnchor/LeftHandVisual", ref fixCount);
        FixHandVisual("OVRCameraRig/TrackingSpace/RightHandAnchor/RightHandVisual", ref fixCount);

        // ============================================================
        // FIX 3: Fix Button targetGraphic references
        // ============================================================
        FixButtonTargetGraphic("SessionBoard/SessionCanvas/StartButton", "SessionBoard/SessionCanvas/StartButton/ButtonBG", ref fixCount);
        FixButtonTargetGraphic("SessionBoard/SessionCanvas/ThemeSection/ThemeToggleBtn", "SessionBoard/SessionCanvas/ThemeSection/ThemeToggleBtn/ThemeBtnBG", ref fixCount);

        // ============================================================
        // FIX 4: Widen HandPokeInteractor thresholds for more reliable poke detection
        // ============================================================
        var pokeInteractors = Object.FindObjectsByType<AGVRSystem.UI.HandPokeInteractor>(FindObjectsSortMode.None);
        foreach (var poke in pokeInteractors)
        {
            var pokeSO = new SerializedObject(poke);

            var radiusProp = pokeSO.FindProperty("_pokeRadius");
            var depthProp = pokeSO.FindProperty("_pokeDepthThreshold");

            bool changed = false;

            if (radiusProp != null && radiusProp.floatValue < 0.025f)
            {
                radiusProp.floatValue = 0.035f;
                changed = true;
            }

            if (depthProp != null && depthProp.floatValue < 0.015f)
            {
                depthProp.floatValue = 0.02f;
                changed = true;
            }

            if (changed)
            {
                pokeSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(poke);
                Debug.Log($"[Fix 4] Widened poke thresholds on {poke.gameObject.name} (radius=0.035, depth=0.02)");
                fixCount++;
            }
        }

        // ============================================================
        // FIX 5: Wire UIAudioFeedback._audioSource
        // ============================================================
        var uiAudio = Object.FindFirstObjectByType<AGVRSystem.Audio.UIAudioFeedback>();
        if (uiAudio != null)
        {
            var uiAudioSO = new SerializedObject(uiAudio);
            var audioSourceProp = uiAudioSO.FindProperty("_audioSource");

            if (audioSourceProp != null && audioSourceProp.objectReferenceValue == null)
            {
                // Check if there's already an AudioSource on the AudioSystem gameobject
                AudioSource existingSource = uiAudio.GetComponent<AudioSource>();
                if (existingSource == null)
                {
                    existingSource = uiAudio.gameObject.AddComponent<AudioSource>();
                    existingSource.playOnAwake = false;
                    existingSource.spatialBlend = 0f;
                    existingSource.priority = 64;
                    Debug.Log("[Fix 5] Created dedicated AudioSource on AudioSystem for UIAudioFeedback");
                }

                audioSourceProp.objectReferenceValue = existingSource;
                uiAudioSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(uiAudio);
                Debug.Log("[Fix 5] Wired UIAudioFeedback._audioSource");
                fixCount++;
            }
            else
            {
                Debug.Log("[Fix 5] UIAudioFeedback._audioSource already assigned — skipped");
            }
        }
        else
        {
            Debug.LogWarning("[Fix 5] UIAudioFeedback not found in scene");
        }

        // ============================================================
        // FIX 6: Ensure TTS agent reference is correct on TTSVoiceGuide
        // ============================================================
        var ttsGuide = Object.FindFirstObjectByType<AGVRSystem.Audio.TTSVoiceGuide>();
        if (ttsGuide != null)
        {
            var ttsSO = new SerializedObject(ttsGuide);
            var agentProp = ttsSO.FindProperty("_ttsAgentComponent");
            if (agentProp != null && agentProp.objectReferenceValue == null)
            {
                // Find the TextToSpeechAgent in scene
                var ttsGO = GameObject.Find("[BuildingBlock] Text To Speech");
                if (ttsGO != null)
                {
                    // Get all MonoBehaviours and find the TTS agent
                    var monos = ttsGO.GetComponents<MonoBehaviour>();
                    foreach (var mono in monos)
                    {
                        if (mono != null && mono.GetType().Name.Contains("TextToSpeech"))
                        {
                            agentProp.objectReferenceValue = mono;
                            ttsSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(ttsGuide);
                            Debug.Log($"[Fix 6] Wired TTSVoiceGuide._ttsAgentComponent to {mono.GetType().Name}");
                            fixCount++;
                            break;
                        }
                    }
                }
            }
            else
            {
                Debug.Log("[Fix 6] TTSVoiceGuide._ttsAgentComponent already assigned — skipped");
            }
        }

        // ============================================================
        // FIX 7: Ensure SessionCanvas worldCamera is assigned
        // ============================================================
        FixCanvasWorldCamera("SessionBoard/SessionCanvas", ref fixCount);
        FixCanvasWorldCamera("AboutBoard/AboutCanvas", ref fixCount);

        // ============================================================
        // Save
        // ============================================================
        if (fixCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[MainMenuSceneFixer] Applied {fixCount} fixes and saved scene.");
        }
        else
        {
            Debug.Log("[MainMenuSceneFixer] No fixes needed — everything looks correct.");
        }
    }

    private static void FixHandVisual(string path, ref int fixCount)
    {
        var go = GameObject.Find(path);
        if (go == null)
        {
            Debug.LogWarning($"[Fix 2] Could not find {path}");
            return;
        }

        // Check if OVRMeshRenderer already exists
        var existingRenderer = go.GetComponent("OVRMeshRenderer") as Component;
        if (existingRenderer != null)
        {
            Debug.Log($"[Fix 2] {go.name} already has OVRMeshRenderer — skipped");
            return;
        }

        // Add SkinnedMeshRenderer if missing (OVRMeshRenderer requires it)
        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr == null)
        {
            smr = go.AddComponent<SkinnedMeshRenderer>();
            Debug.Log($"[Fix 2] Added SkinnedMeshRenderer to {go.name}");
        }

        // Add OVRMeshRenderer
        var meshRendererType = System.Type.GetType("OVRMeshRenderer, Assembly-CSharp") 
            ?? System.Type.GetType("OVRMeshRenderer, Meta.XR.SDK.Core")
            ?? System.Type.GetType("OVRMeshRenderer, Oculus.VR");

        // Try to find it in all loaded assemblies
        if (meshRendererType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                meshRendererType = asm.GetType("OVRMeshRenderer");
                if (meshRendererType != null) break;
            }
        }

        if (meshRendererType != null)
        {
            var renderer = go.AddComponent(meshRendererType);
            if (renderer != null)
            {
                // Configure via SerializedObject
                var so = new SerializedObject(renderer);

                // Set the mesh filter reference to the OVRMesh on the same object
                var ovrMesh = go.GetComponent("OVRMesh");
                var meshFilterProp = so.FindProperty("_ovrMesh");
                if (meshFilterProp != null && ovrMesh != null)
                {
                    meshFilterProp.objectReferenceValue = ovrMesh;
                }

                // Set the skeleton reference
                var ovrSkeleton = go.GetComponent("OVRSkeleton");
                var skeletonProp = so.FindProperty("_ovrSkeleton");
                if (skeletonProp != null && ovrSkeleton != null)
                {
                    skeletonProp.objectReferenceValue = ovrSkeleton;
                }

                // Try to assign a hand material
                var materialProp = so.FindProperty("_systemGestureMaterial");
                if (materialProp == null)
                {
                    materialProp = so.FindProperty("_handMaterial");
                }

                // Try to find the HandMaterial in the project
                if (materialProp != null)
                {
                    string[] guids = AssetDatabase.FindAssets("HandMaterial t:Material");
                    if (guids.Length > 0)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (mat != null)
                        {
                            materialProp.objectReferenceValue = mat;
                        }
                    }
                    else
                    {
                        // Try HandGhost material
                        guids = AssetDatabase.FindAssets("HandGhost t:Material");
                        if (guids.Length > 0)
                        {
                            string matPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                            if (mat != null)
                            {
                                materialProp.objectReferenceValue = mat;
                            }
                        }
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(go);
                Debug.Log($"[Fix 2] Added OVRMeshRenderer to {go.name}");
                fixCount++;
            }
        }
        else
        {
            Debug.LogWarning($"[Fix 2] Could not find OVRMeshRenderer type. Hand mesh rendering won't work.");
        }
    }

    private static void FixButtonTargetGraphic(string buttonPath, string graphicPath, ref int fixCount)
    {
        var buttonGO = GameObject.Find(buttonPath);
        var graphicGO = GameObject.Find(graphicPath);

        if (buttonGO == null)
        {
            Debug.LogWarning($"[Fix 3] Could not find button at {buttonPath}");
            return;
        }

        var button = buttonGO.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"[Fix 3] No Button component on {buttonPath}");
            return;
        }

        if (button.targetGraphic != null)
        {
            Debug.Log($"[Fix 3] {buttonGO.name} targetGraphic already assigned — skipped");
            return;
        }

        Graphic graphic = null;

        // First try the specified graphic child
        if (graphicGO != null)
        {
            graphic = graphicGO.GetComponent<Graphic>();
        }

        // Fallback: look for any Image on the button itself or children
        if (graphic == null)
        {
            graphic = buttonGO.GetComponentInChildren<Graphic>();
        }

        if (graphic != null)
        {
            // Ensure the graphic has raycastTarget enabled
            graphic.raycastTarget = true;

            var buttonSO = new SerializedObject(button);
            var targetProp = buttonSO.FindProperty("m_TargetGraphic");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = graphic;
                buttonSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(button);
                Debug.Log($"[Fix 3] Set {buttonGO.name}.targetGraphic to {graphic.gameObject.name}");
                fixCount++;
            }
        }
        else
        {
            Debug.LogWarning($"[Fix 3] No Graphic found for button {buttonPath}");
        }
    }

    private static void FixCanvasWorldCamera(string canvasPath, ref int fixCount)
    {
        var canvasGO = GameObject.Find(canvasPath);
        if (canvasGO == null) return;

        var canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;

        if (canvas.worldCamera != null) 
        {
            Debug.Log($"[Fix 7] {canvasGO.name} worldCamera already assigned — skipped");
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            canvas.worldCamera = mainCam;
            EditorUtility.SetDirty(canvas);
            Debug.Log($"[Fix 7] Assigned worldCamera to {canvasGO.name}");
            fixCount++;
        }
    }
}
