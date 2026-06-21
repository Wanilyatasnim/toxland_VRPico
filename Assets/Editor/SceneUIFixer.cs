using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

public class SceneUIFixer : EditorWindow
{
    [MenuItem("Tools/Fix Scene UI")]
    public static void FixUI()
    {
        Debug.Log("========== STARTING SCENE UI FIXES ==========");

        // 1. Fix EventSystem
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject eventSystemGo = null;
        
        if (eventSystems.Length == 0)
        {
            Debug.Log("🔧 Creating new EventSystem GameObject...");
            eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<XRUIInputModule>();
            Debug.Log("✅ Created EventSystem with XRUIInputModule.");
        }
        else
        {
            eventSystemGo = eventSystems[0].gameObject;
            Debug.Log($"🔧 Checking existing EventSystem: '{eventSystemGo.name}'");
            
            var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Debug.Log("🔧 Removing StandaloneInputModule...");
                Object.DestroyImmediate(standalone);
            }
            
            var xrui = eventSystemGo.GetComponent<XRUIInputModule>();
            if (xrui == null)
            {
                Debug.Log("🔧 Adding XRUIInputModule...");
                eventSystemGo.AddComponent<XRUIInputModule>();
            }
            Debug.Log("✅ EventSystem configuration verified.");
        }

        // 2. Fix Canvases
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Remove missing scripts (Oculus components, etc.)
            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);
            if (removedCount > 0)
            {
                Debug.Log($"✅ Removed {removedCount} missing script(s) from Canvas '{canvas.name}'.");
            }
            
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                // Ensure TrackedDeviceGraphicRaycaster is present
                var trackedRaycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (trackedRaycaster == null)
                {
                    // Remove standard raycaster if it exists, as it conflicts / is redundant
                    var standardRaycaster = canvas.GetComponent<GraphicRaycaster>();
                    if (standardRaycaster != null)
                    {
                        Object.DestroyImmediate(standardRaycaster);
                    }
                    
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                    Debug.Log($"✅ Added TrackedDeviceGraphicRaycaster to WorldSpace Canvas '{canvas.name}'.");
                }
                
                // Ensure event camera is assigned to main camera
                if (canvas.worldCamera == null && Camera.main != null)
                {
                    canvas.worldCamera = Camera.main;
                    Debug.Log($"✅ Assigned Main Camera as Event Camera for WorldSpace Canvas '{canvas.name}'.");
                }
            }
        }

        // 3. Fix InputActionManager
        var origin = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin");
        if (origin != null)
        {
            var iam = origin.GetComponent<InputActionManager>();
            if (iam != null)
            {
                Debug.Log("🔧 Checking InputActionManager on XR Origin...");
                SerializedObject so = new SerializedObject(iam);
                SerializedProperty actionAssetsProp = so.FindProperty("m_ActionAssets");
                
                if (actionAssetsProp != null)
                {
                    bool isEmpty = actionAssetsProp.arraySize == 0;
                    if (isEmpty)
                    {
                        Debug.LogWarning("⚠️ InputActionManager has no Action Assets! Finding 'XRI Default Input Actions'...");
                        string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
                            if (asset != null)
                            {
                                actionAssetsProp.InsertArrayElementAtIndex(0);
                                actionAssetsProp.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                                so.ApplyModifiedProperties();
                                Debug.Log($"✅ Successfully added '{asset.name}' to InputActionManager.");
                            }
                        }
                        else
                        {
                            Debug.LogError("❌ Could not find 'XRI Default Input Actions' asset in the project!");
                        }
                    }
                    else
                    {
                        Debug.Log("✅ InputActionManager has registered action assets.");
                    }
                }
            }
        }

        // 4. Fix XR Controllers (Add lasers if they only have ActionBasedController)
        var controllers = Object.FindObjectsByType<ActionBasedController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (controllers.Length == 0)
        {
            Debug.LogWarning("⚠️ No ActionBasedController components found in the scene.");
        }
        else
        {
            foreach (var controller in controllers)
            {
                Debug.Log($"🔧 Checking controller: '{controller.name}'");
                
                // Let's print out if input references are set or empty
                PrintControllerActions(controller);

                // Let's look for any type of Interactor
                bool hasInteractor = false;
                var components = controller.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c != null && c.GetType().Name.Contains("Interactor"))
                    {
                        hasInteractor = true;
                        break;
                    }
                }

                if (!hasInteractor)
                {
                    Debug.Log($"🔧 Controller '{controller.name}' has no Interactor. Adding XRRayInteractor components...");
                    var rayInteractor = controller.gameObject.AddComponent<XRRayInteractor>();
                    var lineRenderer = controller.gameObject.AddComponent<LineRenderer>();
                    lineRenderer.startWidth = 0.01f;
                    lineRenderer.endWidth = 0.01f;
                    lineRenderer.useWorldSpace = true;
                    var lineVisual = controller.gameObject.AddComponent<XRInteractorLineVisual>();
                    
                    Debug.Log($"✅ Added XRRayInteractor, LineRenderer, and XRInteractorLineVisual to '{controller.name}'.");
                }
                else
                {
                    Debug.Log($"✅ Controller '{controller.name}' already has an Interactor component.");
                }
            }
        }

        // 5. Scan & Output XR Origin hierarchy to help diagnose controllers
        if (origin != null)
        {
            Debug.Log($"ℹ️ [HIERARCHY] Found XR Origin: '{origin.name}'. Scanning children for controllers...");
            ScanHierarchy(origin.transform, "  ");
        }
        else
        {
            Debug.LogWarning("⚠️ Could not find XR Origin in the scene. Make sure you have one set up.");
        }

        Debug.Log("========== SCENE UI FIXES COMPLETE ==========");
    }

    private static void PrintControllerActions(ActionBasedController controller)
    {
        // Check if actions are unassigned
        bool posEmpty = controller.positionAction.reference == null;
        bool rotEmpty = controller.rotationAction.reference == null;
        bool selectEmpty = controller.selectAction.reference == null;
        bool uiPressEmpty = controller.uiPressAction.reference == null;

        Debug.Log($"   [INPUT ACTIONS] '{controller.name}': " +
                  $"Position={(!posEmpty ? controller.positionAction.reference.name : "EMPTY")}, " +
                  $"Rotation={(!rotEmpty ? controller.rotationAction.reference.name : "EMPTY")}, " +
                  $"Select={(!selectEmpty ? controller.selectAction.reference.name : "EMPTY")}, " +
                  $"UIPress={(!uiPressEmpty ? controller.uiPressAction.reference.name : "EMPTY")}");
    }

    private static void ScanHierarchy(Transform current, string indent)
    {
        var components = current.GetComponents<Component>();
        string compList = "";
        foreach (var c in components)
        {
            if (c == null)
            {
                compList += "[MISSING SCRIPT], ";
            }
            else
            {
                compList += c.GetType().Name + ", ";
            }
        }
        if (compList.Length > 2) compList = compList.Substring(0, compList.Length - 2);

        Debug.Log($"{indent}- {current.name} (Components: {compList})");

        // Recurse into children
        for (int i = 0; i < current.childCount; i++)
        {
            ScanHierarchy(current.GetChild(i), indent + "  ");
        }
    }
}
