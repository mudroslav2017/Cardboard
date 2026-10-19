using UnityEngine;

namespace Cardboard
{
    public class FactoryLunchControl : MonoBehaviour
    {
        public FactoryLunch lunch;
        public bool serve;
        public string Prompt => !lunch?"":serve?
            (lunch.CanServe?"E - SET THE TRAY ON THE TABLE":"Balance the tray before setting it down"):
            lunch.State==LunchState.Waiting?"Workers noticed the fallen lunch":
            lunch.game.HasPart?"Spilled food: the dishes slid off the tray":"E - COLLECT YOUR LUNCH TRAY";
        public bool Use() => lunch && (serve?lunch.TryServe():lunch.TryTake());
    }
}
