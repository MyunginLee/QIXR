using System.IO;
using UnityEditor;
using UnityEngine;

public static class QuantumCompanionSetup
{
    [MenuItem("QIXR/Companion/Install pairing configuration")]
    public static void InstallConfiguration()
    {
        string file=EditorUtility.OpenFilePanel("Select a role pairing file or quest-password-profile.json", "", "json");
        if(string.IsNullOrEmpty(file)) return;
        if(!Qixr.Companion.QuantumCompanionConfiguration.TryParse(File.ReadAllText(file),out var config,out var error))
        { Debug.LogError(error); return; }
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.Copy(file,Path.Combine(Application.streamingAssetsPath,"qixr-companion.json"),true);
        AssetDatabase.Refresh();
        Debug.Log("Companion pairing installed. The runtime will add the panel when the scene starts.");
    }
}
