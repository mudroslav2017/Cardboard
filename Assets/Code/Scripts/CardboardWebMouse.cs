using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Cardboard
{
    // Browser look input comes only from an actually locked canvas, never absolute cursor positions.
    public static class CardboardWebMouse
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CardboardWebMouse_Configure(int capture,int tracking);
        [DllImport("__Internal")] static extern int CardboardWebMouse_Read([Out] float[] delta);
        static readonly float[] delta=new float[2];
#endif
        public static void Configure(bool capture,bool tracking)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CardboardWebMouse_Configure(capture?1:0,tracking?1:0);
#endif
        }
        public static Vector2 ReadDelta()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CardboardWebMouse_Read(delta)!=0?new Vector2(delta[0],delta[1]):Vector2.zero;
#else
            return Mouse.current!=null?Mouse.current.delta.ReadValue():Vector2.zero;
#endif
        }
    }
}