using UnityEngine;

namespace Cardboard
{
    public class FactoryLunchControl : MonoBehaviour
    {
        public FactoryLunch lunch;
        public bool serve;
        public string Prompt => !lunch?"":serve?
            (lunch.CanServe?"E — поставить поднос и пообедать":"Выровняйте поднос перед тем, как поставить"):
            lunch.State==LunchState.Waiting?"Раздача откроется во время обеда":
            lunch.game.HasPart?"Освободите руки: установите деталь или положите её на ПКМ":"E — взять поднос с обедом";
        public bool Use() => lunch && (serve?lunch.TryServe():lunch.TryTake());
    }
}
