using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Qixr.Companion
{
    [RequireComponent(typeof(QuantumContextExporter), typeof(QuantumEventJournal))]
    public sealed class QuantumCompanionClient : MonoBehaviour
    {
        public CompanionConfig Config { get; private set; }
        public string Status { get; private set; } = "Connecting to Quantum…";
        public string Caption { get; private set; } = "";
        public int[] Targets => targets.ToArray();
        public bool SharedPresentation { get; set; }
        public bool IsRecording => microphoneClip != null;
        public bool IsRemotePresentation { get; private set; }
        public event Action<CompanionAnswer> AnswerReceived;
        public event Action<AudioClip> SpeechReceived;
        public event Action Interrupted;
        public event Action<string> TranscriptReceived;
        private readonly List<int> targets = new List<int>();
        private QuantumContextExporter exporter;
        private QuantumEventJournal journal;
        private string conversationId, token;
        private string selectionId = Guid.NewGuid().ToString("N");
        private int generation, publishSerial, pendingCancels;
        private bool turnActive, reconnecting;
        private bool forcePublish;
        private AudioClip microphoneClip;
        private int[] recordingTargets;
        private float recordingStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<QuantumCompanionClient>() != null) return;
            new GameObject("QIXR Quantum Companion").AddComponent<QuantumCompanionClient>();
        }
        private void Awake()
        {
            exporter=GetComponent<QuantumContextExporter>(); journal=GetComponent<QuantumEventJournal>();
        }
        private IEnumerator Start()
        {
            string path=Application.streamingAssetsPath+"/qixr-companion.json";
            if (!path.Contains("://")) path="file://"+path;
            using (var request=UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { Status="Companion disabled: install qixr-companion.json (see server/README.md)."; enabled=false; yield break; }
                Config=Parse<CompanionConfig>(request.downloadHandler.text);
            }
            if (Config==null || string.IsNullOrEmpty(Config.roomId) || string.IsNullOrEmpty(Config.joinToken))
            { Status="Invalid companion configuration."; enabled=false; yield break; }
            if (!Config.serverUrl.StartsWith("https://",StringComparison.OrdinalIgnoreCase))
            { Status="Companion requires HTTPS."; enabled=false; yield break; }
            Config.serverUrl=Config.serverUrl.TrimEnd('/');
            if(Config.language!="ko" && Config.language!="en") Config.language="en";
            if (string.IsNullOrEmpty(Config.participantId))
            {
                Config.participantId=PlayerPrefs.GetString("QixrCompanionParticipant","");
                if(string.IsNullOrEmpty(Config.participantId))
                { Config.participantId=Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("QixrCompanionParticipant",Config.participantId); PlayerPrefs.Save(); }
            }
            gameObject.AddComponent<QuantumCompanionPresenter>();
            exporter.RoomId=Config.roomId;
            while (conversationId==null)
            {
                yield return Send("POST","/v1/rooms/"+Config.roomId+"/conversations",JsonUtility.ToJson(new JoinDto { participantId=Config.participantId }),Config.joinToken,
                    text => { var r=Parse<JoinResultDto>(text); if(r!=null) { conversationId=r.conversationId; token=r.conversationToken; } });
                if (conversationId==null) yield return new WaitForSecondsRealtime(2);
            }
            StartCoroutine(PublishLoop());
            StartCoroutine(SharedLoop());
            Status=Config.language=="ko" ? "큐빗을 잡거나 선택하고 질문해 주세요." : "Select or grab a qubit and ask me.";
        }
        private void Update()
        {
            if (microphoneClip != null && Time.realtimeSinceStartup-recordingStarted>=14.5f) EndTalk();
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                var input=selected!=null ? selected.GetComponent<UnityEngine.UI.InputField>() : null;
                if (input==null || !input.isFocused)
                {
                    if (Keyboard.current.tKey.wasPressedThisFrame) BeginTalk();
                    if (Keyboard.current.tKey.wasReleasedThisFrame) EndTalk();
                }
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Interrupt();
            }
#endif
        }
        public void SetTargets(params int[] ids)
        {
            if (ids.Length>2) return;
            var next=new List<int>();
            foreach(int id in ids) if(id>=0 && id<3 && !next.Contains(id)) next.Add(id);
            if (string.Join(",",next)==string.Join(",",targets)) return;
            Interrupt(); targets.Clear(); targets.AddRange(next); selectionId=Guid.NewGuid().ToString("N");
        }
        public void ToggleTarget(int id)
        {
            var next=new List<int>(targets);
            if (next.Contains(id)) next.Remove(id); else { if(next.Count==2) next.RemoveAt(0); next.Add(id); }
            SetTargets(next.ToArray());
        }
        public void Interrupt()
        {
            generation++;
            turnActive=false;
            Caption="";
            if(microphoneClip!=null) { Microphone.End(null); Destroy(microphoneClip); microphoneClip=null; }
            Interrupted?.Invoke();
            if(conversationId!=null) StartCoroutine(CancelRemote());
        }
        private IEnumerator CancelRemote()
        {
            pendingCancels++;
            try { yield return Send("POST",ConversationPath+"/cancel","{}",token); }
            finally { pendingCancels--; }
        }
        private string ConversationPath => "/v1/conversations/"+conversationId;
        public void AskText(string text)
        {
            if(conversationId==null || string.IsNullOrWhiteSpace(text)) return;
            Interrupt(); StartCoroutine(Ask(text,generation,Targets));
        }
        public void BeginTalk()
        {
            if(conversationId==null || microphoneClip!=null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            { UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone); Status="Allow microphone access, then hold Talk again."; return; }
#endif
            Interrupt();
            if(Microphone.devices.Length==0) { Status="No microphone. Text questions remain available."; return; }
            recordingTargets=Targets; recordingStarted=Time.realtimeSinceStartup;
            microphoneClip=Microphone.Start(null,false,15,16000); Status=Config.language=="ko"?"● 듣고 있어요…":"● Listening…";
        }
        public void EndTalk()
        {
            if(microphoneClip==null) return;
            int count=Microphone.GetPosition(null);
            if(count<=0) count=Mathf.Min(microphoneClip.samples,(int)((Time.realtimeSinceStartup-recordingStarted)*microphoneClip.frequency));
            Microphone.End(null);
            var raw=new float[count*microphoneClip.channels]; microphoneClip.GetData(raw,0);
            int output=Mathf.Min(15*16000,Mathf.FloorToInt(count*16000f/microphoneClip.frequency));
            var mono=new float[output];
            for(int i=0;i<output;i++)
            {
                int source=Mathf.Min(count-1,Mathf.FloorToInt(i*microphoneClip.frequency/16000f));
                for(int channel=0;channel<microphoneClip.channels;channel++) mono[i]+=raw[source*microphoneClip.channels+channel]/microphoneClip.channels;
            }
            Destroy(microphoneClip); microphoneClip=null;
            if(output<3200) { Status="Hold to speak a little longer."; return; }
            StartCoroutine(Transcribe(QuantumWav.Encode(mono,16000),generation,recordingTargets));
        }
        private IEnumerator Transcribe(byte[] wav,int turn,int[] lockedTargets)
        {
            Status=Config.language=="ko"?"말을 확인하고 있어요…":"Checking what I heard…";
            string transcript=null;
            yield return SendBytes("POST",ConversationPath+"/transcribe?language="+Config.language,wav,"audio/wav",token,
                bytes => { var r=Parse<TranscriptDto>(Encoding.UTF8.GetString(bytes)); transcript=r?.text; });
            if(turn!=generation) yield break;
            if(string.IsNullOrWhiteSpace(transcript)) { Status="No speech recognized. Try again or use text."; yield break; }
            // A transcript is editable input, not an instruction to the agent yet.
            TranscriptReceived?.Invoke(transcript);
            Status=Config.language=="ko"?"인식한 질문을 확인·수정한 뒤 질문 버튼을 눌러 주세요.":"Review or edit what I heard, then press Ask.";
        }
        private IEnumerator Ask(string text,int turn,int[] lockedTargets)
        {
            turnActive=true;
            try { yield return AskCore(text,turn,lockedTargets); }
            finally { if(turn==generation) turnActive=false; }
        }
        private IEnumerator AskCore(string text,int turn,int[] lockedTargets)
        {
            while(pendingCancels>0 && turn==generation) yield return null;
            if(!string.IsNullOrEmpty(Config.authorityToken) && QuantumContextExporter.IsAuthority)
            {
                int serial=publishSerial; forcePublish=true;
                float deadline=Time.realtimeSinceStartup+5;
                while(publishSerial<=serial && Time.realtimeSinceStartup<deadline && turn==generation) yield return null;
            }
            if(turn!=generation) yield break;
            ContextDto context=null;
            yield return Send("GET",ConversationPath+"/context",null,token,value=>context=Parse<ContextDto>(value));
            if(context?.snapshot==null || turn!=generation) yield break;
            var question=new CompanionQuestion { requestId=Guid.NewGuid().ToString("N"), selectionId=selectionId,
                referenceSnapshotId=context.snapshot.snapshotId, referenceEventId=context.lastEventId,
                targetQubitIds=lockedTargets, language=Config.language, text=text, visibility=SharedPresentation?"room":"private" };
            CompanionJob accepted=null;
            Status=Config.language=="ko"?"확인하고 있어요…":"Let me check…";
            yield return Send("POST",ConversationPath+"/questions",JsonUtility.ToJson(question),token,value=>accepted=Parse<CompanionJob>(value));
            if(accepted==null || turn!=generation) yield break;
            int audioIndex=0; bool displayed=false;
            while(turn==generation)
            {
                CompanionJob job=null;
                yield return Send("GET",ConversationPath+"/requests/"+question.requestId,null,token,value=>job=Parse<CompanionJob>(value));
                if(job==null) yield break;
                if(turn!=generation || job.state=="cancelled") yield break;
                if(job.result!=null && job.result.selectionId==selectionId && !displayed)
                {
                    IsRemotePresentation=false;
                    Caption=job.result.text; AnswerReceived?.Invoke(job.result); displayed=true;
                    Status=job.result.referenceCue?.label ?? "Answer ready.";
                }
                if(job.result!=null && job.result.status=="context_changed")
                { Interrupt(); Status="The live state changed. Ask again for a fresh explanation."; yield break; }
                while(audioIndex<job.audioCount && turn==generation)
                {
                    byte[] data=null;
                    yield return SendBytes("GET",ConversationPath+"/requests/"+question.requestId+"/audio/"+audioIndex,null,null,token,value=>data=value);
                    audioIndex++;
                    if(data!=null && turn==generation)
                    {
                        AudioClip clip=DecodeAudio(data);
                        if(clip!=null) SpeechReceived?.Invoke(clip);
                    }
                }
                if(job.state=="done" || job.state=="error")
                { Status=job.state=="error"?"This turn failed; please retry.":"Ready when you have another question."; yield break; }
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }
        private IEnumerator PublishLoop()
        {
            while(enabled)
            {
                if(string.IsNullOrEmpty(Config.authorityToken) || !QuantumContextExporter.IsAuthority || !QubitManager.IsInitialized)
                { yield return new WaitForSecondsRealtime(.2f); continue; }
                if(exporter.AuthorityEpoch==0 || journal.Overflowed)
                {
                    RoomStatusDto current=null;
                    yield return Send("GET","/v1/rooms/"+Config.roomId,null,Config.authorityToken,text=>current=Parse<RoomStatusDto>(text));
                    if(current==null) { yield return new WaitForSecondsRealtime(2); continue; }
                    EpochResultDto next=null;
                    yield return Send("POST","/v1/rooms/"+Config.roomId+"/epoch",JsonUtility.ToJson(new EpochDto {expectedEpoch=current.authorityEpoch}),Config.authorityToken,text=>next=Parse<EpochResultDto>(text));
                    if(next==null) { yield return new WaitForSecondsRealtime(2); continue; }
                    exporter.AuthorityEpoch=next.authorityEpoch; journal.ClearJournal();
                }
                bool success=true;
                var entry=journal.Peek();
                if(entry!=null)
                {
                    yield return Publish(entry.Before,result=>success=result);
                    if(success) yield return Publish(entry.After,result=>success=result);
                    if(success)
                    {
                        success=false;
                        yield return Send("POST","/v1/rooms/"+Config.roomId+"/events",JsonUtility.ToJson(entry.Event),Config.authorityToken,_=>success=true);
                    }
                    if(success) journal.Acknowledge();
                    else yield return new WaitForSecondsRealtime(1);
                    continue;
                }
                QuantumSnapshotDto snapshot=exporter.Capture();
                if(snapshot!=null)
                {
                    yield return Publish(snapshot,result=>success=result);
                    if(success) { publishSerial++; forcePublish=false; }
                }
                float until=Time.realtimeSinceStartup+.2f;
                while(Time.realtimeSinceStartup<until && !forcePublish) yield return null;
            }
        }
        private IEnumerator SharedLoop()
        {
            int cursor=0;
            while(enabled)
            {
                if(conversationId==null) { yield return new WaitForSecondsRealtime(1); continue; }
                SharedMessages batch=null;
                yield return Send("GET",ConversationPath+"/messages?after="+cursor,null,token,text=>batch=Parse<SharedMessages>(text));
                if(batch!=null)
                {
                    cursor=batch.cursor;
                    foreach(var message in batch.messages ?? new SharedMessage[0])
                    {
                        if(message.type=="authority_reset") { Interrupt(); continue; }
                        if(!SharedPresentation || turnActive || message.speakerConversationId==conversationId) continue;
                        if(message.type=="answer" && message.result!=null)
                        { IsRemotePresentation=true; Caption="[Shared] "+message.result.text; AnswerReceived?.Invoke(message.result); }
                        if(message.type=="audio_ready")
                        {
                            byte[] data=null; int turn=generation;
                            yield return SendBytes("GET",ConversationPath+"/shared/"+message.speakerConversationId+"/"+message.requestId+"/audio/"+message.audioIndex,null,null,token,value=>data=value);
                            if(data!=null && turn==generation && SharedPresentation && !turnActive)
                            { var clip=DecodeAudio(data); if(clip!=null) SpeechReceived?.Invoke(clip); }
                        }
                        if(message.type=="cancelled" || message.type=="context_changed") Interrupted?.Invoke();
                    }
                }
                yield return new WaitForSecondsRealtime(.3f);
            }
        }
        private IEnumerator Reconnect()
        {
            reconnecting=true; Interrupt(); conversationId=null; token=null;
            Status="Reconnecting to Quantum…";
            while(enabled && conversationId==null)
            {
                yield return Send("POST","/v1/rooms/"+Config.roomId+"/conversations",JsonUtility.ToJson(new JoinDto {participantId=Config.participantId}),Config.joinToken,
                    text=>{ var r=Parse<JoinResultDto>(text); if(r!=null) {conversationId=r.conversationId; token=r.conversationToken;} });
                if(conversationId==null) yield return new WaitForSecondsRealtime(2);
            }
            reconnecting=false;
            Status="Reconnected. Select a qubit and ask again.";
        }
        private IEnumerator Publish(QuantumSnapshotDto snapshot,Action<bool> done)
        {
            bool success=false;
            yield return Send("POST","/v1/rooms/"+Config.roomId+"/snapshots",JsonUtility.ToJson(snapshot),Config.authorityToken,_=>success=true);
            done(success);
        }
        private IEnumerator Send(string method,string path,string body,string credential,Action<string> receive=null)
        {
            yield return SendBytes(method,path,body==null?null:Encoding.UTF8.GetBytes(body),"application/json",credential,
                bytes=>receive?.Invoke(Encoding.UTF8.GetString(bytes)));
        }
        private IEnumerator SendBytes(string method,string path,byte[] body,string contentType,string credential,Action<byte[]> receive=null)
        {
            using(var request=new UnityWebRequest(Config.serverUrl+path,method))
            {
                request.downloadHandler=new DownloadHandlerBuffer(); request.timeout=35;
                if(body!=null) { request.uploadHandler=new UploadHandlerRaw(body); request.SetRequestHeader("Content-Type",contentType); }
                request.SetRequestHeader("Authorization","Bearer "+credential);
                if(!string.IsNullOrEmpty(Config.certificateSha256)) request.certificateHandler=new PinnedCertificate(Config.certificateSha256);
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)
                {
                    Status="Conversation unavailable. You can keep exploring the qubits.";
                    if(request.responseCode==409 && (path.EndsWith("/snapshots") || path.EndsWith("/events"))) exporter.AuthorityEpoch=0;
                    if(!reconnecting && !string.IsNullOrEmpty(conversationId) &&
                        (request.responseCode==401 || request.responseCode==404) && path.StartsWith(ConversationPath+"/",StringComparison.Ordinal))
                        StartCoroutine(Reconnect());
                    yield break;
                }
                receive?.Invoke(request.downloadHandler.data);
            }
        }
        private static T Parse<T>(string json) where T:class
        { try { return JsonUtility.FromJson<T>(json); } catch(Exception) { return null; } }
        private static AudioClip DecodeAudio(byte[] bytes)
        {
            try { int rate; float[] samples=QuantumWav.Decode(bytes,out rate); var clip=AudioClip.Create("Quantum speech",samples.Length,1,rate,false); clip.SetData(samples,0); return clip; }
            catch(Exception e) { Debug.LogWarning("Invalid companion audio: "+e.Message); return null; }
        }
        private void OnDisable() { Interrupt(); }
        private sealed class PinnedCertificate:CertificateHandler
        {
            private readonly string expected;
            public PinnedCertificate(string hash) { expected=hash.Replace(":","").ToLowerInvariant(); }
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                using(var sha=SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(certificateData)).Replace("-","").ToLowerInvariant()==expected;
            }
        }
    }
}
