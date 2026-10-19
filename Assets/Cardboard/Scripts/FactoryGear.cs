using System;
using UnityEngine;

namespace Cardboard
{
    public enum FactoryHub { Square, Hexagon, Round }
    public enum FactoryDeath { Buttons, Pins, Press }
    public enum FactoryState { Working, Chased, Captured, Dead, Escaped }
    public enum FactoryAction { Gear, Blueprint, Bench, Reserved, Exit, Station, Lunch, Instruction, Door, PhysicsProp }

    [Serializable]
    public struct FactoryRecipe : IEquatable<FactoryRecipe>
    {
        public int teeth;
        public FactoryHub hub;
        public int layers;
        public int finish;
        public int Type => Mathf.Clamp((teeth-8)/2+1,1,3);
        public FactoryRecipe(int teeth,FactoryHub hub,int layers) { this.teeth=teeth;this.hub=hub;this.layers=layers;finish=0; }
        public static FactoryRecipe ForType(int type,bool finished=true)
        { type=Mathf.Clamp(type,1,3);return new FactoryRecipe(6+type*2,(FactoryHub)(type-1),1) {finish=finished?type:0}; }
        public bool Equals(FactoryRecipe other) => teeth==other.teeth && hub==other.hub && layers==other.layers && finish==other.finish;
        public static string HubName(FactoryHub hub) => hub==FactoryHub.Square?"квадрат":hub==FactoryHub.Hexagon?"шестигранник":"круг";
        public override string ToString() => teeth+" зубьев · "+HubName(hub)+" · слоёв: "+layers;
    }

    public class FactoryGear : MonoBehaviour
    {
        public int index;
        public FactoryRecipe recipe;
        public bool broken=true;
        public Transform healthy,damaged;
        public Light indicator;
        public Transform punchcard;
        Vector3 cardHome=new Vector3(0,.20f,.14f);
        float cardTravel;
        bool printCard;
        void Start() { if(punchcard && !printCard) punchcard.gameObject.SetActive(false); }
        public void PrintCard() { printCard=true;if(punchcard) punchcard.gameObject.SetActive(true); }
        public void SetBroken(bool value)
        {
            broken=value;
            if(value && punchcard) {printCard=false;cardTravel=0;punchcard.localPosition=cardHome;punchcard.gameObject.SetActive(false);}
            if(healthy) healthy.gameObject.SetActive(!value);
            if(damaged) damaged.gameObject.SetActive(value);
            if(indicator) indicator.color=value?new Color(1,.19f,.08f):new Color(.3f,1,.6f);
        }
        void Update()
        {
            if(punchcard && printCard)
            { cardTravel=Mathf.MoveTowards(cardTravel,1,Time.deltaTime*1.8f);punchcard.localPosition=cardHome+Vector3.down*(cardTravel*.5f); }
            var game=CardboardFactoryGame.Instance;
            if(!broken && healthy && (!game || !game.Ended))
                healthy.Rotate(0,0,(index%2==0?1:-1)*Time.deltaTime*26,Space.Self);
        }
    }
}
