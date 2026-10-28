using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Qixr.Companion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class QuantumCompanionPlayModeChecks
{
    private const string Pending="QixrCompanionOfflinePlayChecks";
    static QuantumCompanionPlayModeChecks()
    {
        EditorApplication.playModeStateChanged+=Changed;
        EditorApplication.update+=Watchdog;
    }
    private static void Watchdog()
    {
        if(SessionState.GetBool(Pending,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Pending+"Deadline",float.MaxValue))
        { Debug.LogError("Offline Play Mode checks timed out."); Finish(false); }
    }
    public static void Run()
    {
        if(!Application.isBatchMode) throw new Exception("Run these isolated checks in batch mode; they replace the open scene.");
        if(File.Exists(Path.Combine(Application.streamingAssetsPath,QuantumCompanionConfiguration.FileName)) ||
            File.Exists(QuantumCompanionConfiguration.OverridePath))
            throw new Exception("Offline Play Mode checks require no embedded/device pairing configuration.");
        QuantumCompanionClient.SuppressBootstrap=true;
        var xr=UnityEngine.XR.Management.XRGeneralSettings.Instance;
        if(xr!=null) { SessionState.SetBool(Pending+"XR",xr.InitManagerOnStart); xr.InitManagerOnStart=false; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true);
        SessionState.SetFloat(Pending+"Deadline",(float)EditorApplication.timeSinceStartup+90f);
        EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
            new GameObject("Offline companion test driver").AddComponent<QuantumCompanionPlayProbe>();
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending,false);
            var xr=UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if(xr!=null) xr.InitManagerOnStart=SessionState.GetBool(Pending+"XR",true);
            EditorApplication.Exit(SessionState.GetInt(Pending+"Exit",1));
        }
    }
    public static void Finish(bool passed)
    {
        SessionState.SetInt(Pending+"Exit",passed?0:1);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/companion-playmode-checks.json","{\"passed\":"+(passed?"true":"false")+",\"liveNetwork\":false}");
        EditorApplication.isPlaying=false;
    }
}

public sealed class QuantumCompanionPlayProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        // All remote I/O is injected before the client Start callback.
        var go=new GameObject("Mock-only companion");
        var client=go.AddComponent<QuantumCompanionClient>();
        var mock=new DelayedTransport(); client.Transport=mock;
        int transcripts=0;
        client.TranscriptReceived+=_=>transcripts++;
        yield return null; yield return null;
        client.SendMessage("OnApplicationFocus",true);
        if(!Require(go.GetComponent<QuantumCompanionPresenter>()!=null,"Unpaired presenter remains visible")) yield break;
        var transcribe=typeof(QuantumCompanionClient).GetMethod("Transcribe",BindingFlags.NonPublic|BindingFlags.Instance);
        var generation=typeof(QuantumCompanionClient).GetField("generation",BindingFlags.NonPublic|BindingFlags.Instance);
        client.StartCoroutine((IEnumerator)transcribe.Invoke(client,new object[]{new byte[0],(int)generation.GetValue(client),new int[]{0}}));
        yield return null;
        client.SetTargets(1);
        for(int i=0;i<5;i++) yield return null;
        if(!Require(transcripts==0,"Late ASR after selection change rejected")) yield break;
        client.StartCoroutine((IEnumerator)transcribe.Invoke(client,new object[]{new byte[0],(int)generation.GetValue(client),new int[]{1}}));
        for(int i=0;i<5;i++) yield return null;
        if(!Require(transcripts==1 && mock.QuestionPosts==0,"ASR remains editable until explicit Ask")) yield break;
        client.Config.authenticationMode="password";
        client.Config.serverUrl="https://fixture.invalid"; client.Config.roomId="fixture";
        client.Config.certificateSha256=new string('a',64); client.Config.participantId="fixture-headset";
        typeof(QuantumCompanionClient).GetProperty("IsPaired").SetValue(client,true);
        client.SignInWithPassword("wrong-fixture-password");
        for(int i=0;i<6;i++) yield return null;
        if(!Require(!client.IsConnected && client.Status.Contains("Incorrect password"),"Wrong password stays disconnected")) yield break;
        mock.LoginStatus=200;
        client.SignInWithPassword("fixture-password");
        for(int i=0;i<6;i++) yield return null;
        if(!Require(client.IsConnected && mock.PasswordPosts==2 && !mock.SentRoleToken,"Password sign-in uses only a password and conversation response")) yield break;
        var presenter=go.GetComponent<QuantumCompanionPresenter>();
        var input=(UnityEngine.UI.InputField)typeof(QuantumCompanionPresenter).GetField("serverPassword",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(presenter);
        if(!Require(input.contentType==UnityEngine.UI.InputField.ContentType.Password && input.text=="","Password input is masked and not persisted into UI")) yield break;
        input.text="fixture-unsaved-draft";
        client.SendMessage("OnApplicationPause",true); client.SendMessage("OnApplicationPause",false);
        for(int i=0;i<3;i++) yield return null;
        if(!Require(!client.IsConnected && mock.PasswordPosts==2 && input.text=="","Resume clears password and requires re-entry, without automatic sign-in")) yield break;
        client.SignInWithPassword("fixture-password");
        yield return null;
        client.Interrupt();
        for(int i=0;i<6;i++) yield return null;
        if(!Require(!client.IsConnected && !client.IsSigningIn,"Late login after cancellation cannot reconnect")) yield break;
        int aborted=mock.Aborts;
        client.enabled=false;
        if(!Require(mock.Aborts>aborted && !client.IsRecording,"Disable aborts outstanding I/O and microphone")) yield break;
        Destroy(go);
        Debug.Log("QIXR OFFLINE PLAY MODE CHECKS PASSED (mock transport only)");
        QuantumCompanionPlayModeChecks.Finish(true);
    }
    private static bool Require(bool condition,string name)
    {
        if(condition) return true;
        Debug.LogError("Offline Play Mode check failed: "+name);
        QuantumCompanionPlayModeChecks.Finish(false); return false;
    }
    private sealed class DelayedTransport : IQuantumCompanionTransport
    {
        public int Aborts,QuestionPosts;
        public int PasswordPosts;
        public long LoginStatus=401;
        public bool SentRoleToken;
        public IEnumerator Send(string url,string method,byte[] body,string type,string token,string pin,Action<CompanionHttpResult> done)
        {
            if(url.EndsWith("/questions")) QuestionPosts++;
            yield return null; yield return null;
            if(url.EndsWith("/password-conversations"))
            {
                PasswordPosts++; SentRoleToken|=!string.IsNullOrEmpty(token);
                done(new CompanionHttpResult {Status=LoginStatus,Data=Encoding.UTF8.GetBytes("{\"conversationId\":\"fixture-conversation\",\"conversationToken\":\"fixture-session\",\"authorityEpoch\":1}")});
                yield break;
            }
            if(url.Contains("/messages?"))
            { done(new CompanionHttpResult {Status=200,Data=Encoding.UTF8.GetBytes("{\"messages\":[],\"cursor\":0}")}); yield break; }
            done(new CompanionHttpResult { Status=200,Data=Encoding.UTF8.GetBytes("{\"text\":\"What are the probabilities?\",\"language\":\"en\"}") });
        }
        public void AbortAll() { Aborts++; }
    }
}
