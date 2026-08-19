#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates a self-contained ribbon scene with local procedural sonification.</summary>
[InitializeOnLoad]
public static class MusicalRibbonSceneBuilder
{
    private const string TemplateScenePath = "Assets/Scenes/RibbonVisualization.unity";
    private const string MusicalScenePath = "Assets/Scenes/MusicalRibbonVisualization.unity";

    static MusicalRibbonSceneBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("QIXR/Visualization/Create or Refresh Musical Ribbon Scene")]
    public static void CreateOrRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QIXR] Stop Play Mode before creating the musical ribbon scene.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MusicalScenePath) == null &&
            !AssetDatabase.CopyAsset(TemplateScenePath, MusicalScenePath))
        {
            throw new InvalidOperationException("Could not copy the ribbon scene for sonification.");
        }

        Scene scene = EditorSceneManager.OpenScene(MusicalScenePath, OpenSceneMode.Additive);
        try
        {
            Entanglement[] visuals = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Entanglement>(true))
                .ToArray();
            if (visuals.Length == 0)
            {
                throw new InvalidOperationException("Musical ribbon scene has no Entanglement controller.");
            }

            foreach (Entanglement visual in visuals)
            {
                RemoveComponent(visual.gameObject, "EntanglementSonification");
                RemoveComponent(visual.gameObject, "QuantumChamberComposer");
                Type composerType = Type.GetType("QuantumArpeggioComposer, Assembly-CSharp");
                if (composerType == null)
                {
                    throw new InvalidOperationException("QuantumArpeggioComposer has not compiled yet.");
                }
                if (visual.GetComponent(composerType) == null)
                {
                    visual.gameObject.AddComponent(composerType);
                }

                // The original scene's grab/untangle one-shots are intentionally muted;
                // the sonifier instead responds to E_N and three-party correlation.
                AudioSource legacyAudio = visual.GetComponent<AudioSource>();
                if (legacyAudio != null)
                {
                    legacyAudio.enabled = false;
                }
            }

            QubitAudio[] legacyFmVoices = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<QubitAudio>(true))
                .ToArray();
            foreach (QubitAudio legacyFmVoice in legacyFmVoices)
            {
                legacyFmVoice.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save the musical ribbon scene.");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QIXR] Created MusicalRibbonVisualization.unity with local arpeggio sonification; legacy FM voices are disabled.");
    }

    private static void CreateIfMissing()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(MusicalScenePath) == null)
        {
            CreateOrRefresh();
        }
    }

    private static void RemoveComponent(GameObject target, string typeName)
    {
        Component[] components = target.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == typeName)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }

    private static void AddToBuildSettings()
    {
        if (EditorBuildSettings.scenes.Any(scene => scene.path == MusicalScenePath))
        {
            return;
        }

        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Concat(new[] { new EditorBuildSettingsScene(MusicalScenePath, true) })
            .ToArray();
    }
}
#endif
