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
        private ScrollRect answerScroll;
        private AudioSource speech;
        private readonly Queue<AudioClip> clips=new Queue<AudioClip>();
        private readonly Dictionary<AudioSource,float> ducked=new Dictionary<AudioSource,float>();
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

        private void Awake()
        {
            client=GetComponent<QuantumCompanionClient>();
            speech=gameObject.AddComponent<AudioSource>(); speech.playOnAwake=false; speech.spatialBlend=0; speech.priority=0;
            preview=gameObject.AddComponent<QuantumDemoPreview>();
            reference=gameObject.AddComponent<QuantumAnswerReference>();
            font=Resources.Load<Font>("Quantum/NanumGothic-Regular");
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
                panel.transform.SetParent(camera.transform,false); panel.transform.localPosition=new Vector3(-.35f,-.13f,1.1f);
                panel.transform.localRotation=Quaternion.identity; panel.transform.localScale=Vector3.one*.0012f;
            }
            if(status!=null) status.text=client.Status;
            if(displayedLanguage!=client.Config.language) RefreshLabels();
            if(selection!=null) selection.text=L("Quantum   ","양자   ")+string.Join(" + ",Array.ConvertAll(client.Targets,id=>"Q"+(id+1)))+
                (client.SharedPresentation?L("  [Shared]","  [공유]"):L("  [Private]","  [개인]"));
            if(shareButton!=null) shareButton.GetComponentInChildren<Text>().text=client.SharedPresentation?L("Shared","공유"):L("Private","개인");
            if(!speech.isPlaying && clips.Count>0)
            {
                if(speech.clip!=null) Destroy(speech.clip);
                speech.clip=clips.Dequeue(); speech.Play();
            }
            Duck(speech.isPlaying || clips.Count>0 || client.IsRecording);
            if(Time.unscaledTime>=nextScan) { ScanGrabs(); UpdateHalos(); nextScan=Time.unscaledTime+.3f; }
            foreach(GameObject halo in halos)
                if(halo!=null) halo.transform.localScale=Vector3.one*(speech.isPlaying?.085f+.008f*Mathf.Sin(Time.unscaledTime*8):.07f);
        }
        private void OnAnswer(CompanionAnswer answer)
        {
            caption.text=answer.text;
            caption.rectTransform.sizeDelta=new Vector2(490,Mathf.Max(130,caption.preferredHeight+8));
            caption.rectTransform.anchoredPosition=Vector2.zero;
            answerScroll.verticalNormalizedPosition=1;
            displayTargets=answer.targetQubitIds;
            referenceLabel.text=answer.referenceCue==null?"":answer.referenceCue.label+" · "+
                string.Join(" + ",Array.ConvertAll(answer.targetQubitIds ?? new int[0],id=>"Q"+(id+1)));
            reference.Show(answer);
            bool local=!client.IsRemotePresentation;
            more.gameObject.SetActive(local && !string.IsNullOrEmpty(answer.detailText) && answer.text!=answer.detailText);
            nextQuestion=local?answer.nextAction?.question:null;
            next.gameObject.SetActive(!string.IsNullOrEmpty(nextQuestion));
            if(next.gameObject.activeSelf) next.GetComponentInChildren<Text>().text=answer.nextAction.label;
            if(answer.preview!=null) preview.Show(answer.preview,answer.responseLanguage);
            UpdateHalos();
        }
        private void OnTranscript(string text) { question.text=text; }
        private void QueueSpeech(AudioClip clip) { clips.Enqueue(clip); }
        private void StopSpeech()
        {
            if(caption!=null) caption.text="";
            speech.Stop();
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
            if(active)
            {
                foreach(var music in FindObjectsByType<QuantumArpeggioComposer>(FindObjectsSortMode.None))
                    foreach(var source in music.GetComponentsInChildren<AudioSource>())
                        if(source!=speech && !ducked.ContainsKey(source)) { ducked[source]=source.volume; source.volume*=.18f; }
            }
            else
            {
                foreach(var item in ducked) if(item.Key!=null) item.Key.volume=item.Value;
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
                grabs[grab]=action; grab.selectEntered.AddListener(action.Invoke);
            }
        }
        private void UpdateHalos()
        {
            int[] ids=displayTargets ?? client.Targets;
            foreach(GameObject halo in halos) if(halo!=null) Destroy(halo);
            halos.Clear();
            foreach(Qubit qubit in FindObjectsByType<Qubit>(FindObjectsSortMode.None))
            {
                if(Array.IndexOf(ids,qubit.GetIndex())<0) continue;
                var halo=GameObject.CreatePrimitive(PrimitiveType.Sphere); halo.name="Quantum speaking marker (presentation only)";
                Destroy(halo.GetComponent<Collider>());
                halo.transform.SetParent(qubit.transform,false); halo.transform.localPosition=new Vector3(.38f,.35f,0);
                halo.GetComponent<Renderer>().material.color=new Color(.3f,.9f,1);
                halos.Add(halo);
            }
        }
        private void BuildPanel()
        {
            var go=new GameObject("Quantum conversation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(transform,false); panel=go.GetComponent<Canvas>(); panel.renderMode=RenderMode.WorldSpace;
            go.transform.SetParent(null); var rect=go.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(540,550);
            var background=go.AddComponent<Image>(); background.color=new Color(.025f,.04f,.08f,.94f);
            selection=Label(go.transform,"",new Vector2(18,-12),new Vector2(505,40),20);
            status=Label(go.transform,"Connecting…",new Vector2(18,-55),new Vector2(505,45),17);
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
            languageButton=Button(go.transform,"EN / 한",new Vector2(310,-318),new Vector2(95,36),()=>{ client.Interrupt(); client.Config.language=client.Config.language=="ko"?"en":"ko"; });
            shareButton=Button(go.transform,"Private",new Vector2(415,-318),new Vector2(100,36),()=>{ client.Interrupt(); client.SharedPresentation=!client.SharedPresentation; });
            for(int i=0;i<5;i++) { int index=i; shortcuts.Add(Button(go.transform,"",new Vector2(18+i*100,-364),new Vector2(93,36),()=>AskShortcut(index))); }
            var input=new GameObject("Question",typeof(RectTransform),typeof(Image),typeof(InputField)); input.transform.SetParent(go.transform,false);
            SetRect(input.GetComponent<RectTransform>(),new Vector2(18,-410),new Vector2(395,45));
            input.GetComponent<Image>().color=new Color(.12f,.17f,.24f);
            question=input.GetComponent<InputField>(); question.textComponent=Label(input.transform,"",new Vector2(8,-3),new Vector2(377,40),20);
            question.characterLimit=500;
            askButton=Button(go.transform,"Ask",new Vector2(425,-410),new Vector2(90,45),()=>client.AskText(question.text));
            talkButton=Button(go.transform,"Hold to talk",new Vector2(18,-478),new Vector2(395,48),null);
            talkButton.gameObject.AddComponent<QuantumTalkButton>().Client=client;
            stopButton=Button(go.transform,"Stop",new Vector2(425,-478),new Vector2(90,48),()=>client.Interrupt());
            RefreshLabels();
            if(EventSystem.current==null) new GameObject("Quantum XR EventSystem",typeof(EventSystem),typeof(XRUIInputModule));
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
            foreach(var pair in grabs) if(pair.Key!=null) pair.Key.selectEntered.RemoveListener(pair.Value.Invoke);
            foreach(GameObject halo in halos) if(halo!=null) Destroy(halo);
            if(panel!=null) Destroy(panel.gameObject);
        }
    }
}
