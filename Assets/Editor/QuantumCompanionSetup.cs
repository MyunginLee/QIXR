using System.IO;
using UnityEditor;
using UnityEngine;

public static class QuantumCompanionSetup
{
    [MenuItem("QIXR/Companion/Install pairing configuration")]
    public static void InstallConfiguration()
    {
        string file=EditorUtility.OpenFilePanel("Select host-config.json for the authority, client-config.json for other headsets", "", "json");
        if(string.IsNullOrEmpty(file)) return;
        var config=JsonUtility.FromJson<Qixr.Companion.CompanionConfig>(File.ReadAllText(file));
        if(config==null || string.IsNullOrEmpty(config.roomId) || string.IsNullOrEmpty(config.joinToken))
        { Debug.LogError("Invalid QIXR pairing configuration."); return; }
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.Copy(file,Path.Combine(Application.streamingAssetsPath,"qixr-companion.json"),true);
        AssetDatabase.Refresh();
        Debug.Log("Companion pairing installed. The runtime will add the panel when the scene starts.");
    }
}
