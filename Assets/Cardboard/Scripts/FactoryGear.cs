using System;
using UnityEngine;

namespace Cardboard
{
    public enum FactoryHub { Square, Hexagon, Round }
    public enum FactoryDeath { Buttons, Pins, Press }
    public enum FactoryState { Working, Chased, Captured, Dead, Escaped }
    public enum FactoryAction { Gear, Blueprint, Bench, Disguise, Exit }

    [Serializable]
    public struct FactoryRecipe : IEquatable<FactoryRecipe>
    {
        public int teeth;
        public FactoryHub hub;
        public int layers;
        public FactoryRecipe(int teeth,FactoryHub hub,int layers) { this.teeth=teeth;this.hub=hub;this.layers=layers; }
        public bool Equals(FactoryRecipe other) => teeth==other.teeth && hub==other.hub && layers==other.layers;
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
        public void SetBroken(bool value)
        {
            broken=value;
            if(healthy) healthy.gameObject.SetActive(!value);
            if(damaged) damaged.gameObject.SetActive(value);
            if(indicator) indicator.color=value?new Color(1,.19f,.08f):new Color(.3f,1,.6f);
        }
        void Update()
        {
            var game=CardboardFactoryGame.Instance;
            if(!broken && healthy && (!game || !game.Ended))
                healthy.Rotate(0,0,(index%2==0?1:-1)*Time.deltaTime*26,Space.Self);
        }
    }
}
