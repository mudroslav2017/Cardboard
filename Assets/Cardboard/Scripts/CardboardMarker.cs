using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera),typeof(CardboardMarkerHand))]
    public class CardboardMarker : MonoBehaviour
    {
        public static CardboardMarker Instance {get;private set;}
        public static bool DrawingMode => Instance && Instance.drawing;
        public readonly Color[] Colors={CardboardArt.Hex("252A31"),CardboardArt.Hex("CF354D"),CardboardArt.Hex("2566C4"),CardboardArt.Hex("20804B"),CardboardArt.Hex("963FA6")};
        public int colorIndex;
        public float radius=.018f;
        public float reach=1.85f;
        public CardboardDrawingSurface Target {get;private set;}
        public Vector2 Pointer {get;private set;}
        public Vector3 ContactPoint {get;private set;}
        public Vector3 ContactNormal {get;private set;}
        public bool IsTouching {get;private set;}
        public bool IsPressed {get;private set;}
        public Color Ink => Colors[colorIndex];
        bool drawing,dragging;
        int previousSide;
        CardboardDrawingSurface previous;
        Vector2 lastUV;
        Camera lens;
        void Awake() { Instance=this;lens=GetComponent<Camera>();Pointer=new Vector2(Screen.width*.5f,Screen.height*.5f); }

        void Update()
        {
            if(CardboardFactoryGame.Instance && CardboardFactoryGame.Instance.PlayerBlocked)
            { if(drawing) SetDrawingMode(false,false);return; }
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard==null) return;
            if(keyboard.fKey.wasPressedThisFrame) SetDrawingMode(!drawing);
            if(keyboard.escapeKey.wasPressedThisFrame && drawing) SetDrawingMode(false,false);
            if(!drawing) return;
            if(keyboard.digit1Key.wasPressedThisFrame) colorIndex=0;
            if(keyboard.digit2Key.wasPressedThisFrame) colorIndex=1;
            if(keyboard.digit3Key.wasPressedThisFrame) colorIndex=2;
            if(keyboard.digit4Key.wasPressedThisFrame) colorIndex=3;
            if(keyboard.digit5Key.wasPressedThisFrame) colorIndex=4;
            if(mouse!=null) radius=Mathf.Clamp(radius+mouse.scroll.ReadValue().y*.000025f,.006f,.045f);
        }
        void LateUpdate()
        {
            var mouse=Mouse.current;
            if(!drawing || mouse==null || !Application.isFocused)
            { Target=null;IsTouching=false;IsPressed=false;BreakStroke();return; }
            TracePointer(mouse.position.ReadValue(),mouse.leftButton.isPressed);
            if(Keyboard.current!=null && Keyboard.current.cKey.wasPressedThisFrame && Target)
            { Target.ClearDrawing();BreakStroke(); }
        }
        // Shared by the input path and integration checks: ink and the visible nib use this same contact.
        public void TracePointer(Vector2 pointer,bool pressed)
        {
            Pointer=pointer;IsPressed=pressed;IsTouching=false;Target=null;
            if(!drawing || pointer.x<0 || pointer.y<0 || pointer.x>Screen.width || pointer.y>Screen.height)
            { BreakStroke();return; }
            Physics.SyncTransforms();
            Ray ray=lens.ScreenPointToRay(pointer);
            if(Physics.Raycast(ray,out RaycastHit hit,reach,~(1<<9),QueryTriggerInteraction.Ignore))
            {
                var surface=hit.collider.GetComponent<CardboardDrawingSurface>();
                if(surface && Mathf.Abs(surface.transform.InverseTransformDirection(hit.normal).z)>.9f)
                {
                    Target=surface;ContactPoint=hit.point;ContactNormal=hit.normal;
                    if(surface.Runner) surface.Runner.HoldForDrawing();
                    if(pressed)
                    {
                        int side=CardboardDrawingSurface.SideOf(hit);
                        Vector2 uv=hit.textureCoord;
                        surface.PaintStroke(dragging && previous==surface && previousSide==side?lastUV:uv,uv,Ink,radius,side);
                        previous=surface;previousSide=side;lastUV=uv;dragging=true;IsTouching=true;return;
                    }
                }
            }
            BreakStroke();
        }
        void BreakStroke() { previous=null;dragging=false; }
        public void SetDrawingMode(bool active,bool captureOnExit=true)
        {
            drawing=active;Target=null;IsTouching=false;IsPressed=false;BreakStroke();
            Cursor.lockState=active || !captureOnExit?CursorLockMode.None:CursorLockMode.Locked;
            Cursor.visible=active || !captureOnExit;
        }
        void OnApplicationFocus(bool focused) { if(!focused && drawing) SetDrawingMode(false,false); }
        void OnDisable()
        {
            drawing=false;Target=null;IsTouching=false;BreakStroke();
            if(Instance==this) Instance=null;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
