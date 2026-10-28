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
        public bool Erasing {get;private set;}
        public float wipeRadius=.09f;
        public Color Ink => Colors[colorIndex];
        bool drawing,dragging;
        int previousSide;
        CardboardDrawingSurface previous;
        Vector2 lastUV;
        Camera lens;
        FactoryMarkerSound writingSound;
        void Awake() { Instance=this;lens=GetComponent<Camera>();writingSound=GetComponent<FactoryMarkerSound>();Pointer=new Vector2(Screen.width*.5f,Screen.height*.5f); }

        void Update()
        {
            if(CardboardFactoryGame.Instance && (CardboardFactoryGame.Instance.PlayerBlocked || CardboardFactoryGame.Instance.HandsBusy))
            { if(drawing) SetDrawingMode(false,false);return; }
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard==null) return;
            if(keyboard.fKey.wasPressedThisFrame) SetDrawingMode(!drawing);
            if(keyboard.escapeKey.wasPressedThisFrame && drawing) SetDrawingMode(false,false);
            if(!drawing) return;
            if(keyboard.cKey.wasPressedThisFrame)SetErasing(!Erasing);
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
        }
        public void SetErasing(bool active)
        {
            Erasing=drawing && active;IsTouching=false;IsPressed=false;BreakStroke();
        }
        // Shared by the input path and integration checks: ink and the visible nib use this same contact.
        public void TracePointer(Vector2 pointer,bool pressed)
        {
            Pointer=pointer;IsPressed=pressed;IsTouching=false;Target=null;
            if(!drawing || pointer.x<0 || pointer.y<0 || pointer.x>Screen.width || pointer.y>Screen.height)
            { BreakStroke();return; }
            Physics.SyncTransforms();
            Ray ray=lens.ScreenPointToRay(pointer);
            if(CardboardDrawingSurface.RaycastForDrawing(ray,reach,out RaycastHit hit))
            {
                var surface=hit.collider.GetComponent<CardboardDrawingSurface>();
                if(surface && Mathf.Abs(surface.transform.InverseTransformDirection(hit.normal).z)>.9f)
                {
                    Target=surface;ContactPoint=hit.point;
                    ContactNormal=Vector3.Dot(hit.normal,ray.direction)>0?-hit.normal:hit.normal;
                    if(surface.Runner) surface.Runner.HoldForDrawing();
                    if(pressed)
                    {
                        int side=surface.transparentInk?0:CardboardDrawingSurface.SideOf(hit);
                        Vector2 uv=hit.textureCoord;
                        var from=dragging && previous==surface && previousSide==side?lastUV:uv;
                        if(Erasing)surface.EraseStroke(from,uv,wipeRadius,side);else surface.PaintStroke(from,uv,Ink,radius,side);
                        previous=surface;previousSide=side;lastUV=uv;dragging=true;IsTouching=true;
                        if(writingSound){if(Erasing)writingSound.EndStroke();else writingSound.Stroke(surface,ContactPoint);}return;
                    }
                }
            }
            BreakStroke();
        }
        void BreakStroke() { previous=null;dragging=false;if(writingSound)writingSound.EndStroke(); }
        public void SetDrawingMode(bool active,bool captureOnExit=true)
        {
            if(active && CardboardFactoryGame.Instance && CardboardFactoryGame.Instance.HandsBusy) return;
            drawing=active;Erasing=false;Target=null;IsTouching=false;IsPressed=false;BreakStroke();
            Cursor.lockState=active || !captureOnExit?CursorLockMode.None:CursorLockMode.Locked;
            FactoryVolumeCursor.SetSystemVisible(active || !captureOnExit);
        }
        void OnApplicationFocus(bool focused) { if(!focused && drawing) SetDrawingMode(false,false); }
        void OnDisable()
        {
            drawing=Erasing=false;Target=null;IsTouching=false;BreakStroke();
            if(Instance==this) Instance=null;
            Cursor.lockState=CursorLockMode.None;FactoryVolumeCursor.SetSystemVisible(true);
        }
    }
}
