using UnityEngine;

namespace Qixr.Companion
{
    public sealed class QuantumDemoPreview : MonoBehaviour
    {
        private GameObject root;
        private readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
        public void Clear()
        {
            if(root!=null) Destroy(root); root=null;
            foreach(var material in materials) if(material!=null) Destroy(material);
            materials.Clear();
        }
        public void Show(PreviewDto preview,string language="en")
        {
            Clear(); Camera camera=Camera.main; if(camera==null) return;
            root=new GameObject("SANDBOX — copy of "+preview.sourceSnapshotId);
            root.transform.SetParent(camera.transform,false);
            root.transform.localPosition=new Vector3(.5f,-.45f,1.4f);
            var label=new GameObject("SANDBOX label").AddComponent<TextMesh>(); label.transform.SetParent(root.transform,false);
            Font font=Resources.Load<Font>("Quantum/NanumGothic-Regular");
            if(font!=null) { label.font=font; label.GetComponent<Renderer>().sharedMaterial=font.material; }
            label.transform.localPosition=new Vector3(-.2f,.22f,0); label.characterSize=.013f; label.fontSize=40;
            label.text=(language=="ko"?"가상 실험":"SANDBOX")+"\n"+preview.operation+" → Q"+(preview.target+1);
            if(preview.branches!=null && preview.branches.Length>0)
            {
                foreach(var branch in preview.branches) label.text+="\n"+branch.outcome+": "+branch.probability.ToString("P1");
            }
            else if(preview.facts?.nodes!=null)
            {
                for(int i=0;i<preview.facts.nodes.Length;i++)
                {
                    var node=preview.facts.nodes[i]; var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name="Sandbox Q"+(i+1); sphere.transform.SetParent(root.transform,false);
                    sphere.transform.localPosition=new Vector3((i-1)*.15f,0,0);
                    sphere.transform.localScale=Vector3.one*.07f; Destroy(sphere.GetComponent<Collider>());
                    var material=new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                    materials.Add(material); sphere.GetComponent<Renderer>().sharedMaterial=material;
                    material.color=new Color(.4f,.65f,.9f);
                    if(node.bloch!=null && node.bloch.Length==3)
                    {
                        var arrow=new GameObject("Sandbox Bloch arrow").AddComponent<LineRenderer>();
                        arrow.transform.SetParent(sphere.transform,false); arrow.useWorldSpace=false;
                        var lineMaterial=new Material(Shader.Find("Sprites/Default")); materials.Add(lineMaterial);
                        arrow.sharedMaterial=lineMaterial; arrow.startWidth=arrow.endWidth=.03f;
                        arrow.positionCount=2; arrow.SetPosition(0,Vector3.zero);
                        arrow.SetPosition(1,new Vector3((float)node.bloch[0],(float)node.bloch[1],(float)node.bloch[2])*.8f);
                    }
                    label.text+="\nQ"+(i+1)+"  P(0)="+node.p0.ToString("P1");
                }
            }
        }
        private void OnDestroy() { Clear(); }
    }
}
