using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    [DefaultExecutionOrder(100)]
    public class FactoryGearPlacementPreview : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public FactoryGear Target {get;private set;}
        public FactoryStationControl BathTarget {get;private set;}
        readonly Dictionary<Transform,LineRenderer> bathOutlines=new Dictionary<Transform,LineRenderer>();
        LineRenderer activeBathOutline;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            var focus=game?game.Focus:null;
            bool visible=game && game.HasPart && !game.DippingPart && !game.HandsBusy && !game.CaptureActive && !game.PlayerBlocked && !CardboardMarker.DrawingMode;
            SetTarget(visible && focus && focus.action==FactoryAction.Gear && focus.gear && focus.gear.broken?focus.gear:null);
            var control=visible && focus && focus.action==FactoryAction.Station?focus.GetComponent<FactoryStationControl>():null;
            SetBathTarget(control && control.part==StationPart.Bath && control.station && control.station.isPlayerStation &&
                control.station.Stage==StationStage.Empty?control:null);
        }
        void SetTarget(FactoryGear gear)
        {
            if(Target && Target!=gear && Target.placementOutline)Target.placementOutline.gameObject.SetActive(false);
            Target=gear;
            if(Target && Target.placementOutline)Target.placementOutline.gameObject.SetActive(true);
        }
        void SetBathTarget(FactoryStationControl control)
        {
            if(BathTarget==control)return;
            if(activeBathOutline)activeBathOutline.gameObject.SetActive(false);
            BathTarget=control;activeBathOutline=null;
            if(!control || control.number<1 || control.number>control.station.baths.Length)return;
            var bath=control.station.baths[control.number-1];
            if(!bathOutlines.TryGetValue(bath,out activeBathOutline) || !activeBathOutline)
            {
                activeBathOutline=CreateBathOutline(bath);
                if(activeBathOutline)bathOutlines[bath]=activeBathOutline;
            }
            if(activeBathOutline)activeBathOutline.gameObject.SetActive(true);
        }
        LineRenderer CreateBathOutline(Transform bath)
        {
            var source=bath.Find("Imported Box vessel");if(!source)return null;
            var material=game.intro?game.intro.glow:null;
            if(!material && game.gears.Length>0 && game.gears[0].placementOutline)
                material=game.gears[0].placementOutline.GetComponent<LineRenderer>().sharedMaterial;
            if(!material)return null;
            Bounds bounds=default;bool first=true;
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var meshBounds=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 point=meshBounds.center+Vector3.Scale(meshBounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    point=bath.InverseTransformPoint(filter.transform.TransformPoint(point));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            if(first)return null;
            var root=new GameObject("Bath selection outline");root.layer=9;root.transform.SetParent(bath,false);
            var line=root.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=4;
            Vector3 min=bounds.min-Vector3.one*.015f,max=bounds.max+Vector3.one*.015f;
            line.SetPositions(new[]{new Vector3(min.x,max.y,min.z),new Vector3(min.x,max.y,max.z),
                new Vector3(max.x,max.y,max.z),new Vector3(max.x,max.y,min.z)});
            line.widthMultiplier=.024f;line.numCornerVertices=2;line.sharedMaterial=material;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return line;
        }
        void OnDisable(){SetTarget(null);SetBathTarget(null);}
    }
}
