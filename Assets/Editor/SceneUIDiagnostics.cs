using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class SceneUIDiagnostics : EditorWindow
{
    [MenuItem("Tools/Diagnose Scene UI")]
    public static void DiagnoseUI()
    {
        Debug.Log("========== STARTING SCENE UI DIAGNOSTICS ==========");

        // 1. Check EventSystem
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length == 0)
        {
            Debug.LogError("❌ [DIAGNOSTIC] No EventSystem found in the scene! UI interaction will not work.");
        }
        else
        {
            foreach (var es in eventSystems)
            {
                Debug.Log($"ℹ️ [DIAGNOSTIC] Found EventSystem on GameObject: '{es.name}'", es);
                
                // Check for input modules
                var xruiModule = es.GetComponent<XRUIInputModule>();
                var standaloneModule = es.GetComponent<StandaloneInputModule>();
                
                if (xruiModule != null)
                {
                    Debug.Log($"  ✅ Found XRUIInputModule on '{es.name}' (Recommended for XRI).", es);
                }
                else if (standaloneModule != null)
                {
                    Debug.LogWarning($"  ⚠️ Found StandaloneInputModule on '{es.name}'. " +
                                     "For XR interaction (VR lasers) to work, you should replace it with XRUIInputModule.", es);
                }
                else
                {
                    Debug.LogError($"  ❌ No valid Input Module found on '{es.name}'! (Expected XRUIInputModule)", es);
                }
            }
        }

        // 2. Check Canvases
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (canvases.Length == 0)
        {
            Debug.LogWarning("⚠️ [DIAGNOSTIC] No Canvas components found in the scene.");
        }
        else
        {
            foreach (var canvas in canvases)
            {
                Debug.Log($"ℹ️ [DIAGNOSTIC] Found Canvas on GameObject: '{canvas.name}' (RenderMode: {canvas.renderMode})", canvas);
                
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    // For WorldSpace UI, XR lasers need TrackedDeviceGraphicRaycaster (or PXR_ScreenRaycaster for PICO, etc.)
                    var trackedRaycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
                    var standardRaycaster = canvas.GetComponent<GraphicRaycaster>();
                    
                    // Check if there are missing scripts (broken Oculus/Pico components)
                    var components = canvas.GetComponents<Component>();
                    bool hasMissingScript = false;
                    foreach (var c in components)
                    {
                        if (c == null)
                        {
                            hasMissingScript = true;
                        }
                    }
                    
                    if (hasMissingScript)
                    {
                        Debug.LogError($"  ❌ GameObject '{canvas.name}' has a Missing/Broken script component! This could be a leftover Oculus raycaster.", canvas);
                    }

                    if (trackedRaycaster != null)
                    {
                        Debug.Log($"  ✅ Found TrackedDeviceGraphicRaycaster on '{canvas.name}' (Correct for VR raycasts).", canvas);
                    }
                    else if (standardRaycaster != null)
                    {
                        Debug.LogWarning($"  ⚠️ '{canvas.name}' is World Space but uses standard GraphicRaycaster instead of TrackedDeviceGraphicRaycaster. VR lasers will NOT click it.", canvas);
                    }
                    else
                    {
                        Debug.LogError($"  ❌ '{canvas.name}' has NO Raycaster component. It cannot receive clicks.", canvas);
                    }
                }
            }
        }

        // 3. Check XR Ray Interactors
        var rayInteractors = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
        if (rayInteractors.Length == 0)
        {
            Debug.LogWarning("⚠️ [DIAGNOSTIC] No XRRayInteractor found in the scene! Are the controller lasers configured?");
        }
        else
        {
            foreach (var ri in rayInteractors)
            {
                Debug.Log($"✅ [DIAGNOSTIC] Found XRRayInteractor on GameObject: '{ri.name}' (Active: {ri.gameObject.activeInHierarchy})", ri);
                
                var lineVisual = ri.GetComponent<XRInteractorLineVisual>();
                if (lineVisual == null)
                {
                    Debug.LogWarning($"  ⚠️ '{ri.name}' has no XRInteractorLineVisual, so the laser pointer will be invisible in the scene.", ri);
                }
                else
                {
                    Debug.Log($"  ✅ '{ri.name}' has XRInteractorLineVisual (laser is visible).", ri);
                }
            }
        }

        Debug.Log("========== SCENE UI DIAGNOSTICS COMPLETE ==========");
    }
}
