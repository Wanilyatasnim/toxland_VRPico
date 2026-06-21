#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Toxland.Editor
{
    public static class MigrationHelper
    {
        [MenuItem("Tools/Add XR Device Simulator")]
        public static void AddXRDeviceSimulator()
        {
            string[] guids = AssetDatabase.FindAssets("XR Device Simulator t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    GameObject instantiated = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Undo.RegisterCreatedObjectUndo(instantiated, "Create XR Device Simulator");
                    Debug.Log($"[PICO Migration] Instantiated XR Device Simulator from '{path}' into the active scene.");
                    
                    var activeScene = EditorSceneManager.GetActiveScene();
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }
                else
                {
                    Debug.LogError("[PICO Migration] Found path for simulator prefab but failed to load it.");
                }
            }
            else
            {
                Debug.LogError("[PICO Migration] XR Device Simulator prefab not found in your assets. Please ensure you have imported the XR Device Simulator sample in Package Manager > XR Interaction Toolkit.");
            }
        }

        [MenuItem("Tools/Clean Missing Scripts from Scene")]
        public static void CleanMissingScripts()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            int totalRemoved = 0;
            int objectsCleaned = 0;

            // Collect every GameObject in the open scene (including all children)
            var allObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var root in activeScene.GetRootGameObjects())
                CollectAllChildren(root, allObjects);

            foreach (var go in allObjects)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    totalRemoved += removed;
                    objectsCleaned++;
                }
            }

            // Destroy any root GameObjects that are missing prefab assets (e.g. UIHelpers)
            int missingPrefabsRemoved = 0;
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (PrefabUtility.IsPrefabAssetMissing(root))
                {
                    Debug.Log($"[PICO Migration] Destroying missing prefab instance: {root.name}");
                    Undo.DestroyObjectImmediate(root);
                    missingPrefabsRemoved++;
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[PICO Migration] Clean complete. Removed {totalRemoved} missing scripts " +
                      $"from {objectsCleaned} GameObjects. Destroyed {missingPrefabsRemoved} missing prefab instances.");

            EditorUtility.DisplayDialog("Clean Missing Scripts",
                $"Done!\n\n" +
                $"Removed {totalRemoved} missing (Oculus) scripts from {objectsCleaned} GameObjects.\n" +
                $"Removed {missingPrefabsRemoved} missing prefab instances.\n\n" +
                "Scene saved.",
                "OK");
        }

        private static void CollectAllChildren(GameObject go, System.Collections.Generic.List<GameObject> list)
        {
            list.Add(go);
            foreach (Transform child in go.transform)
                CollectAllChildren(child.gameObject, list);
        }

        [MenuItem("Tools/Complete PICO Migration")]
        public static void RunMigration()
        {
            Debug.Log("[PICO Migration] Starting automated migration process...");

            // 1. Delete Oculus Loader Asset if it exists
            string oculusLoaderPath = "Assets/XR/Loaders/Oculus Loader.asset";
            if (File.Exists(oculusLoaderPath))
            {
                AssetDatabase.DeleteAsset(oculusLoaderPath);
                Debug.Log("[PICO Migration] Deleted Oculus Loader asset.");
            }

            // 2. Ensure MainScene is open
            string scenePath = "Assets/Scenes/MainScene.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                Debug.Log($"[PICO Migration] Opening scene: {scenePath}");
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // 3. Locate OVRPlayerController or OVR rigs
            GameObject ovrPlayer = GameObject.Find("OVRPlayerController");
            if (ovrPlayer == null)
            {
                // Fallback to searching by OVR components if renamed
                foreach (var go in activeScene.GetRootGameObjects())
                {
                    if (go.name.Contains("OVR") || go.GetComponentInChildren<CharacterController>() != null && go.name.Contains("Player"))
                    {
                        ovrPlayer = go;
                        break;
                    }
                }
            }

            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = Quaternion.identity;
            Transform playerParent = null;

            if (ovrPlayer != null)
            {
                spawnPosition = ovrPlayer.transform.position;
                spawnRotation = ovrPlayer.transform.rotation;
                playerParent = ovrPlayer.transform.parent;
                Debug.Log($"[PICO Migration] Found Oculus Player controller at position {spawnPosition}. Deleting it...");
                Undo.DestroyObjectImmediate(ovrPlayer);
            }
            else
            {
                Debug.LogWarning("[PICO Migration] OVRPlayerController not found in the scene. Spawning XR Origin at root origin.");
            }

            // 4. Create XR Origin (VR) programmatically
            GameObject xrOriginGo = new GameObject("XR Origin (VR)");
            xrOriginGo.transform.SetParent(playerParent);
            xrOriginGo.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            // Add XROrigin Component
            Type xrOriginType = GetTypeFromAssemblies("Unity.XR.CoreUtils.XROrigin");
            Component xrOrigin = null;
            if (xrOriginType != null)
            {
                xrOrigin = xrOriginGo.AddComponent(xrOriginType);
                Debug.Log("[PICO Migration] Added XROrigin component.");
            }
            else
            {
                Debug.LogError("[PICO Migration] XROrigin type not found! Ensure XR Core Utilities is installed.");
            }

            // Create Camera Offset
            GameObject cameraOffsetGo = new GameObject("Camera Offset");
            cameraOffsetGo.transform.SetParent(xrOriginGo.transform);
            cameraOffsetGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (xrOrigin != null)
            {
                var floorOffsetField = xrOriginType.GetProperty("CameraFloorOffsetObject") ?? xrOriginType.GetProperty("cameraFloorOffsetObject");
                if (floorOffsetField != null) floorOffsetField.SetValue(xrOrigin, cameraOffsetGo);
            }

            // Create Main Camera
            GameObject mainCameraGo = new GameObject("Main Camera");
            mainCameraGo.transform.SetParent(cameraOffsetGo.transform);
            mainCameraGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mainCameraGo.tag = "MainCamera";
            
            var camera = mainCameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            mainCameraGo.AddComponent<AudioListener>();

            // Add Tracked Pose Driver (Input System)
            Type tpdType = GetTypeFromAssemblies("UnityEngine.InputSystem.XR.TrackedPoseDriver") 
                         ?? GetTypeFromAssemblies("UnityEngine.SpatialTracking.TrackedPoseDriver");
            if (tpdType != null)
            {
                mainCameraGo.AddComponent(tpdType);
                Debug.Log($"[PICO Migration] Added TrackedPoseDriver component ({tpdType.Name}).");
            }

            if (xrOrigin != null)
            {
                var cameraField = xrOriginType.GetProperty("Camera") ?? xrOriginType.GetProperty("camera");
                if (cameraField != null) cameraField.SetValue(xrOrigin, camera);
            }

            // Create Left and Right Controllers
            GameObject leftControllerGo = new GameObject("LeftHand Controller");
            leftControllerGo.transform.SetParent(cameraOffsetGo.transform);
            leftControllerGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject rightControllerGo = new GameObject("RightHand Controller");
            rightControllerGo.transform.SetParent(cameraOffsetGo.transform);
            rightControllerGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            Type controllerType = GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.ActionBasedController");
            Component leftController = null;
            Component rightController = null;

            if (controllerType != null)
            {
                leftController = leftControllerGo.AddComponent(controllerType);
                rightController = rightControllerGo.AddComponent(controllerType);
                Debug.Log("[PICO Migration] Created Action-Based Hand Controllers.");
            }
            else
            {
                Debug.LogError("[PICO Migration] ActionBasedController type not found! Ensure XR Interaction Toolkit is installed.");
            }

            // 5. Add Input Action Manager
            Type iamType = GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager")
                         ?? GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.InputActionManager");
            if (iamType != null)
            {
                var iam = xrOriginGo.AddComponent(iamType);
                Debug.Log("[PICO Migration] Added InputActionManager to XR Origin.");

                // Assign Default XRI Input Actions Asset
                string[] actionAssetGuids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
                if (actionAssetGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(actionAssetGuids[0]);
                    var actionAsset = AssetDatabase.LoadAssetAtPath(path, typeof(ScriptableObject));
                    if (actionAsset != null)
                    {
                        var actionAssetsField = iamType.GetField("m_ActionAssets", BindingFlags.NonPublic | BindingFlags.Instance)
                                             ?? iamType.GetField("actionAssets", BindingFlags.Public | BindingFlags.Instance);
                        if (actionAssetsField != null)
                        {
                            var listType = typeof(List<>).MakeGenericType(GetTypeFromAssemblies("UnityEngine.InputSystem.InputActionAsset"));
                            var list = Activator.CreateInstance(listType);
                            var addMethod = listType.GetMethod("Add");
                            addMethod.Invoke(list, new object[] { actionAsset });
                            actionAssetsField.SetValue(iam, list);
                            Debug.Log($"[PICO Migration] Assigned '{path}' to InputActionManager.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[PICO Migration] 'XRI Default Input Actions' asset not found. Make sure Starter Assets sample is imported.");
                }
            }

            // 6. Apply Presets to Controllers
            ApplyPresetToController(leftController, "XRI Default Left Controller");
            ApplyPresetToController(rightController, "XRI Default Right Controller");

            // 7. Assign PICO Controller Models
            string picoLeftModelPath = "Packages/com.unity.xr.picoxr/Assets/Resources/Controller/PICO 4 L.prefab";
            string picoRightModelPath = "Packages/com.unity.xr.picoxr/Assets/Resources/Controller/PICO 4 R.prefab";

            GameObject leftModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(picoLeftModelPath);
            GameObject rightModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(picoRightModelPath);

            if (leftController != null && leftModelPrefab != null)
            {
                var modelPrefabProp = controllerType.GetProperty("modelPrefab") ?? controllerType.GetProperty("ModelPrefab");
                if (modelPrefabProp != null)
                {
                    modelPrefabProp.SetValue(leftController, leftModelPrefab.transform);
                    Debug.Log("[PICO Migration] Assigned PICO 4 Left Controller Model Prefab.");
                }
            }

            if (rightController != null && rightModelPrefab != null)
            {
                var modelPrefabProp = controllerType.GetProperty("modelPrefab") ?? controllerType.GetProperty("ModelPrefab");
                if (modelPrefabProp != null)
                {
                    modelPrefabProp.SetValue(rightController, rightModelPrefab.transform);
                    Debug.Log("[PICO Migration] Assigned PICO 4 Right Controller Model Prefab.");
                }
            }

            // 8. Add PXR_Manager component to XR Origin
            Type pxrManagerType = GetTypeFromAssemblies("Unity.XR.PXR.PXR_Manager");
            if (pxrManagerType != null)
            {
                xrOriginGo.AddComponent(pxrManagerType);
                Debug.Log("[PICO Migration] Added PXR_Manager component.");
            }
            else
            {
                Debug.LogWarning("[PICO Migration] PXR_Manager type not found! This is expected if the PICO package is still importing. You can manually add it to XR Origin once imports complete.");
            }

            // 9. Rebind references in AvatarInputConverter
            var avatarConverter = GameObject.FindAnyObjectByType<AvatarInputConverter>();
            if (avatarConverter != null)
            {
                avatarConverter.oculusHead = mainCameraGo.transform;
                avatarConverter.oculusHand_Left = leftControllerGo.transform;
                avatarConverter.oculusHand_Right = rightControllerGo.transform;
                EditorUtility.SetDirty(avatarConverter);
                Debug.Log("[PICO Migration] Re-bound AvatarInputConverter fields (oculusHead, oculusHand_Left, oculusHand_Right) to the new XR Origin.");
            }
            else
            {
                Debug.LogWarning("[PICO Migration] AvatarInputConverter component not found in the scene.");
            }

            // 10. Set Android Build Settings
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.Log("[PICO Migration] Configured Android build settings: Min API Level 29, IL2CPP, ARM64.");

            // 11. Configure XR Plugin Management (PICO Loader assignment)
            try
            {
                AssignPicoLoader();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PICO Migration] Could not automatically enable PICO Loader: {ex.Message}. You can easily enable it manually in Edit > Project Settings > XR Plug-in Management (check PICO on Android tab).");
            }

            // Save and mark dirty
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[PICO Migration] AUTOMATED MIGRATION COMPLETE! Please review the console for warnings and follow verification steps.");
        }

        private static Type GetTypeFromAssemblies(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        private static void ApplyPresetToController(Component controller, string presetName)
        {
            if (controller == null) return;
            string[] presetGuids = AssetDatabase.FindAssets($"{presetName} t:Preset");
            if (presetGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(presetGuids[0]);
                Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(path);
                if (preset != null)
                {
                    preset.ApplyTo(controller);
                    Debug.Log($"[PICO Migration] Applied Preset '{presetName}' to controller.");
                }
            }
            else
            {
                Debug.LogWarning($"[PICO Migration] Preset '{presetName}' not found. Make sure Starter Assets is imported.");
            }
        }

        private static void AssignPicoLoader()
        {
            Type storeType = GetTypeFromAssemblies("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
            if (storeType != null)
            {
                var assignLoaderMethod = storeType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                if (assignLoaderMethod != null)
                {
                    var xrSettings = XRGeneralSettings.Instance;
                    if (xrSettings != null && xrSettings.Manager != null)
                    {
                        // Assign Pico Loader: "Unity.XR.PXR.PXR_Loader"
                        assignLoaderMethod.Invoke(null, new object[] { xrSettings.Manager, "Unity.XR.PXR.PXR_Loader", BuildTargetGroup.Android });
                        Debug.Log("[PICO Migration] Programmatically assigned PICO Loader to Android XR Plug-in Management.");
                        
                        // Remove Oculus Loader: "Unity.XR.Oculus.OculusLoader"
                        var removeLoaderMethod = storeType.GetMethod("RemoveLoader", BindingFlags.Public | BindingFlags.Static);
                        if (removeLoaderMethod != null)
                        {
                            removeLoaderMethod.Invoke(null, new object[] { xrSettings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android });
                            Debug.Log("[PICO Migration] Programmatically removed Oculus Loader from Android XR Plug-in Management.");
                        }

                        // Standalone testing uses the XR Device Simulator directly without requiring HMD simulation plugins.
                    }
                }
            }
        }
    }
}
#endif
