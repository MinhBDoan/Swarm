using UnityEditor;
using UnityEngine;
namespace Swarm.Editor
{
    public static class DownloadSetup
    {
        public static void Install()
        {
            var old=GameObject.Find("Objectives / Download Terminals"); if(old) Object.DestroyImmediate(old);
            var root=new GameObject("Objectives / Download Terminals").transform;
            for(int i=0;i<CanyonLayout.DownloadCount;i++)
            {
                var tier=(CombatPlayground.UpgradeRarity)CanyonLayout.DownloadTierIndex(i); var center=CanyonLayout.DownloadCenters[i];
                string path="Assets/Materials/Download"+tier+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material) {material=new Material(Shader.Find("Unlit/Color"));AssetDatabase.CreateAsset(material,path);}
                material.color=CombatPlayground.RarityColor(tier); EditorUtility.SetDirty(material);
                var console=GameObject.CreatePrimitive(PrimitiveType.Cube); console.name=tier+" download console"; console.transform.SetParent(root);
                console.transform.position=center+new Vector3(0,.45f,3); console.transform.localScale=new Vector3(.8f,.9f,.6f); console.GetComponent<Renderer>().sharedMaterial=material;
                var zone=new GameObject(tier+" download zone",typeof(DownloadTerminal)); zone.transform.SetParent(root); zone.transform.position=center; zone.GetComponent<DownloadTerminal>().terminalIndex=i;
                var sign=new GameObject(tier+" download label",typeof(TextMesh)); sign.transform.SetParent(root); sign.transform.position=center+new Vector3(0,2.7f,4);
                var text=sign.GetComponent<TextMesh>(); text.text=tier.ToString().ToUpperInvariant()+" DOWNLOAD / "+CanyonLayout.DownloadDuration(i)+"s";
                text.anchor=TextAnchor.MiddleCenter; text.fontSize=64; text.characterSize=.1f; text.color=CombatPlayground.RarityColor(tier);
                var terminal=zone.GetComponent<DownloadTerminal>();
                terminal.consoleProp=console.transform;terminal.labelProp=sign.transform;
            }
        }
    }
}
