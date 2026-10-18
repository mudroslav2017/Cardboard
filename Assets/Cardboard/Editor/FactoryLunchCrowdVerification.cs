using System;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryLunchCrowdVerification
    {
        static int stage,frame;
        static Vector3 playerHome;
        static Quaternion cameraHome;
        static FactoryLunchCrowd crowd;
        public static void Reset(){stage=0;frame=-1;}
        static void MovePlayer(Vector3 position)
        {
            var body=CardboardRunner.Player.GetComponent<CharacterController>();body.enabled=false;
            CardboardRunner.Player.transform.position=position;body.enabled=true;
            Camera.main.transform.position=position+Vector3.up*1.62f;Physics.SyncTransforms();
        }
        static void Simulate(float seconds)
        {
            for(int step=0;step<Mathf.CeilToInt(seconds/.05f);step++)
            {
                crowd.Tick(.05f);
                foreach(var worker in crowd.workers) worker.TickMovement(.05f);
                Physics.SyncTransforms();
            }
        }
        public static bool Tick(CardboardFactoryGame game,Action<bool,string> check)
        {
            if(frame==Time.frameCount)return false;frame=Time.frameCount;
            if(stage==0)
            {
                crowd=game.lunch.diners;check(crowd,"Shared lunch crowd exists");if(!crowd)return true;
                playerHome=CardboardRunner.Player.transform.position;cameraHome=Camera.main.transform.rotation;
                game.ResetShift();MovePlayer(new Vector3(-7,0,-22));
                var floor=GameObject.Find("Canteen floor").GetComponent<Renderer>().bounds;
                check(Mathf.Abs(floor.size.x-26)<.01f && Mathf.Abs(floor.size.z-18)<.01f,"Canteen expands from 22 by 11 to 26 by 18 metres");
                check(Physics.Raycast(new Vector3(-13,1,-32),Vector3.down,2,1<<8),"Expanded rear of canteen has a solid floor");
                game.lunch.TickSchedule(60);
                check(crowd.Active && crowd.workers.Length==11 && crowd.places.Length==11,"All eleven coworkers join lunch at the same bell");
                var navigation=new FactoryCaptureNavigation(.38f);
                for(int i=0;i<crowd.workers.Length;i++)
                {
                    var path=navigation.Path(crowd.workers[i].transform.position,crowd.places[i].position);
                    check(path.Count>0 && Vector3.Distance(path[path.Count-1],crowd.places[i].position)<.5f,"Worker has a reachable unique lunch place: "+(i+1));
                    for(int j=0;j<i;j++) check(Vector3.Distance(crowd.places[i].position,crowd.places[j].position)>1.2f,"Lunch places keep coworkers apart: "+i+" / "+j);
                }
                var carryPath=new FactoryCaptureNavigation(.78f).Path(new Vector3(-13,0,-32),game.station.transform.TransformPoint(new Vector3(0,0,-1.8f)));
                check(carryPath.Count>3,"Capture route reaches the workshop from the expanded canteen");
                Simulate(80);
                check(crowd.EatingCount==11,"All eleven workers physically reach their tables and start eating");
                for(int i=0;i<crowd.workers.Length;i++)
                    check(crowd.IsEating(crowd.workers[i]) && crowd.meals[i].gameObject.activeSelf,"Worker eats with a visible tray and food: "+(i+1)+" at "+crowd.workers[i].transform.position+" "+crowd.Describe(crowd.workers[i]));
                check(!crowd.Returning,"Workers stay at lunch while player has not finished");
                Vector3 bite=crowd.bites[0].position;bool biteMoved=false;
                for(int i=0;i<30;i++){crowd.Tick(.1f);crowd.PoseDiner(crowd.workers[0]);biteMoved |= Vector3.Distance(bite,crowd.bites[0].position)>.05f;}
                check(biteMoved,"Dining animates food between tray and worker's mouth");
                stage=1;return false;
            }
            if(stage==1)
            {
                var camera=Camera.main.transform;
                camera.position=new Vector3(-13,8,-18);camera.rotation=Quaternion.LookRotation(new Vector3(-13,0,-28)-camera.position);
                CardboardFactorySceneBuilder.Capture("SharedCanteenLunch");
                MovePlayer(new Vector3(game.lunch.pickup.position.x,0,game.lunch.pickup.position.z+1.5f));
                check(game.lunch.TryTake(),"Player can take a tray while all coworkers are dining");
                MovePlayer(new Vector3(game.lunch.destination.position.x,0,game.lunch.destination.position.z+1.6f));
                check(game.lunch.TryServe(),"Player can finish lunch at a shared table with coworkers");
                Simulate(70);
                check(!crowd.Active && crowd.EatingCount==0,"Workers finish lunch and return to their work routes");
                bool clear=true;foreach(var meal in crowd.meals)clear &= !meal.gameObject.activeSelf;
                check(clear,"Coworker food is cleared after lunch");
                foreach(var worker in crowd.workers)check(worker.transform.position.z>-14 && !crowd.Guides(worker),"Coworker returns through the doorway into workshop: "+worker.personality+" at "+worker.transform.position+" "+crowd.Describe(worker));
                game.ResetShift();game.lunch.TickSchedule(60);game.Capture(FactoryDeath.Press);
                check(!crowd.Active && crowd.EatingCount==0,"Chase immediately overrides coworker lunch");
                game.WorkerTarget(crowd.workers[0],out float speed);check(speed>6,"Dining coworkers join pursuit when player reaches three stars");
                game.ResetShift();check(!crowd.Active && crowd.Elapsed==0,"New shift resets coworker lunch and restores their positions");
                MovePlayer(playerHome);camera.rotation=cameraHome;stage=2;return true;
            }
            return true;
        }
    }
}
