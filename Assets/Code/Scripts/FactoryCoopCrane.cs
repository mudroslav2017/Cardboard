#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using UnityEngine;

namespace Cardboard
{
    internal sealed partial class FactoryCoopShift
    {
        Transform[] craneParts;
        readonly CardboardRunner[] guestRunners = new CardboardRunner[MaximumPlayers];
        int craneOwner;
        bool localCraneWasActive;
        Vector3 craneOperatorPosition;

        void SetUpCrane(bool host)
        {
            if (!game.craneJob) return;
            craneParts = game.craneJob.PrepareCoopReplication(!host);
            world.Crane = new FactoryCoopCraneState { Parts = new FactoryCoopObjectPose[craneParts.Length] };
            if (!host) return;
            for (int role = FirstGuest; role <= MaximumPlayers; role++)
            {
                var proxy = new GameObject("Guest machinery operator " + role);
                proxy.transform.SetParent(game.transform);
                proxy.AddComponent<CharacterController>().enabled = false;
                var runner = proxy.AddComponent<CardboardRunner>();
                runner.isPlayer = true;
                runner.enabled = false;
                guestRunners[role - 1] = runner;
            }
        }

        CardboardRunner RunnerFor(int role) => role == Host ? CardboardRunner.Player : guestRunners[role - 1];

        void ReleaseProxy(int role)
        {
            if (role != Host) RunnerFor(role).GetComponent<CharacterController>().enabled = false;
        }

        void ResetCrane()
        {
            if (!game.craneJob) return;
            craneOwner = 0;
            localCraneWasActive = false;
            game.CoopToolView = false;
            if (localRole != Host) game.craneJob.PrepareCoopReplication(true);
        }

        void UseCrane(int role, FactoryCoopPlayer actor, CraneAccess access)
        {
            if (craneOwner != 0 || !game.craneJob) return;
            var runner = RunnerFor(role);
            if (role != Host)
                runner.transform.SetPositionAndRotation(actor.Position, Quaternion.Euler(0, actor.Look.eulerAngles.y, 0));
            if (!game.craneJob.Use(runner, access)) return;
            craneOwner = role;
            craneOperatorPosition = runner.transform.position;
        }

        void StepCrane(float dt)
        {
            if (craneOwner == 0 || !game.craneJob) return;
            var runner = RunnerFor(craneOwner);
            game.craneJob.TickCoop(Player(craneOwner).Controls, dt);
            craneOperatorPosition = runner.transform.position;
            if (!game.craneJob.HoldsPlayer)
            {
                ReleaseProxy(craneOwner);
                craneOwner = 0;
            }
        }

        void ReadCrane()
        {
            if (!game.craneJob) return;
            var state = world.Crane;
            state.Owner = craneOwner;
            state.OperatorPosition = craneOperatorPosition;
            state.FixedView = game.craneJob.TryGetHatchView(out var eyes, out var look);
            state.View = new FactoryCoopPose { Position = eyes, Rotation = look };
            for (int i = 0; i < craneParts.Length; i++)
                state.Parts[i] = FactoryCoopObjectPose.Read(craneParts[i]);
            game.craneJob.ReadCoopBoard(state);
        }

        bool ApplyCrane(FactoryCoopCraneState state)
        {
            if (!game.craneJob) return state == null;
            if (state == null || state.Parts?.Length != craneParts.Length || state.Owner < 0 || state.Owner > MaximumPlayers)
                return false;
            foreach (var pose in state.Parts)
                if (pose.Position.sqrMagnitude > 50000) return false;
            for (int i = 0; i < craneParts.Length; i++)
                state.Parts[i].Apply(craneParts[i]);
            game.craneJob.ApplyCoopBoard(state);
            craneOwner = state.Owner;
            var local = CardboardRunner.Player;
            if (LocalUsesCrane)
            {
                if (!localCraneWasActive) local.PlaceAtWorkAnchor(state.OperatorPosition, false);
                else local.transform.position = state.OperatorPosition;
            }
            else if (localCraneWasActive) local.PlaceAtWorkAnchor(state.OperatorPosition, true);
            game.CoopToolCamera = state.View;
            game.CoopToolView = LocalUsesCrane && state.FixedView;
            localCraneWasActive = LocalUsesCrane;
            return true;
        }
    }
}
#endif
