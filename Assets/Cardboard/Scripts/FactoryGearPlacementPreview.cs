using UnityEngine;

namespace Cardboard
{
    [DefaultExecutionOrder(100)]
    public class FactoryGearPlacementPreview : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public FactoryGear Target {get;private set;}
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            var focus=game?game.Focus:null;
            bool visible=game && game.HasPart && !game.DippingPart && !game.HandsBusy && !game.CaptureActive && !game.PlayerBlocked && !CardboardMarker.DrawingMode;
            SetTarget(visible && focus && focus.action==FactoryAction.Gear && focus.gear && focus.gear.broken?focus.gear:null);
        }
        void SetTarget(FactoryGear gear)
        {
            if(Target && Target!=gear && Target.placementOutline)Target.placementOutline.gameObject.SetActive(false);
            Target=gear;
            if(Target && Target.placementOutline)Target.placementOutline.gameObject.SetActive(true);
        }
        void OnDisable(){SetTarget(null);}
    }
}