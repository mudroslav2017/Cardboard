using UnityEngine;

namespace Cardboard
{
    public enum KitchenAction { Rat, Potato, Knife, Peeler, Board, Prep, Pan, Burner, Spatula, Pot, Serve, Bin, Bowl, Tray, SoupBurner, PotRest }

    public sealed class FactoryKitchenControl : MonoBehaviour
    {
        public FactoryKitchen kitchen;
        public KitchenAction action;
        public int slot;
        public string Prompt => kitchen.Prompt(action, slot);
        public bool AcceptsHeld => kitchen.AcceptsHeld(action);
        public bool Use() => kitchen.Use(action, slot);
    }
}
