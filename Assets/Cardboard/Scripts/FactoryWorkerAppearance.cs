using UnityEngine;

namespace Cardboard
{
    public enum FactoryFaceState { Normal,Suspicious,Angry }

    public class FactoryWorkerAppearance : MonoBehaviour
    {
        public Material normal,suspicious,angry;
        public FactoryFaceState State {get;private set;}=(FactoryFaceState)(-1);
        Renderer display;
        CardboardDrawingSurface drawing;
        public void SetState(FactoryFaceState state)
        {
            if(State==state)return;
            var material=state==FactoryFaceState.Angry?angry:state==FactoryFaceState.Suspicious?suspicious:normal;
            if(!material)return;
            if(!display)display=GetComponent<Renderer>();
            if(!drawing)drawing=GetComponent<CardboardDrawingSurface>();
            if(drawing)drawing.SetBaseMaterial(material);else display.sharedMaterial=material;
            State=state;
        }
    }
}
