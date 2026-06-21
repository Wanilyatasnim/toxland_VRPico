using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

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
            Debug.Log($"🔧 Processing Canvas: '{canvas.name}'");
            
            // Remove missing scripts (Oculus components, etc.)
            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);
            if (removedCount > 0)
            {
                Debug.Log($"✅ Removed {removedCount} missing script(s) from '{canvas.name}'.");
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
                    Debug.Log($"✅ Added TrackedDeviceGraphicRaycaster to '{canvas.name}'.");
                }
                
                // Ensure event camera is assigned to main camera
                if (canvas.worldCamera == null && Camera.main != null)
                {
                    canvas.worldCamera = Camera.main;
                    Debug.Log($"✅ Assigned Main Camera as Event Camera for '{canvas.name}'.");
                }
            }
        }

        // 3. Scan & Output XR Origin hierarchy to help diagnose controllers
        GameObject origin = GameObject.Find("XR Origin (VR)");
        if (origin == null) origin = GameObject.Find("XR Origin");
        if (origin == null) origin = GameObject.Find("XROrigin");
        if (origin == null && Camera.main != null) origin = Camera.main.transform.root.gameObject;

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
