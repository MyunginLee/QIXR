#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QixrPhase3SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/main.unity";
    private const string SourcePrefabPath = "Assets/Prefab/Qubit 1.prefab";
    private const string ThirdPrefabPath = "Assets/Prefab/Qubit 3.prefab";
    private const string ShaderPath = "Assets/Environment/Orbit/Hydrogen-Floor.shader";

    [MenuItem("QIXR/Phase 3/Build and Validate Three-Qubit Scene")]
    public static void BuildAndValidate()
    {
        BuildThirdQubitPrefab();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureThirdQubit(scene);
        EnsureThirdShell(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        QixrThreeQubitEngineChecks.Run();
        ValidateOpenScene();
        Debug.Log("[QIXR Phase 3] Three-qubit XR scene build and validation passed.");
    }

    [MenuItem("QIXR/Validation/Validate Phase 3 Scene")]
    public static void ValidateOpenScene()
    {
        Qubit[] qubits = UnityEngine.Object.FindObjectsByType<Qubit>(FindObjectsSortMode.None);
        Array.Sort(qubits, (left, right) => left.GetIndex().CompareTo(right.GetIndex()));
        Assert(qubits.Length == 3, $"Expected 3 active qubits, found {qubits.Length}.");
        for (int id = 0; id < qubits.Length; id++)
        {
            Assert(qubits[id].GetIndex() == id, $"Expected Qubit ID {id}, found {qubits[id].GetIndex()}.");
            Assert(qubits[id].CompareTag("Qubit"), $"Q{id} is missing the Qubit tag.");
            Assert(qubits[id].DotTransform != null, $"Q{id} is missing its Bloch-vector dot reference.");
            Assert(qubits[id].GetComponent<LineRenderer>() != null, $"Q{id} is missing its LineRenderer.");
            Assert(qubits[id].GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null,
                $"Q{id} is missing XR grab interaction.");
            QubitInput input = qubits[id].GetComponent<QubitInput>();
            Assert(input != null, $"Q{id} is missing QubitInput.");
            var serializedInput = new SerializedObject(input);
            AssertReference(serializedInput, "inputActions", $"Q{id} input actions");
            AssertReference(serializedInput, "measureActions", $"Q{id} measurement actions");
            AssertReference(serializedInput, "qubit", $"Q{id} QubitInput.qubit");
            AssertReference(serializedInput, "innerSphere", $"Q{id} inner sphere");
            AssertReference(serializedInput, "interactableObject", $"Q{id} interactable reference");
            Assert(qubits[id].GetComponentInChildren<QubitAudio>(true) != null,
                $"Q{id} is missing its per-node audio component.");

            var serializedQubit = new SerializedObject(qubits[id]);
            AssertReference(serializedQubit, "audioClipH", $"Q{id} H audio");
            AssertReference(serializedQubit, "audioClipX", $"Q{id} X audio");
            AssertReference(serializedQubit, "audioClipZ", $"Q{id} Z audio");
            AssertReference(serializedQubit, "audioClipS", $"Q{id} S audio");
            Assert(GameObject.Find($"QubitShell {id + 1}") != null, $"QubitShell {id + 1} is missing.");
        }

        Assert(UnityEngine.Object.FindFirstObjectByType<Entanglement>() != null,
            "The central entanglement visual controller is missing.");
        Assert(UnityEngine.Object.FindObjectsByType<FloorElectrons>(FindObjectsSortMode.None).Length > 0,
            "No wavefunction floor/top controller was found.");

        GameObject thirdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPrefabPath);
        Assert(thirdPrefab != null, "Qubit 3 prefab was not created.");
        Assert(thirdPrefab.GetComponent<Qubit>()?.GetIndex() == 2, "Qubit 3 prefab does not have Qubit ID 2.");

        string shader = File.ReadAllText(ShaderPath);
        Assert(shader.Contains("MAX_QUBITS 8"), "Wave shader does not declare the expanded qubit capacity.");
        Assert(shader.Contains("_OrbitColor[MAX_QUBITS]"), "Wave shader colors are not per-qubit arrays.");
    }

    private static void BuildThirdQubitPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
        try
        {
            contents.name = "Qubit 3";
            Qubit qubit = contents.GetComponent<Qubit>();
            Assert(qubit != null, "Qubit 1 prefab has no Qubit component.");
            var serializedQubit = new SerializedObject(qubit);
            SerializedProperty id = serializedQubit.FindProperty("qubitId");
            Assert(id != null, "Qubit ID serialized field was not found.");
            id.intValue = 2;
            serializedQubit.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, ThirdPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void EnsureThirdQubit(Scene scene)
    {
        Qubit[] qubits = UnityEngine.Object.FindObjectsByType<Qubit>(FindObjectsSortMode.None);
        Qubit third = Array.Find(qubits, candidate => candidate.GetIndex() == 2);
        if (third == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Qubit 3";
            third = instance.GetComponent<Qubit>();
        }
        third.name = "Qubit 3";
        third.transform.SetPositionAndRotation(new Vector3(-0.65f, 0.62f, 4.69f), Quaternion.identity);
        third.transform.localScale = new Vector3(0.171f, 0.171f, 0.171f);
    }

    private static void EnsureThirdShell(Scene scene)
    {
        GameObject shell = GameObject.Find("QubitShell 3");
        if (shell == null)
        {
            GameObject source = GameObject.Find("QubitShell 1");
            Assert(source != null, "QubitShell 1 cannot be used as the shell template.");
            shell = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(shell, scene);
            shell.name = "QubitShell 3";
        }
        shell.tag = "Shell";
        shell.transform.position = new Vector3(-0.65f, 0.62f, 4.69f);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertReference(SerializedObject owner, string propertyName, string label)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        Assert(property != null && property.objectReferenceValue != null, $"Missing reference: {label}.");
    }
}
#endif
