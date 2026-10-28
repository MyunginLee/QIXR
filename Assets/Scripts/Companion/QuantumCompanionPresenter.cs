using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Qixr.Companion
{
    /// <summary>Local presentation only. It never calls a quantum gate or measurement.</summary>
    public sealed class QuantumCompanionPresenter : MonoBehaviour
    {
        private QuantumCompanionClient client;
        private Text caption, status, selection, referenceLabel;
        private InputField question;
        private InputField serverPassword;
        private Button connectButton;
        private GameObject passwordRow;
        private ScrollRect answerScroll;
        private AudioSource speech;
        private readonly Queue<AudioClip> clips=new Queue<AudioClip>();
        private readonly Dictionary<QuantumArpeggioComposer,float> ducked=new Dictionary<QuantumArpeggioComposer,float>();
        private readonly Dictionary<XRGrabInteractable,Action<SelectEnterEventArgs>> grabs=new Dictionary<XRGrabInteractable,Action<SelectEnterEventArgs>>();
        private readonly List<GameObject> halos=new List<GameObject>();
        private QuantumDemoPreview preview;
        private QuantumAnswerReference reference;
        private Button more, next, languageButton, shareButton, askButton, talkButton, stopButton;
        private readonly List<Button> shortcuts=new List<Button>();
        private string nextQuestion, displayedLanguage;
        private int[] displayTargets;
        private Canvas panel;
        private Font font;
        private float nextScan;
        private readonly Dictionary<int, Qubit> knownQubits=new Dictionary<int, Qubit>();
        private bool isDucking;
        private Material markerMaterial;
        private string lastTargets;

        private void Awake()
        {
            client=GetComponent<QuantumCompanionClient>();
            speech=gameObject.AddComponent<AudioSource>(); speech.playOnAwake=false; speech.spatialBlend=0; speech.priority=0;
            preview=gameObject.AddComponent<QuantumDemoPreview>();
            reference=gameObject.AddComponent<QuantumAnswerReference>();
            font=Resources.Load<Font>("Quantum/NanumGothic-Regular");
            markerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            markerMaterial.color=new Color(.3f,.9f,1f);
            if(font==null) font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            client.AnswerReceived+=OnAnswer; client.SpeechReceived+=QueueSpeech; client.Interrupted+=StopSpeech;
            client.TranscriptReceived+=OnTranscript;
            BuildPanel();
        }
        private void Update()
        {
            Camera camera=Camera.main;
            if(panel!=null && panel.transform.parent==null && camera!=null)
            {
                panel.worldCamera=camera;
                panel.transform.SetParent(camera.transform,false); panel.transform.localPosition=new Vector3(-.35f,-.13f,1.1f);
                panel.transform.localRotation=Quaternion.identity; panel.transform.localScale=Vector3.one*.0012f;
            }
            if(status!=null) status.text=client.Status+"\n"+client.Diagnostics;
            if(passwordRow!=null) passwordRow.SetActive(!client.IsPaired || client.UsesPassword);
            if(connectButton!=null)
            {
                connectButton.interactable=!client.IsSigningIn;
                connectButton.GetComponentInChildren<Text>().text=client.IsConnected?"Disconnect":client.IsSigningIn?"Connecting...":"Connect";
            }
            if(displayedLanguage!=client.Config.language) RefreshLabels();
            if(selection!=null) selection.text=L("Quantum   ","양자   ")+string.Join(" + ",Array.ConvertAll(client.Targets,id=>"Q"+(id+1)))+
                (client.SharedPresentation?L("  [Shared]","  [공유]"):L("  [Private]","  [개인]"));
            if(shareButton!=null) shareButton.GetComponentInChildren<Text>().text=client.SharedPresentation?L("Shared","공유"):L("Private","개인");
            string targetSignature=string.Join(",",displayTargets ?? client.Targets);
            if(lastTargets!=targetSignature) { lastTargets=targetSignature; UpdateHalos(); }
            if(!speech.isPlaying && clips.Count>0)
            {
                if(speech.clip!=null) Destroy(speech.clip);
                speech.clip=clips.Dequeue(); speech.Play();
            }
            Duck(speech.isPlaying || clips.Count>0 || client.IsRecording);
            client.PlaybackActive=speech.isPlaying || clips.Count>0;
            if(Time.unscaledTime>=nextScan) { ScanGrabs(); UpdateHalos(); nextScan=Time.unscaledTime+2f; }
            foreach(GameObject halo in halos)
                if(halo!=null) halo.transform.localScale=Vector3.one*(speech.isPlaying?.085f+.008f*Mathf.Sin(Time.unscaledTime*8):.07f);
        }
        private void OnAnswer(CompanionAnswer answer)
        {
            if(answer==null) return;
            caption.text=answer.text;
            caption.rectTransform.sizeDelta=new Vector2(490,Mathf.Max(130,caption.preferredHeight+8));
            caption.rectTransform.anchoredPosition=Vector2.zero;
            answerScroll.verticalNormalizedPosition=1;
            if(answer.status=="quiet") { StopSpeech(); return; }
            displayTargets=answer.targetQubitIds;
            referenceLabel.text=answer.referenceCue==null?"":answer.referenceCue.label+" · "+
                string.Join(" + ",Array.ConvertAll(answer.targetQubitIds ?? new int[0],id=>"Q"+(id+1)));
            reference.Show(answer);
            bool local=!client.IsRemotePresentation;
            more.gameObject.SetActive(local && !string.IsNullOrEmpty(answer.detailText) && answer.text!=answer.detailText);
            nextQuestion=local && answer.nextAction?.kind=="ask"?answer.nextAction.question:null;
            next.gameObject.SetActive(!string.IsNullOrEmpty(nextQuestion));
            if(next.gameObject.activeSelf) next.GetComponentInChildren<Text>().text=answer.nextAction.label;
            if(answer.preview?.kind=="sandbox") preview.Show(answer.preview,answer.responseLanguage);
            UpdateHalos();
        }
        private void OnTranscript(string text) { question.text=text; }
        private void QueueSpeech(AudioClip clip) { clips.Enqueue(clip); }
        private void StopSpeech()
        {
            if(serverPassword!=null) serverPassword.text="";
            if(caption!=null) caption.text="";
            speech.Stop();
            client.PlaybackActive=false;
            if(speech.clip!=null) { Destroy(speech.clip); speech.clip=null; }
            while(clips.Count>0) Destroy(clips.Dequeue());
            if(preview!=null) preview.Clear();
            if(reference!=null) reference.Clear();
            if(referenceLabel!=null) referenceLabel.text="";
            if(more!=null) more.gameObject.SetActive(false);
            if(next!=null) next.gameObject.SetActive(false);
            nextQuestion=null; displayTargets=null;
            Duck(false);
        }
        private void Duck(bool active)
        {
            if (active==isDucking) return;
            isDucking=active;
            if(active)
            {
                foreach(var music in FindObjectsByType<QuantumArpeggioComposer>(FindObjectsSortMode.None))
                    if(!ducked.ContainsKey(music)) { ducked[music]=music.CompanionDuckGain; music.CompanionDuckGain*=.18f; }
            }
            else
            {
                foreach(var item in ducked) if(item.Key!=null) item.Key.CompanionDuckGain=item.Value;
                ducked.Clear();
            }
        }
        private void ScanGrabs()
        {
            foreach(Qubit qubit in FindObjectsByType<Qubit>(FindObjectsSortMode.None))
            {
                var grab=qubit.GetComponent<XRGrabInteractable>();
                if(grab==null || grabs.ContainsKey(grab)) continue;
                int id=qubit.GetIndex(); Action<SelectEnterEventArgs> action=_=>client.SetTargets(id);
                knownQubits[id]=qubit;
                grabs[grab]=action; grab.selectEntered.AddListener(action.Invoke);
            }
        }
        private void UpdateHalos()
        {
            int[] ids=displayTargets ?? client.Targets;
            foreach(GameObject halo in halos) if(halo!=null) halo.SetActive(false);
            foreach(var item in knownQubits)
            {
                Qubit qubit=item.Value;
                if(qubit==null) continue;
                if(Array.IndexOf(ids,qubit.GetIndex())<0) continue;
                GameObject existing=halos.Find(h=>h!=null && h.transform.parent==qubit.transform);
                if(existing!=null) { existing.SetActive(true); continue; }
                var halo=GameObject.CreatePrimitive(PrimitiveType.Sphere); halo.name="Quantum speaking marker (presentation only)";
                Destroy(halo.GetComponent<Collider>());
                halo.transform.SetParent(qubit.transform,false); halo.transform.localPosition=new Vector3(.38f,.35f,0);
                halo.GetComponent<Renderer>().sharedMaterial=markerMaterial;
                halos.Add(halo);
            }
        }
        private void BuildPanel()
        {
            var go=new GameObject("Quantum conversation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(transform,false); panel=go.GetComponent<Canvas>(); panel.renderMode=RenderMode.WorldSpace;
            go.transform.SetParent(null); var rect=go.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(540,730);
            var background=go.AddComponent<Image>(); background.color=new Color(.025f,.04f,.08f,.94f);
            selection=Label(go.transform,"",new Vector2(18,-12),new Vector2(505,40),20);
            status=Label(go.transform,"Connecting…",new Vector2(18,-55),new Vector2(505,55),15);
            referenceLabel=Label(go.transform,"",new Vector2(18,-100),new Vector2(505,25),15);
            var scroll=new GameObject("Answer scroll",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect));
            scroll.transform.SetParent(go.transform,false);
            SetRect(scroll.GetComponent<RectTransform>(),new Vector2(18,-132),new Vector2(505,130));
            scroll.GetComponent<Mask>().showMaskGraphic=false;
            caption=Label(scroll.transform,L("Select a qubit and ask what you notice.","큐빗을 선택하고 눈에 띄는 것을 물어보세요."),Vector2.zero,new Vector2(490,130),22);
            answerScroll=scroll.GetComponent<ScrollRect>(); answerScroll.horizontal=false;
            answerScroll.viewport=scroll.GetComponent<RectTransform>(); answerScroll.content=caption.rectTransform;
            more=Button(go.transform,"More",new Vector2(18,-273),new Vector2(95,34),()=>client.AskText(L("Tell me more","자세히 알려 주세요")));
            next=Button(go.transform,"",new Vector2(125,-273),new Vector2(390,34),()=>{ var ask=nextQuestion; if(!string.IsNullOrEmpty(ask)) client.AskText(ask); });
            more.gameObject.SetActive(false); next.gameObject.SetActive(false);
            for(int i=0;i<3;i++) { int id=i; Button(go.transform,"Q"+(i+1),new Vector2(18+i*95,-318),new Vector2(85,36),()=>client.ToggleTarget(id)); }
            languageButton=Button(go.transform,"EN / 한",new Vector2(310,-318),new Vector2(95,36),()=>client.SetLanguage(client.Config.language=="ko"?"en":"ko"));
            shareButton=Button(go.transform,"Private",new Vector2(415,-318),new Vector2(100,36),()=>client.SharedPresentation=!client.SharedPresentation);
            for(int i=0;i<5;i++) { int index=i; shortcuts.Add(Button(go.transform,"",new Vector2(18+i*100,-364),new Vector2(93,36),()=>AskShortcut(index))); }
            var input=new GameObject("Question",typeof(RectTransform),typeof(Image),typeof(InputField)); input.transform.SetParent(go.transform,false);
            SetRect(input.GetComponent<RectTransform>(),new Vector2(18,-410),new Vector2(395,45));
            input.GetComponent<Image>().color=new Color(.12f,.17f,.24f);
            question=input.GetComponent<InputField>(); question.textComponent=Label(input.transform,"",new Vector2(8,-3),new Vector2(377,40),20);
            question.targetGraphic=input.GetComponent<Image>();
            question.characterLimit=4096;
            question.shouldHideMobileInput=false;
            askButton=Button(go.transform,"Ask",new Vector2(425,-410),new Vector2(90,45),()=>client.AskText(question.text));
            talkButton=Button(go.transform,"Hold to talk",new Vector2(18,-478),new Vector2(395,48),null);
            talkButton.gameObject.AddComponent<QuantumTalkButton>().Client=client;
            stopButton=Button(go.transform,"Stop",new Vector2(425,-478),new Vector2(90,48),()=>client.Interrupt());
            Label(go.transform,"Install the server connection profile using QIXR > Companion before building. For password access, enter Server Password below and press Connect.",new Vector2(18,-536),new Vector2(505,45),14);
            Button(go.transform,"Import pairing JSON from question field",new Vector2(18,-590),new Vector2(495,34),()=>
            { if(client.InstallPairing(question.text)) question.text=""; });
            passwordRow=new GameObject("Server sign-in",typeof(RectTransform));
            passwordRow.transform.SetParent(go.transform,false);
            SetRect(passwordRow.GetComponent<RectTransform>(),new Vector2(18,-640),new Vector2(495,70));
            Label(passwordRow.transform,"Server Password",Vector2.zero,new Vector2(375,22),16);
            var passwordObject=new GameObject("Server Password",typeof(RectTransform),typeof(Image),typeof(InputField));
            passwordObject.transform.SetParent(passwordRow.transform,false);
            SetRect(passwordObject.GetComponent<RectTransform>(),new Vector2(0,-25),new Vector2(360,40));
            passwordObject.GetComponent<Image>().color=new Color(.12f,.17f,.24f);
            serverPassword=passwordObject.GetComponent<InputField>();
            serverPassword.textComponent=Label(passwordObject.transform,"",new Vector2(8,-3),new Vector2(344,34),20);
            serverPassword.targetGraphic=passwordObject.GetComponent<Image>();
            serverPassword.contentType=InputField.ContentType.Password;
            serverPassword.characterLimit=256; serverPassword.shouldHideMobileInput=false;
            connectButton=Button(passwordRow.transform,"Connect",new Vector2(370,-25),new Vector2(125,40),()=>
            {
                if(client.IsConnected) client.DisconnectPasswordSession();
                else
                {
                    string password=serverPassword.text;
                    serverPassword.text="";
                    client.SignInWithPassword(password);
                }
            });
            RefreshLabels();
            EnsureEventSystem();
        }
        public static void EnsureEventSystem()
        {
            EventSystem system=EventSystem.current ?? FindFirstObjectByType<EventSystem>();
            if(system==null) system=new GameObject("Quantum XR EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            foreach(var module in system.GetComponents<BaseInputModule>()) if(!(module is XRUIInputModule)) module.enabled=false;
            var xr=system.GetComponent<XRUIInputModule>() ?? system.gameObject.AddComponent<XRUIInputModule>();
            xr.enabled=true;
        }
        private string L(string en,string ko) { return client.Config.language=="ko"?ko:en; }
        private void AskShortcut(int index)
        {
            string[] en={"Explain this arrow","What are the Z probabilities?","Are you two entangled?","What just happened?","Preview H on a copy"};
            string[] ko={"지금 화살표를 설명해 주세요","Z 측정 확률은?","너희 둘은 얽혀 있어요?","방금 무슨 일이 일어났어요?","복사본에 H를 적용하면?"};
            client.AskText(client.Config.language=="ko"?ko[index]:en[index]);
        }
        private void RefreshLabels()
        {
            displayedLanguage=client.Config.language;
            string[] en={"Arrow","Chances","Pair","Last action","Preview H"};
            string[] ko={"화살표","확률","관계","방금","가상 H"};
            for(int i=0;i<shortcuts.Count;i++) shortcuts[i].GetComponentInChildren<Text>().text=displayedLanguage=="ko"?ko[i]:en[i];
            more.GetComponentInChildren<Text>().text=L("More","더 보기");
            askButton.GetComponentInChildren<Text>().text=L("Ask","질문");
            talkButton.GetComponentInChildren<Text>().text=L("Hold to talk","누르고 말하기");
            stopButton.GetComponentInChildren<Text>().text=L("Stop","중단");
        }
        private Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            SetRect(go.GetComponent<RectTransform>(),pos,size); var label=go.GetComponent<Text>(); label.font=font; label.text=text;
            label.fontSize=fontSize; label.color=Color.white; label.raycastTarget=false; label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.verticalOverflow=VerticalWrapMode.Overflow; return label;
        }
        private Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Action action)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            SetRect(go.GetComponent<RectTransform>(),pos,size); go.GetComponent<Image>().color=new Color(.1f,.25f,.35f);
            var button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>();
            if(action!=null) button.onClick.AddListener(action.Invoke);
            var label=Label(go.transform,text,new Vector2(4,-3),size-new Vector2(8,6),17); label.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        private static void SetRect(RectTransform rect,Vector2 pos,Vector2 size)
        { rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1); rect.anchoredPosition=pos; rect.sizeDelta=size; }
        private void OnDestroy()
        {
            if(client!=null) { client.AnswerReceived-=OnAnswer; client.SpeechReceived-=QueueSpeech; client.Interrupted-=StopSpeech; }
            if(client!=null) client.TranscriptReceived-=OnTranscript;
            StopSpeech();
            if(markerMaterial!=null) Destroy(markerMaterial);
            foreach(var pair in grabs) if(pair.Key!=null) pair.Key.selectEntered.RemoveListener(pair.Value.Invoke);
            foreach(GameObject halo in halos) if(halo!=null) Destroy(halo);
            if(panel!=null) Destroy(panel.gameObject);
        }
        private void OnDisable() { if(speech!=null) StopSpeech(); }
    }
}
