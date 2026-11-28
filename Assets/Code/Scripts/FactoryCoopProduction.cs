#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using UnityEngine;

namespace Cardboard
{
    internal sealed partial class FactoryCoopShift
    {
        int productionOwner;
        bool localProductionWasSeated;
        Transform productionLens;
        Vector3 productionOperatorPosition;

        void SetUpProduction(bool host)
        {
            if (!game.production) return;
            game.production.ConfigureCoop(!host);
            world.Production = new FactoryCoopProductionState();
            if (host)
            {
                productionLens = new GameObject("Co-op production grip").transform;
                productionLens.SetParent(game.transform);
            }
        }

        CardboardRunner BindProduction(int role, FactoryCoopPlayer actor)
        {
            var runner = RunnerFor(role);
            if (role != Host && !game.production.HoldsPlayer)
                runner.transform.SetPositionAndRotation(actor.Position, Quaternion.Euler(0, actor.Look.eulerAngles.y, 0));
            productionLens.SetPositionAndRotation(actor.Position + Vector3.up * (1.62f + actor.Jump), actor.Look);
            game.production.BindCoopOperator(runner, productionLens, actor);
            return runner;
        }

        void UseProduction(int role, FactoryCoopPlayer actor, FactoryInteractable target)
        {
            if (!game.production || role == craneOwner || (productionOwner != 0 && productionOwner != role)) return;
            var runner = BindProduction(role, actor);
            if (!game.production.Use(target, runner)) return;
            productionOperatorPosition = runner.transform.position;
            productionOwner = game.production.HoldsPlayer || game.production.Carrying ? role : 0;
        }

        void StepProduction(float dt)
        {
            if (!game.production) return;
            if (productionOwner == 0 && game.State == FactoryState.Working)
            {
                for (int role = Host; role <= MaximumPlayers; role++)
                {
                    var actor = Player(role);
                    if (!actor.Active || role == craneOwner || HandsBusy(actor) || Underground(actor)) continue;
                    if (!game.craneJob || !game.craneJob.InHall(actor.Position)) continue;
                    var runner = BindProduction(role, actor);
                    if (!game.production.TryRideCoop(runner)) continue;
                    productionOwner = role;
                    break;
                }
            }
            var rider = productionOwner != 0 ? BindProduction(productionOwner, Player(productionOwner)) : null;
            game.production.TickCoop(dt);
            if (!rider) return;
            productionOperatorPosition = rider.transform.position;
            if (!game.production.HoldsPlayer && !game.production.Carrying)
            {
                ReleaseProxy(productionOwner);
                productionOwner = 0;
            }
        }

        void ReadProduction()
        {
            if (!game.production) return;
            world.Production.Owner = productionOwner;
            world.Production.OperatorPosition = productionOperatorPosition;
            game.production.ReadCoopState(world.Production);
        }

        bool ApplyProduction(FactoryCoopProductionState state)
        {
            if (!game.production) return state == null;
            if (state == null || state.Owner < 0 || state.Owner > MaximumPlayers || !game.production.ApplyCoopState(state))
                return false;
            productionOwner = state.Owner;
            var local = CardboardRunner.Player;
            bool seated = LocalUsesProduction && state.FixedView;
            if (seated)
            {
                if (!localProductionWasSeated) local.PlaceAtWorkAnchor(state.OperatorPosition, false);
                else local.transform.position = state.OperatorPosition;
                game.CoopToolView = true;
                game.CoopToolCamera = state.View;
            }
            else if (localProductionWasSeated) local.PlaceAtWorkAnchor(state.OperatorPosition, true);
            localProductionWasSeated = seated;
            return true;
        }
    }
}
#endif
