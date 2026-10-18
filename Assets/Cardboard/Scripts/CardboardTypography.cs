using UnityEngine;

namespace Cardboard
{
    // Shared font reference for current and future world labels and UI.
    public class CardboardTypography : ScriptableObject
    {
        public Font font;
        public static Font DefaultFont
        {
            get
            {
                var settings=Resources.Load<CardboardTypography>("Typography");
                return settings && settings.font?settings.font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }
    }
}
