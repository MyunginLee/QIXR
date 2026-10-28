using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Qixr.Companion;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

public static class QuantumCompanionQuestBuild
{
    private const string ScenePath="Assets/Scenes/MusicalRibbonVisualization.unity";
    [MenuItem("QIXR/Companion/Validate Quest Setup")]
    public static void Validate() { ValidateSetup(File.Exists(Path.Combine(Application.streamingAssetsPath,QuantumCompanionConfiguration.FileName))); }

    private static void ValidateSetup(bool paired)
    {
        string path=Path.Combine(Application.streamingAssetsPath,QuantumCompanionConfiguration.FileName);
        CompanionConfig pairing=null;
        if(paired)
        {
            if(!File.Exists(path) || !QuantumCompanionConfiguration.TryParse(File.ReadAllText(path),out pairing,out _))
                throw new BuildFailedException("Install a valid role-specific pairing configuration first.");
        }
        else if(File.Exists(path)) throw new BuildFailedException("Unpaired build requires no embedded pairing file. Use Build paired Quest APK, or move the private config outside Assets first.");
        if(Resources.Load<Font>("Quantum/NanumGothic-Regular")==null) throw new BuildFailedException("Companion font missing.");
        var xr=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var quest=xr==null?null:xr.GetFeature<UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature>();
        if(quest==null || !quest.enabled) throw new BuildFailedException("Android Meta Quest OpenXR support must be enabled.");
        if(quest.ForceRemoveInternetPermission)
            throw new BuildFailedException("Disable Meta Quest Support > Force Remove Internet Permission; the companion requires HTTPS.");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var roots=scene.GetRootGameObjects();
            foreach(var item in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)>0)
                    Debug.LogError("QIXR missing script: "+HierarchyPath(item)+" | prefab "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject));
            foreach(var controller in roots.SelectMany(r=>r.GetComponentsInChildren<ArtsOfEntanglement.Colocation.ColocationUIController>(true)))
            {
                var serialized=new SerializedObject(controller);
                serialized.FindProperty("autoStartOnEnable").boolValue=paired && pairing.simulationMode=="fusion";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if(roots.SelectMany(r=>r.GetComponentsInChildren<XROrigin>(true)).Count()!=1)
                throw new BuildFailedException("Expected exactly one XR Origin in the musical scene.");
            int uiInteractors=0;
            foreach(var component in roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if(component==null) throw new BuildFailedException("Missing script in the musical scene.");
                if(component.GetType().Name!="NearFarInteractor" && component.GetType().Name!="XRRayInteractor") continue;
                var serialized=new SerializedObject(component);
                var enable=serialized.FindProperty("m_EnableUIInteraction");
                if(enable!=null) { enable.boolValue=true; serialized.ApplyModifiedPropertiesWithoutUndo(); uiInteractors++; }
            }
            if(uiInteractors==0) throw new BuildFailedException("No controller UI ray interactor found.");
            var qubits=roots.SelectMany(r=>r.GetComponentsInChildren<Qubit>()).OrderBy(q=>q.GetIndex()).ToArray();
            if(qubits.Length!=3 || !qubits.Select(q=>q.GetIndex()).SequenceEqual(new[]{0,1,2}))
                throw new BuildFailedException("Three distinct qubit IDs 0,1,2 are required.");
            var systems=roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if(systems.Length>1) throw new BuildFailedException("Multiple EventSystems in the musical scene.");
            var system=systems.Length==1?systems[0]:new GameObject("XR EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            if(systems.Length==0) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(system.gameObject,scene);
            foreach(var module in system.GetComponents<BaseInputModule>()) if(!(module is XRUIInputModule)) module.enabled=false;
            (system.GetComponent<XRUIInputModule>() ?? system.gameObject.AddComponent<XRUIInputModule>()).enabled=true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if(previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            if(opened) EditorSceneManager.CloseScene(scene,true);
        }
        Debug.Log("QIXR Quest setup validated. Device controller/keyboard/TLS checks remain pending.");
    }
    private static string HierarchyPath(Transform item) => item.parent==null?item.name:HierarchyPath(item.parent)+"/"+item.name;
    [MenuItem("QIXR/Companion/Build unpaired Quest APK")]
    public static void BuildUnpaired() { Build(false); }
    [MenuItem("QIXR/Companion/Build paired Quest APK")]
    public static void BuildPaired() { Build(true); }
    [MenuItem("QIXR/Companion/Build password Quest APK")]
    public static void BuildPassword()
    {
        string path=Path.Combine(Application.streamingAssetsPath,QuantumCompanionConfiguration.FileName);
        if(!File.Exists(path) || !QuantumCompanionConfiguration.TryParse(File.ReadAllText(path),out var config,out _) ||
            config.authenticationMode!="password" || !string.IsNullOrEmpty(config.authorityToken) || !string.IsNullOrEmpty(config.joinToken))
            throw new BuildFailedException("Install the public quest-password-profile.json first. Password APKs must not embed role tokens.");
        Build(true,"password");
    }
    private static void Build(bool paired,string variant=null)
    {
        QuantumCompanionClient.SuppressBootstrap=true;
        ValidateSetup(paired);
        QuantumCompanionOfflineChecks.Run();
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included=graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach(string shaderName in new[]{"Universal Render Pipeline/Unlit","Sprites/Default"})
        {
            Shader shader=Shader.Find(shaderName);
            if(shader==null) throw new BuildFailedException("Required companion shader missing: "+shaderName);
            bool exists=false;
            for(int i=0;i<included.arraySize;i++) exists|=included.GetArrayElementAtIndex(i).objectReferenceValue==shader;
            if(!exists) { int index=included.arraySize++; included.GetArrayElementAtIndex(index).objectReferenceValue=shader; }
        }
        graphics.ApplyModifiedPropertiesWithoutUndo();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission=true;
        EditorUserBuildSettings.buildAppBundle=false;
        string folder="Builds/Quest";
        Directory.CreateDirectory(folder);
        string apk=folder+"/qixr-companion-"+(variant ?? (paired?"paired":"unpaired"))+".apk";
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName=apk,
            target=BuildTarget.Android, options=BuildOptions.Development | BuildOptions.AllowDebugging });
        string hash="";
        if(File.Exists(apk)) using(var sha=SHA256.Create()) using(var stream=File.OpenRead(apk))
            hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        File.WriteAllText(folder+"/build-report.json",JsonUtility.ToJson(new QuestBuildReport {
            utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,result=report.summary.result.ToString(),
            errors=(int)report.summary.totalErrors,apk=apk,sha256=hash,applicationId=PlayerSettings.applicationIdentifier,
            version=PlayerSettings.bundleVersion,paired=paired,seconds=report.summary.totalTime.TotalSeconds },true));
        if(report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("Quest APK build failed; see build-report.json and Unity log.");
        Debug.Log("QIXR APK BUILT: "+apk+" SHA256 "+hash);
    }
    [Serializable] private class QuestBuildReport
    {
        public string utc,unity,result,apk,sha256,applicationId,version;
        public int errors; public bool paired; public double seconds;
    }
}
