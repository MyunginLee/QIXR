using System.Collections.Generic;
using UnityEngine;

namespace Qixr.Companion
{
    /// <summary>A small, read-only diagram of the state used by an answer.</summary>
    public sealed class QuantumAnswerReference : MonoBehaviour
    {
        private GameObject root;
        private readonly List<Material> materials=new List<Material>();
        public void Clear()
        {
            if(root!=null) Destroy(root);
            root=null;
            foreach(var material in materials) if(material!=null) Destroy(material);
            materials.Clear();
        }
        public void Show(CompanionAnswer answer)
        {
            Clear();
            if(answer.referenceCue==null || !answer.referenceCue.showState || answer.facts?.nodes==null || Camera.main==null) return;
            root=new GameObject("Quantum answer reference (read only)");
            root.transform.SetParent(Camera.main.transform,false);
            root.transform.localPosition=new Vector3(.42f,.28f,1.4f);
            DrawTitle(answer.referenceCue.label,new Vector3(-.09f,.13f,0));
            DrawNodes(answer.facts.nodes,answer.targetQubitIds,0);
            if(answer.comparison?.nodes!=null)
            {
                DrawTitle(answer.responseLanguage=="ko"?"앞선 상태":"Earlier state",new Vector3(-.09f,-.1f,0));
                DrawNodes(answer.comparison.nodes,answer.targetQubitIds,-.23f);
            }
        }
        private void DrawNodes(CompanionNode[] nodes,int[] ids,float y)
        {
            int column=0;
            foreach(var node in nodes)
            {
                if(ids==null || System.Array.IndexOf(ids,node.qubitId)<0) continue;
                Vector3 center=new Vector3(column++*.16f,y,0);
                for(int axis=0;axis<3;axis++)
                {
                    var points=new Vector3[33];
                    for(int i=0;i<points.Length;i++)
                    {
                        float angle=i*Mathf.PI*2/32, a=Mathf.Cos(angle)*.05f, b=Mathf.Sin(angle)*.05f;
                        points[i]=center+(axis==0?new Vector3(0,a,b):axis==1?new Vector3(a,0,b):new Vector3(a,b,0));
                    }
                    Line(points,new Color(.38f,.5f,.6f),.001f);
                }
                if(node.bloch!=null && node.bloch.Length==3)
                {
                    Vector3 end=center+new Vector3((float)node.bloch[0],(float)node.bloch[1],(float)node.bloch[2])*.05f;
                    Line(new[]{center,end},new Color(.3f,.9f,1),.004f);
                    var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name="Captured Bloch endpoint"; Destroy(dot.GetComponent<Collider>());
                    dot.transform.SetParent(root.transform,false); dot.transform.localPosition=end; dot.transform.localScale=Vector3.one*.009f;
                    var material=new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                    dot.GetComponent<Renderer>().sharedMaterial=material;
                    material.color=new Color(.3f,.9f,1); materials.Add(material);
                }
                DrawTitle(node.label+" · 0: "+node.p0.ToString("P1"),center+new Vector3(-.06f,.075f,0));
            }
        }
        private void Line(Vector3[] points,Color color,float width)
        {
            var line=new GameObject("Reference line").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform,false); line.useWorldSpace=false;
            var material=new Material(Shader.Find("Sprites/Default")); materials.Add(material); line.material=material;
            line.startColor=line.endColor=color; line.startWidth=line.endWidth=width;
            line.positionCount=points.Length; line.SetPositions(points);
        }
        private void DrawTitle(string text,Vector3 position)
        {
            var label=new GameObject("Reference caption").AddComponent<TextMesh>();
            label.transform.SetParent(root.transform,false); label.transform.localPosition=position;
            label.text=text; label.fontSize=40; label.characterSize=.006f;
            var font=Resources.Load<Font>("Quantum/NanumGothic-Regular");
            if(font!=null) { label.font=font; label.GetComponent<Renderer>().sharedMaterial=font.material; }
        }
        private void OnDestroy() { Clear(); }
    }
}
