using UnityEngine;

namespace Cardboard
{
    public enum KitchenAction { Rat, Potato, Knife, Peeler, Board, Prep, Pan, Burner, Spatula, Pot, Serve, Bin }

    public sealed class FactoryKitchenControl : MonoBehaviour
    {
        public FactoryKitchen kitchen;
        public KitchenAction action;
        public string Prompt => kitchen.Prompt(action);
        public bool AcceptsHeld => kitchen.AcceptsHeld(action);
        public bool Use() => kitchen.Use(action);
    }
}
