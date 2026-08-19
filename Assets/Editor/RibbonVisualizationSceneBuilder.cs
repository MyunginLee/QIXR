#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Produces a deployable ribbon-only counterpart of the main XR scene without
/// changing the legacy gravity-trail scene.
/// </summary>
[InitializeOnLoad]
public static class RibbonVisualizationSceneBuilder
{
    private const string TemplateScenePath = "Assets/Scenes/main.unity";
    private const string RibbonScenePath = "Assets/Scenes/RibbonVisualization.unity";

    static RibbonVisualizationSceneBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("QIXR/Visualization/Create or Refresh Ribbon-Only Scene")]
    public static void CreateOrRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QIXR] Stop Play Mode before creating the ribbon visualization scene.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RibbonScenePath) == null &&
            !AssetDatabase.CopyAsset(TemplateScenePath, RibbonScenePath))
        {
            throw new InvalidOperationException("Could not copy the main XR scene for ribbon visualization.");
        }

        Scene ribbonScene = EditorSceneManager.OpenScene(RibbonScenePath, OpenSceneMode.Additive);
        try
        {
            Entanglement[] visuals = ribbonScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Entanglement>(true))
                .ToArray();
            if (visuals.Length == 0)
            {
                throw new InvalidOperationException("Ribbon scene has no Entanglement visual controller.");
            }

            foreach (Entanglement visual in visuals)
            {
                var serializedVisual = new SerializedObject(visual);
                SerializedProperty mode = serializedVisual.FindProperty("visualizationMode");
                if (mode == null)
                {
                    throw new InvalidOperationException("Entanglement visualization mode was not found.");
                }
                mode.enumValueIndex = 1; // EntanglementVisualizationMode.BraidedRibbons
                SerializedProperty ribbonOnly = serializedVisual.FindProperty("ribbonOnlyScene");
                if (ribbonOnly == null)
                {
                    throw new InvalidOperationException("Entanglement ribbon-only scene setting was not found.");
                }
                ribbonOnly.boolValue = true;
                serializedVisual.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(ribbonScene);
            if (!EditorSceneManager.SaveScene(ribbonScene))
            {
                throw new InvalidOperationException("Could not save the ribbon visualization scene.");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(ribbonScene, true);
        }

        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QIXR] Created RibbonVisualization.unity. It defaults to braided ribbons; main.unity remains legacy gravity trails.");
    }

    private static void CreateIfMissing()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(RibbonScenePath) == null)
        {
            CreateOrRefresh();
        }
    }

    private static void AddToBuildSettings()
    {
        if (EditorBuildSettings.scenes.Any(scene => scene.path == RibbonScenePath))
        {
            return;
        }

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes
            .Concat(new[] { new EditorBuildSettingsScene(RibbonScenePath, true) })
            .ToArray();
        EditorBuildSettings.scenes = scenes;
    }
}
#endif
