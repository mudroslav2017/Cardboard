using System;
using UnityEngine;

namespace Cardboard
{
    [Serializable]
    internal struct FactoryCoopHand
    {
        public bool HasPart, Dipping;
        public FactoryRecipe Recipe;

        public void Take(FactoryRecipe recipe)
        {
            HasPart = true;
            Recipe = recipe;
        }
    }

    // The station retains the same timing and interlocks in both modes.
    internal sealed class FactoryCoopPlayer
    {
        public int Role;
        public bool Active;
        public FactoryCoopHand Hand;
        public Vector3 Position;
        public Quaternion Look;
        public int Prop;
        public float Jump, RunTime, LastRunTime;
        public bool RunReported;
        public bool Left, Right;
        public int SewerAccess, Relocation;
        public FactoryCoopControls Controls;
        public float LastToolShot;
    }

    [Serializable]
    internal struct FactoryCoopPlayerState
    {
        public FactoryCoopHand Hand;
        public int Prop, SewerAccess, Relocation;
    }

    [Serializable]
    internal struct FactoryCoopAvatar
    {
        public bool Active;
        public Vector3 Position;
        public float Yaw, Jump;
    }

    [Serializable]
    internal struct FactoryStationState
    {
        public StationStage Stage;
        public StationPart Lever;
        public int SelectedType, CutType, OrderNumber, QueuedOrder, Bath, LeverOwner;
        public bool PressClosed, GuardClosed, LeverAnimating, Manipulating, Execution;
        public float Pressure, CutProgress, GuardBlend, PressBlend, LeverTravel, WheelAngle,
            OrderTravel, LandingPulse, Elapsed, LeverTime, ExecutionLever, ExecutionCrush;
        public Vector3 SheetPosition;
        public FactoryRecipe BathRecipe;
    }

    internal enum FactoryCoopAction
    {
        Interact, WheelStart, WheelTurn, WheelEnd, Drop, Throw, Draw, LeaveTool, ToolSpace, ToolReset, ToolPrimary, KitchenInput,
        SecretToolShot
    }

    [Flags]
    internal enum FactoryCoopControls
    {
        None = 0, Forward = 1, Back = 2, Left = 4, Right = 8, Q = 16, E = 32,
        Primary = 64, Secondary = 128, Precision = 256
    }

    [Serializable]
    internal struct FactoryCoopCommand
    {
        public FactoryCoopAction Action;
        public float Yaw, Pitch, Amount;
        public FactoryCoopStroke Stroke;
        public Vector2 PointerDelta;
        public FactoryCoopControls Buttons;
        public bool Pressed, Released, SecondaryPressed;
    }

    [Serializable]
    internal struct FactoryCoopStroke
    {
        public int Surface, Side, Color;
        public Vector2 From, To;
        public Vector3 Contact;
        public float Radius;
        public bool Erase;
    }

    [Serializable]
    internal struct FactoryCoopPartState
    {
        public int Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public FactoryRecipe Recipe;
    }

    [Serializable]
    internal struct FactoryCoopPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    internal sealed class FactoryCoopLunchState
    {
        public LunchState State;
        public int Carrier;
        public float CarryTime, Tilt, PitchTilt, Attendance;
        public bool Attended, Missed, Final, Visible, CookedService;
        public FactoryCoopPose Tray;
        public FactoryCoopPose[] Food;
    }

    [Serializable]
    internal struct FactoryCoopRat
    {
        public FactoryCoopPose Pose;
        public bool Visible, Available;
    }

    [Serializable]
    internal struct FactoryCoopObjectPose
    {
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public bool Active;

        public static FactoryCoopObjectPose Read(Transform item) => new FactoryCoopObjectPose
        {
            Position = item.position, Rotation = item.rotation, Active = item.gameObject.activeSelf, Scale = item.localScale
        };
        public void Apply(Transform item)
        {
            item.SetPositionAndRotation(Position, Rotation);
            item.localScale = Scale;
            if (item.gameObject.activeSelf != Active) item.gameObject.SetActive(Active);
        }

        public static FactoryCoopObjectPose ReadLocal(Transform item) => new FactoryCoopObjectPose
        {
            Position = item.localPosition, Rotation = item.localRotation, Active = item.gameObject.activeSelf, Scale = item.localScale
        };

        public void ApplyLocal(Transform item)
        {
            item.SetLocalPositionAndRotation(Position, Rotation);
            item.localScale = Scale;
            if (item.gameObject.activeSelf != Active) item.gameObject.SetActive(Active);
        }

        public bool Matches(FactoryCoopObjectPose other) => Active == other.Active &&
            (Position - other.Position).sqrMagnitude < .000001f && (Scale - other.Scale).sqrMagnitude < .000001f &&
            Mathf.Abs(Quaternion.Dot(Rotation, other.Rotation)) > .999999f;
    }

    [Serializable]
    internal struct FactoryCoopKitchenProp
    {
        public int Index;
        public bool WorldSpace;
        public FactoryCoopObjectPose Pose;
    }

    [Serializable]
    internal sealed class FactoryCoopCraneState
    {
        public int Owner, Relocation;
        public Vector3 OperatorPosition;
        public bool FixedView;
        public FactoryCoopPose View;
        public FactoryCoopObjectPose[] Parts;
        public string Status, Counter;
    }

    [Serializable]
    internal sealed class FactoryCoopProductionState
    {
        public int Owner, Work, Ride, PartStage, Cuts, Misses, Result;
        public float Belt, Spindle, Stroke;
        public bool HasPiece, FixedView;
        public Vector3 OperatorPosition;
        public FactoryCoopPose View, Piece, Rotor, Cutter, Quill;
    }

    [Serializable]
    internal struct FactoryCoopIngredient
    {
        public int Id;
        public float Browning;
        public bool Rat, Peeled, Visible, Burnt;
        public FactoryCoopObjectPose Pose;
    }

    [Serializable]
    internal sealed class FactoryCoopKitchenState
    {
        public int Owner, Stock, Portions, Filled, Plated, MeshId;
        public float Peel, RatMass, PotatoMass;
        public bool Preparing, Holding;
        public Vector2 PeelPointer;
        public bool PeelerPressed;
        public FactoryCoopPose View;
        public FactoryCoopKitchenProp[] Props;
        public FactoryCoopIngredient[] Pieces;
        public string MeshData, Hint;
    }

    [Serializable]
    internal sealed class FactoryCoopWorld
    {
        public FactoryStationState Station;
        public FactoryCoopPlayerState[] Players;
        public int Order, Repairs, WheelOwner, Round, StrokeStart;
        public float Mistakes, Remaining;
        public FactoryState State;
        public string Notice;
        public FactoryCoopPartState[] Parts;
        public FactoryCoopPose[] Workers;
        public FactoryCoopPose[] Props;
        public FactoryCoopStroke[] Strokes;
        public FactoryCoopLunchState Lunch;
        public CapturePhase Capture;
        public float CaptureTime;
        public FactoryCoopPose CaptureCamera;
        public bool EndingView, EndingGlow;
        public float EndingFade;
        public FactoryCoopPose EndingCamera;
        public int RatsCaught, RatStrikes;
        public bool RatsDelivered;
        public FactoryCoopRat[] Rats;
        public FactoryCoopCraneState Crane;
        public FactoryCoopProductionState Production;
        public FactoryCoopKitchenState Kitchen;
        public float[] Doors;
    }
}
