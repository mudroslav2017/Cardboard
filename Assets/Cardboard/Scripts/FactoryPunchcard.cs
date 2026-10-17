using UnityEngine;
using static Cardboard.CardboardArt;

namespace Cardboard
{
    public static class FactoryPunchcard
    {
        public static Transform Create(Transform parent, int number)
        {
            var card = Group("Punched work order " + number, parent, Vector3.zero);
            Box("Card", card, Vector3.zero, new Vector3(.42f, .60f, .018f), Cream);
            FactoryGeometry.WorldLabel(number.ToString(), card, new Vector3(0, .10f, .012f), .075f, .32f);
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 6; column++)
                if ((column + row * 2 + number) % 3 != 0)
                    Box("Punch", card, new Vector3(-.15f + column * .06f, -.12f - row * .055f, .012f), new Vector3(.027f, .034f, .006f), Ink);
            return card;
        }
    }
}
