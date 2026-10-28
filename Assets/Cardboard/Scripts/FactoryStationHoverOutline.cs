using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    [DefaultExecutionOrder(105),DisallowMultipleComponent]
    public class FactoryStationHoverOutline : MonoBehaviour
    {
        const string ShellName="Station hover shell";
        public FactoryStationControl Target {get;private set;}
        public int VisibleMeshCount
        {
            get{int count=0;foreach(var shell in visible)if(shell && shell.enabled && shell.gameObject.activeInHierarchy)count++;return count;}
        }
        readonly Dictionary<MeshFilter,MeshRenderer> cached=new Dictionary<MeshFilter,MeshRenderer>();
        readonly List<MeshRenderer> visible=new List<MeshRenderer>();
        FactoryWorkstation station;
        Material outline;
        void Awake(){station=GetComponent<FactoryWorkstation>();}
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            var game=CardboardFactoryGame.Instance;FactoryStationControl control=null;
            if(game && station && station.isPlayerStation && !game.PlayerBlocked && !game.CaptureActive && !CardboardMarker.DrawingMode)
            {
                var focus=game.Focus;
                if(focus && focus.action==FactoryAction.Station)
                {
                    var candidate=focus.GetComponent<FactoryStationControl>();
                    if(candidate && candidate.station==station && station.CanInteract(candidate.part,candidate.number))control=candidate;
                }
            }
            if(Target==control)return;
            Hide();Target=control;if(!control)return;
            Transform root=ModelRoot(control);if(!root || !root.gameObject.activeInHierarchy)return;
            if(!outline)
            {
                var shader=Resources.Load<Shader>("StationHoverOutline");
                if(!shader)return;
                outline=new Material(shader){name="Warm station interaction outline",hideFlags=HideFlags.DontSave};
            }
            // Use original meshes and transforms, without copying colliders or changing source materials.
            foreach(var source in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=source.GetComponent<MeshRenderer>();
                if(source.name==ShellName || !source.sharedMesh || !renderer || !renderer.enabled || source.GetComponent<TextMesh>())continue;
                if(!cached.TryGetValue(source,out var shell) || !shell)
                {
                    var node=new GameObject(ShellName);node.layer=9;node.transform.SetParent(source.transform,false);
                    node.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                    shell=node.AddComponent<MeshRenderer>();shell.sharedMaterial=outline;
                    shell.shadowCastingMode=ShadowCastingMode.Off;shell.receiveShadows=false;
                    shell.lightProbeUsage=LightProbeUsage.Off;shell.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    cached[source]=shell;
                }
                shell.enabled=true;visible.Add(shell);
            }
        }
        public Transform ModelRoot(FactoryStationControl control)
        {
            if(!control || !station)return null;
            switch(control.part)
            {
                case StationPart.Feed:return station.FeedMotion;
                case StationPart.Press:return station.PressMotion;
                case StationPart.Selector:return station.selector;
                case StationPart.Guard:return station.transform.Find("Grille lift button left of press");
                case StationPart.Wheel:return station.wheel;
                case StationPart.Output:return station.cutPart;
                case StationPart.Bath:return control.number>=1 && control.number<=station.baths.Length?
                    station.baths[control.number-1].Find("Imported Box vessel"):null;
                default:return null;
            }
        }
        void Hide(){foreach(var shell in visible)if(shell)shell.enabled=false;visible.Clear();Target=null;}
        void OnDisable(){Hide();}
        void OnDestroy()
        {
            Hide();foreach(var shell in cached.Values)if(shell)Destroy(shell.gameObject);
            cached.Clear();if(outline)Destroy(outline);
        }
    }
}