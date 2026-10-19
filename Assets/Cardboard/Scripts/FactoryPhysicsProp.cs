using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(Rigidbody))]
    public class FactoryPhysicsProp : MonoBehaviour
    {
        public string itemName="предмет";
        public bool instructionSheet,workbenchPart;
        public FactoryRecipe recipe;
        public Vector3 homePosition;
        public Quaternion homeRotation=Quaternion.identity;
        public bool homeStored;
        public Rigidbody Body {get;private set;}
        public bool IsHeld {get;internal set;}
        public bool ThrowArmed => Time.time<throwUntil;
        readonly HashSet<CardboardRunner> hitWorkers=new HashSet<CardboardRunner>();
        Collider[] shapes,playerShapes;
        float throwUntil,restoreCollisionAt;
        bool scored,ignoringPlayer;

        void Awake(){Initialize();if(!homeStored)RememberHome();}
        public void Initialize()
        {
            Body=GetComponent<Rigidbody>();shapes=GetComponentsInChildren<Collider>();
            Body.useGravity=true;Body.isKinematic=false;
            Body.interpolation=RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping=.12f;Body.angularDamping=.22f;
            Body.maxAngularVelocity=14;Body.solverIterations=12;Body.solverVelocityIterations=6;
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetFloat("_Cull",0);renderer.SetPropertyBlock(properties);
            }
        }
        public void RememberHome()
        {homePosition=transform.position;homeRotation=transform.rotation;homeStored=true;}
        public void ResetHome()
        {
            if(!Body)Initialize();
            IsHeld=false;throwUntil=0;hitWorkers.Clear();RestorePlayerCollision();
            Body.useGravity=true;Body.linearVelocity=Body.angularVelocity=Vector3.zero;
            Body.position=homePosition;Body.rotation=homeRotation;Body.WakeUp();
        }
        public bool Take(CardboardFactoryGame game)
        {
            if(!game || game.HandsBusy || game.HasPart || game.PlayerBlocked || game.CaptureActive || game.Ended)return false;
            if(workbenchPart)
            {game.ReceiveWorkbenchPart(recipe);gameObject.SetActive(false);Destroy(gameObject);return true;}
            return game.objectCarry && game.objectCarry.Take(this);
        }
        internal void BeginCarry()
        {
            if(!Body)Initialize();
            IsHeld=true;throwUntil=0;hitWorkers.Clear();Body.useGravity=false;Body.WakeUp();
            IgnorePlayer();
        }
        internal void Release(Vector3 velocity,bool thrown)
        {
            IsHeld=false;Body.useGravity=true;Body.linearVelocity=velocity;
            if(thrown)
            {
                throwUntil=Time.time+6;scored=false;hitWorkers.Clear();
                Body.angularVelocity=transform.right*(instructionSheet?5:2.4f)+Vector3.up*1.3f;
            }
            else {throwUntil=0;Body.angularVelocity=Vector3.ClampMagnitude(Body.angularVelocity,1);}
            restoreCollisionAt=Time.time+.35f;Body.WakeUp();
        }
        void IgnorePlayer()
        {
            RestorePlayerCollision();
            if(!CardboardRunner.Player)return;
            playerShapes=CardboardRunner.Player.GetComponentsInChildren<Collider>(true);
            foreach(var shape in shapes)foreach(var playerShape in playerShapes)
                if(shape && playerShape)Physics.IgnoreCollision(shape,playerShape,true);
            ignoringPlayer=true;restoreCollisionAt=float.PositiveInfinity;
        }
        void RestorePlayerCollision()
        {
            if(!ignoringPlayer)return;
            foreach(var shape in shapes)foreach(var playerShape in playerShapes)
                if(shape && playerShape)Physics.IgnoreCollision(shape,playerShape,false);
            ignoringPlayer=false;playerShapes=null;
        }
        void FixedUpdate()
        {
            if(!ignoringPlayer || IsHeld || Time.time<restoreCollisionAt)return;
            // Keep collision disabled while the released object still intersects the player's capsule.
            foreach(var shape in shapes)foreach(var playerShape in playerShapes)
                if(shape && playerShape && shape.bounds.Intersects(playerShape.bounds) &&
                    Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,
                        playerShape,playerShape.transform.position,playerShape.transform.rotation,out _,out _))return;
            RestorePlayerCollision();
        }
        void OnCollisionEnter(Collision collision)
        {
            if(IsHeld || !ThrowArmed || collision.relativeVelocity.magnitude<1.5f)return;
            var worker=collision.collider.GetComponentInParent<CardboardRunner>();
            if(!worker || worker.isPlayer || !hitWorkers.Add(worker))return;
            var reaction=worker.GetComponent<FactoryWorkerImpact>();
            Vector3 incoming=collision.relativeVelocity;
            Vector3 direction=collision.contactCount>0?-collision.GetContact(0).normal:worker.transform.position-transform.position;
            direction.y=0;if(direction.sqrMagnitude<.001f)direction=transform.forward;
            if(reaction)reaction.ReceiveImpact(direction.normalized,Mathf.Clamp(incoming.magnitude*.38f,.6f,3));
            var game=CardboardFactoryGame.Instance;
            if(!scored && game){scored=true;game.RegisterMistake("Брошенный предмет попал в рабочего.",.5f,worker,FactoryWorkerEmotion.Thrown);}
        }
        void OnDisable(){RestorePlayerCollision();}
    }
}
