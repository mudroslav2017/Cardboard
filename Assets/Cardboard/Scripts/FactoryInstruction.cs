using UnityEngine;

namespace Cardboard
{
    public class FactoryInstruction : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Transform sheet,controlHint,flipHint;
        public BoxCollider pickup;
        public FactoryPhysicsProp physical;
        public Vector3 floorPosition;
        public Quaternion floorRotation=Quaternion.identity;
        public bool Held => physical && physical.IsHeld;
        public bool Reverse {get;private set;}
        public bool Animating {get;private set;}
        public bool HasBeenPickedUp {get;private set;}
        public bool Highlighted => floorHighlight && floorHighlight.enabled;
        LineRenderer floorHighlight;
        float flipUntil;
        public void HighlightForTutorial(Material material)
        {
            if(HasBeenPickedUp || Held || !sheet || !pickup || !material)return;
            if(!floorHighlight)
            {
                var outline=new GameObject("Tutorial instruction glow");outline.layer=9;outline.transform.SetParent(sheet,false);
                floorHighlight=outline.AddComponent<LineRenderer>();floorHighlight.useWorldSpace=false;floorHighlight.loop=true;
                floorHighlight.widthMultiplier=.025f;floorHighlight.numCornerVertices=4;floorHighlight.positionCount=4;
                floorHighlight.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;floorHighlight.receiveShadows=false;
                Vector3 min=pickup.center-pickup.size*.5f,max=pickup.center+pickup.size*.5f;
                float z=min.z-.025f;
                floorHighlight.SetPositions(new[]{new Vector3(min.x-.04f,min.y-.04f,z),new Vector3(min.x-.04f,max.y+.04f,z),
                    new Vector3(max.x+.04f,max.y+.04f,z),new Vector3(max.x+.04f,min.y-.04f,z)});
            }
            floorHighlight.sharedMaterial=material;floorHighlight.enabled=true;
        }
        public void MarkPickedUp()
        {
            HasBeenPickedUp=true;
            if(floorHighlight)floorHighlight.enabled=false;
        }
        public bool Take()
        {
            if(!physical || !physical.Take(game))return false;
            Reverse=Animating=false;return true;
        }
        public bool Flip()
        {
            if(!Held || Animating || game.MenuActive || game.CaptureActive || game.Ended)return false;
            Reverse=!Reverse;Animating=true;flipUntil=Time.time+.38f;
            game.objectCarry.FlipInstruction(Reverse);return true;
        }
        void Update(){if(Animating && Time.time>=flipUntil)Animating=false;}
        void LateUpdate(){RefreshHints();}
        public void RefreshHints()
        {
            if(!sheet || !Camera.main)return;
            bool front=Vector3.Dot(sheet.forward,sheet.position-Camera.main.transform.position)>0;
            if(controlHint)controlHint.gameObject.SetActive(front);
            if(flipHint)flipHint.gameObject.SetActive(front);
        }
        public void PutDown(){if(Held && game.objectCarry)game.objectCarry.Release(false);}
        public void Released(){Reverse=Animating=false;}
        public void ResetFloor()
        {
            if(!sheet)return;
            if(Held && game.objectCarry)game.objectCarry.Clear();
            Released();HasBeenPickedUp=false;
            if(floorHighlight)floorHighlight.enabled=false;
            sheet.SetParent(transform,true);sheet.localScale=Vector3.one;
            sheet.SetPositionAndRotation(floorPosition,floorRotation);
            if(physical)
            {
                physical.homePosition=floorPosition;physical.homeRotation=floorRotation;physical.homeStored=true;
                physical.ResetHome();
            }
            pickup.enabled=true;
        }
    }
}
