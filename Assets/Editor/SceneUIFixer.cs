using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEditor.Presets;
using System.Reflection;
using System.Collections.Generic;

public class SceneUIFixer : EditorWindow
{
    [MenuItem("Tools/Fix Scene UI")]
    public static void FixUI()
    {
        Debug.Log("========== STARTING SCENE UI FIXES ==========");

        // 1. Fix EventSystem
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        GameObject eventSystemGo = null;
        
        if (eventSystems.Length == 0)
        {
            Debug.Log("🔧 Creating new EventSystem GameObject...");
            eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            var xrui = eventSystemGo.AddComponent<XRUIInputModule>();
            ApplyInputModulePreset(xrui);
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
                xrui = eventSystemGo.AddComponent<XRUIInputModule>();
            }
            
            ApplyInputModulePreset(xrui);
            InspectInputModule(xrui);
            Debug.Log("✅ EventSystem configuration verified.");
        }

        // 2. Fix Canvases
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
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

        // 4. Fix XR Controllers (Clean old interactors and add new XRI 3.x ones)
        var controllers = Object.FindObjectsByType<ActionBasedController>(FindObjectsInactive.Include);
        if (controllers.Length == 0)
        {
            Debug.LogWarning("⚠️ No ActionBasedController components found in the scene.");
        }
        else
        {
            foreach (var controller in controllers)
            {
                Debug.Log($"🔧 Re-initializing XRI 3.x Interactors on controller: '{controller.name}'");

                // Auto-bind actions using the correct namespaces/prefixes we found
                bool isLeft = controller.name.ToLower().Contains("left");
                FixControllerBindings(controller, isLeft);
                
                // Let's print out the actions after binding
                PrintControllerActions(controller);

                // Resolve namespaces dynamically using AppDomain assembly scan to avoid null type crashes
                System.Type rayType = GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor")
                                   ?? GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.XRRayInteractor");
                                   
                System.Type visualType = GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual")
                                      ?? GetTypeFromAssemblies("UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual");

                if (rayType == null || visualType == null)
                {
                    Debug.LogError($"❌ Could not resolve XRRayInteractor ({rayType != null}) or XRInteractorLineVisual ({visualType != null}) types from assemblies. Skipping controller installation.");
                    continue;
                }

                // 1. Destroy components in dependency order: LineVisual -> Interactor -> LineRenderer
                var oldVisual = controller.GetComponent(visualType);
                if (oldVisual != null)
                {
                    Debug.Log($"   🔧 Destroying old component '{oldVisual.GetType().Name}' on '{controller.name}'...");
                    Object.DestroyImmediate(oldVisual);
                }

                var oldRay = controller.GetComponent(rayType);
                if (oldRay != null)
                {
                    Debug.Log($"   🔧 Destroying old component '{oldRay.GetType().Name}' on '{controller.name}'...");
                    Object.DestroyImmediate(oldRay);
                }

                var oldLr = controller.GetComponent<LineRenderer>();
                if (oldLr != null)
                {
                    Debug.Log($"   🔧 Destroying old component '{oldLr.GetType().Name}' on '{controller.name}'...");
                    Object.DestroyImmediate(oldLr);
                }

                // 2. Add the correct XRI components
                Debug.Log($"   🔧 Adding correct XRI components to '{controller.name}'...");
                
                // Add XRRayInteractor
                var rayInteractor = controller.gameObject.AddComponent(rayType);
                
                // Inspect and bind XRRayInteractor properties using reflection
                InspectAndFixRayInteractor(rayInteractor, rayType, isLeft);
                
                // Add LineRenderer
                var lineRenderer = controller.gameObject.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.01f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.useWorldSpace = true;
                
                // Add XRInteractorLineVisual
                var lineVisual = controller.gameObject.AddComponent(visualType);
                
                // Bind LineRenderer to LineVisual
                var lrProp = visualType.GetProperty("lineRenderer", BindingFlags.Public | BindingFlags.Instance);
                if (lrProp != null)
                {
                    lrProp.SetValue(lineVisual, lineRenderer);
                    Debug.Log("   ✅ Bound LineRenderer to XRInteractorLineVisual property.");
                }
                else
                {
                    SerializedObject soVisual = new SerializedObject(lineVisual);
                    var serializedLr = soVisual.FindProperty("m_LineRenderer");
                    if (serializedLr != null)
                    {
                        serializedLr.objectReferenceValue = lineRenderer;
                        soVisual.ApplyModifiedProperties();
                        Debug.Log("   ✅ Bound LineRenderer to XRInteractorLineVisual serialized field.");
                    }
                }
                
                Debug.Log($"   ✅ Re-initialization of '{controller.name}' complete.");
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

    private static System.Type GetTypeFromAssemblies(string typeName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

    private static void InspectAndFixRayInteractor(Component rayInteractor, System.Type rayType, bool isLeft)
    {
        string mapPrefix = isLeft ? "XRI Left Interaction" : "XRI Right Interaction";
        Debug.Log($"   🔧 Binding XRRayInteractor UI Press for '{rayInteractor.name}' using '{mapPrefix}/UI Press'...");
        
        string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
        if (guids.Length == 0) return;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        
        UnityEngine.InputSystem.InputActionReference uiPressRef = null;
        foreach (var sub in subAssets)
        {
            if (sub is UnityEngine.InputSystem.InputActionReference reference)
            {
                if (reference.name == $"{mapPrefix}/UI Press")
                {
                    uiPressRef = reference;
                    break;
                }
            }
        }
        
        if (uiPressRef == null)
        {
            Debug.LogError($"   ❌ Could not find UI Press action reference '{mapPrefix}/UI Press'!");
            return;
        }
        
        // Set the UI Press Action serialized property
        SerializedObject so = new SerializedObject(rayInteractor);
        string[] uiPressPropNames = { "m_UIPressAction", "m_UiPressAction", "uiPressAction", "m_UIPressInput", "m_UiPressInput" };
        bool successfullyBound = false;
        
        foreach (var propName in uiPressPropNames)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
            {
                var useRefProp = prop.FindPropertyRelative("m_UseReference");
                var refProp = prop.FindPropertyRelative("m_Reference");
                
                if (useRefProp != null && refProp != null)
                {
                    useRefProp.boolValue = true;
                    refProp.objectReferenceValue = uiPressRef;
                    so.ApplyModifiedProperties();
                    Debug.Log($"      ✅ Bound '{uiPressRef.name}' to RayInteractor field '{propName}'.");
                    successfullyBound = true;
                    break;
                }
            }
        }
        
        if (!successfullyBound)
        {
            Debug.LogWarning("   ⚠️ Could not find a serialized field to bind UI Press Action on XRRayInteractor.");
        }
    }

    private static void ApplyInputModulePreset(XRUIInputModule xrui)
    {
        Debug.Log("🔧 Verifying XRUIInputModule preset bindings...");
        
        string[] guids = AssetDatabase.FindAssets("XRI Default XR UI Input Module t:Preset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(path);
            if (preset != null)
            {
                if (preset.CanBeAppliedTo(xrui))
                {
                    preset.ApplyTo(xrui);
                    Debug.Log("✅ Applied 'XRI Default XR UI Input Module' preset to EventSystem's XRUIInputModule.");
                }
                else
                {
                    Debug.LogWarning("⚠️ Found UI input module preset, but it cannot be applied to this component.");
                }
            }
        }
        else
        {
            Debug.LogError("❌ Could not find 'XRI Default XR UI Input Module' preset in the project!");
        }
    }

    private static void InspectInputModule(XRUIInputModule xrui)
    {
        Debug.Log("🔧 Inspecting XRUIInputModule properties via Reflection...");
        
        PropertyInfo[] properties = xrui.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in properties)
        {
            if (p.PropertyType.Name.Contains("InputAction") || p.Name.ToLower().Contains("action"))
            {
                try
                {
                    object val = p.GetValue(xrui);
                    string details = val != null ? val.ToString() : "null";
                    
                    if (val != null && val.GetType().Name == "InputActionProperty")
                    {
                        var refProp = val.GetType().GetProperty("reference");
                        if (refProp != null)
                        {
                            object refVal = refProp.GetValue(val);
                            details = refVal != null ? refVal.ToString() : "EMPTY REFERENCE";
                        }
                    }
                    
                    Debug.Log($"   [XRUIInputModule] {p.Name} = {details}");
                }
                catch {}
            }
        }
    }

    private static void FixControllerBindings(ActionBasedController controller, bool isLeft)
    {
        string trackPrefix = isLeft ? "XRI Left" : "XRI Right";
        string interactPrefix = isLeft ? "XRI Left Interaction" : "XRI Right Interaction";
        Debug.Log($"🔧 Binding actions for {controller.name} (Tracking: '{trackPrefix}', Interaction: '{interactPrefix}')...");
        
        string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
        if (guids.Length == 0) return;
        
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        
        UnityEngine.InputSystem.InputActionReference posRef = null;
        UnityEngine.InputSystem.InputActionReference rotRef = null;
        UnityEngine.InputSystem.InputActionReference selectRef = null;
        UnityEngine.InputSystem.InputActionReference uiPressRef = null;
        UnityEngine.InputSystem.InputActionReference activateRef = null;
        
        foreach (var sub in subAssets)
        {
            if (sub is UnityEngine.InputSystem.InputActionReference reference)
            {
                string refName = reference.name;
                if (refName == $"{trackPrefix}/Position") posRef = reference;
                else if (refName == $"{trackPrefix}/Rotation") rotRef = reference;
                else if (refName == $"{interactPrefix}/Select") selectRef = reference;
                else if (refName == $"{interactPrefix}/UI Press") uiPressRef = reference;
                else if (refName == $"{interactPrefix}/Activate") activateRef = reference;
            }
        }
        
        SerializedObject so = new SerializedObject(controller);
        
        if (posRef != null)
        {
            so.FindProperty("m_PositionAction.m_UseReference").boolValue = true;
            so.FindProperty("m_PositionAction.m_Reference").objectReferenceValue = posRef;
        }
        if (rotRef != null)
        {
            so.FindProperty("m_RotationAction.m_UseReference").boolValue = true;
            so.FindProperty("m_RotationAction.m_Reference").objectReferenceValue = rotRef;
        }
        if (selectRef != null)
        {
            so.FindProperty("m_SelectAction.m_UseReference").boolValue = true;
            so.FindProperty("m_SelectAction.m_Reference").objectReferenceValue = selectRef;
        }
        if (uiPressRef != null)
        {
            so.FindProperty("m_UiPressAction.m_UseReference").boolValue = true;
            so.FindProperty("m_UiPressAction.m_Reference").objectReferenceValue = uiPressRef;
        }
        if (activateRef != null)
        {
            so.FindProperty("m_ActivateAction.m_UseReference").boolValue = true;
            so.FindProperty("m_ActivateAction.m_Reference").objectReferenceValue = activateRef;
        }
        
        so.ApplyModifiedProperties();
    }

    private static void PrintControllerActions(ActionBasedController controller)
    {
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
