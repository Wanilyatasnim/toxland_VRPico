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

        // Print all sub-assets of XRI Default Input Actions to see their exact names
        string[] actionAssetGuids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
        if (actionAssetGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(actionAssetGuids[0]);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"🔧 DUMPING sub-assets for XRI Default Input Actions at path '{path}':");
            foreach (var sub in subAssets)
            {
                if (sub is UnityEngine.InputSystem.InputActionReference reference)
                {
                    Debug.Log($"   [ACTION REF] Name: '{reference.name}', Action Name: '{reference.action.name}'");
                }
            }
        }
        else
        {
            Debug.LogError("❌ XRI Default Input Actions asset not found!");
        }

        Debug.Log("========== SCENE UI FIXES COMPLETE ==========");
    }
}
