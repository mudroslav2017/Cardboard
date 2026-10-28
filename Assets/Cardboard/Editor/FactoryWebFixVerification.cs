using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryWebFixVerification
    {
        const string Pending="Cardboard.WebFixVerification";
        static FactoryWebFixVerification(){EditorApplication.update+=Tick;}
        public static void Batch()
        {
            EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);
            Apply();Run();
        }
        [MenuItem("Cardboard/Verify web build fixes")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            SessionState.SetBool(Pending,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static Color32[] Frame(Camera camera)
        {
            var target=new RenderTexture(640,400,24);var active=RenderTexture.active;
            var texture=new Texture2D(640,400,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,640,400),0,0);texture.Apply();return texture.GetPixels32();
            }
            finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            var game=CardboardFactoryGame.Instance;if(!game || !CardboardRunner.Player || Time.time<.3f)return;
            SessionState.SetBool(Pending,false);
            var report=new System.Collections.Generic.List<string>();var simulation=Physics.simulationMode;var pipeline=QualitySettings.renderPipeline;
            void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);}
            try
            {
                game.ResetShift();game.mainMenu.enabled=false;game.enabled=false;game.intro.enabled=false;
                Camera.main.GetComponent<CardboardCamera>().enabled=false;
                foreach(var worker in game.crowd.runners)worker.enabled=false;
                foreach(var station in game.stations)
                {
                    var motion=station.guardModel.GetComponent<FactoryGuardAnimation>();
                    Check(motion && motion.frames>30,"Baked guard animation: "+station.name);
                    motion.Evaluate(0);var start=station.guardModel.localRotation;var pos=station.guardModel.localPosition;
                    motion.Evaluate(1);
                    Check(Quaternion.Angle(start,station.guardModel.localRotation)>20 || Vector3.Distance(pos,station.guardModel.localPosition)>.2f,"Guard visibly moves between endpoints");
                    motion.Evaluate(0);
                }
                Physics.SyncTransforms();
                foreach(var bath in game.station.baths)
                {
                    var control=bath.GetComponentInChildren<FactoryStationControl>();int hits=0,total=0;
                    foreach(float x in new[]{-.32f,0,.32f})foreach(float y in new[]{.20f,.50f,.72f})
                    {
                        var point=bath.TransformPoint(new Vector3(x,y,0));var eye=bath.TransformPoint(new Vector3(x,1.4f,-1.8f));
                        var hit=game.TargetForRay(new Ray(eye,point-eye));total++;
                        if(hit && hit.GetComponent<FactoryStationControl>()==control)hits++;
                    }
                    Check(hits==total,"Bath "+control.number+" broad area "+hits+"/"+total);
                }
                game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));
                Check(game.station.Operate(StationPart.Bath,1),"Held unpainted gear enters selected bath");
                game.station.Tick(1.2f);Check(game.HasPart && !game.DippingPart && game.HeldRecipe.finish==1,"Dip returns a finished gear");
                game.ResetShift();
                var portal=game.transform.Find("Factory doorways/Canteen door");
                var lintel=portal.Cast<Transform>().First(t=>t.name=="Doorway infill").GetComponent<MeshFilter>().sharedMesh;
                Check(lintel.triangles.All(i=>Mathf.Abs(lintel.normals[i].x)<.9f),"Canteen lintel has no end faces coplanar with passage walls");
                Camera.main.transform.SetPositionAndRotation(new Vector3(-13,1.6f,-18),Quaternion.LookRotation(new Vector3(0,.5f,1)));
                CardboardFactorySceneBuilder.Capture("WebFixCanteen");
                var marker=CardboardMarker.Instance;var hand=marker.GetComponent<CardboardMarkerHand>();
                Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.identity);
                foreach(string profile in new[]{"Mobile","PC"})
                {
                    QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/"+profile+"_RPAsset.asset");marker.SetDrawingMode(true);
                    marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),false);hand.RefreshPose(.02f);
                    Frame(Camera.main);var visible=Frame(Camera.main);
                    if(profile=="Mobile")CardboardFactorySceneBuilder.Capture("WebFixMarker");
                    hand.viewCamera.enabled=false;var hidden=Frame(Camera.main);int changed=0;
                    for(int i=0;i<visible.Length;i++)if(Math.Abs(visible[i].r-hidden[i].r)+Math.Abs(visible[i].g-hidden[i].g)+Math.Abs(visible[i].b-hidden[i].b)>12)changed++;
                    Check(changed>200,"Marker renders with "+profile+" pipeline; pixels="+changed);
                }
                marker.SetDrawingMode(false,false);hand.RefreshPose(.02f);QualitySettings.renderPipeline=pipeline;
                game.ResetShift();game.ReceiveWorkbenchPart(game.Memorized.recipe);game.TryInstall(game.Memorized);game.lunch.TickSchedule(61);
                var player=CardboardRunner.Player;var controller=player.GetComponent<CharacterController>();controller.enabled=false;
                player.transform.position=game.lunch.pickup.position+new Vector3(0,-1,1.5f);
                Camera.main.transform.SetPositionAndRotation(player.transform.position+Vector3.up*1.62f,Quaternion.Euler(35,180,0));
                Check(game.lunch.TryTake(),"Physical tray can be collected");
                var tray=game.lunch.physicsTray;Physics.simulationMode=SimulationMode.Script;
                for(int i=0;i<150;i++){game.lunch.TickBalance(.02f,false,false);tray.TickSupport(.02f);Physics.Simulate(.02f);}
                Check(game.lunch.Carrying,"Meal survives three seconds at the counter");
                // Test a real 30-second walk with a 180-degree turn and vertical look changes.
                tray.ResetContents();Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.identity);tray.Begin(new Vector3(200,1.5f,200.72f),Quaternion.identity);
                for(int i=0;i<1500 && game.lunch.Carrying;i++)
                {
                    float t=i*.02f;Camera.main.transform.SetPositionAndRotation(new Vector3(200+t*.7f,2,200),Quaternion.Euler(35*Mathf.Sin(t*.4f),180*Mathf.SmoothStep(0,1,t/12),0));
                    game.lunch.TickBalance(.02f,game.lunch.Tilt>.15f && game.lunch.AngularSpeed>0,game.lunch.Tilt<-.15f && game.lunch.AngularSpeed<0);
                    tray.TickSupport(.02f);Physics.Simulate(.02f);
                }
                Check(game.lunch.Carrying && tray.contents.All(item=>(Quaternion.Inverse(tray.body.rotation)*(item.worldCenterOfMass-tray.body.position)).y>-.1f),"Meal survives 30-second walk, turn and vertical look");
                report.Add("DETAIL contents: "+string.Join("; ",tray.contents.Select(item=>item.name+" "+(Quaternion.Inverse(tray.body.rotation)*(item.worldCenterOfMass-tray.body.position)))));
                for(int i=0;i<750 && game.lunch.Carrying;i++)
                {game.lunch.TickBalance(.02f,false,true);tray.TickSupport(.02f);Physics.Simulate(.02f);}
                Check(game.lunch.State==LunchState.Spilled,"Severe tilt can still spill the meal");
                game.ResetShift();Check(tray.contents.All(item=>item.isKinematic && item.transform.parent==tray.transform),"Restart restores all dishes");
            }
            catch(Exception error){report.Add("FAIL "+error);}
            finally
            {
                Physics.simulationMode=simulation;QualitySettings.renderPipeline=pipeline;
                File.WriteAllLines("Library/FactoryWebFixVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));
                EditorApplication.isPlaying=false;
                if(Application.isBatchMode)EditorApplication.Exit(report.Any(l=>l.StartsWith("FAIL"))?1:0);
            }
        }
        public static void BakeGuard(FactoryWorkstation station)
        {
            var root=station.guardModel;
            if(!root || !station.guardClip)return;
            var motion=root.GetComponent<FactoryGuardAnimation>();
            if(!motion)motion=root.gameObject.AddComponent<FactoryGuardAnimation>();
            var paths=AnimationUtility.GetCurveBindings(station.guardClip).Where(b=>b.type==typeof(Transform)).Select(b=>b.path).Distinct().ToArray();
            motion.parts=paths.Select(p=>string.IsNullOrEmpty(p)?root:root.Find(p)).ToArray();
            motion.frames=Mathf.CeilToInt(station.guardClip.length*60)+1;
            int length=motion.parts.Length*motion.frames;
            motion.positions=new Vector3[length];motion.rotations=new Quaternion[length];motion.scales=new Vector3[length];
            for(int f=0;f<motion.frames;f++)
            {
                station.guardClip.SampleAnimation(root.gameObject,station.guardClip.length*f/(motion.frames-1));
                for(int p=0;p<motion.parts.Length;p++)
                {
                    int index=p*motion.frames+f;var part=motion.parts[p];
                    motion.positions[index]=part.localPosition;motion.rotations[index]=part.localRotation;motion.scales[index]=part.localScale;
                }
            }
            motion.Evaluate(0,true);EditorUtility.SetDirty(motion);
        }
        [MenuItem("Cardboard/Apply web build fixes")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode before applying web fixes.");
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
            foreach(var station in game.stations)
            {
                BakeGuard(station);
                foreach(var bath in station.baths)
                {
                    var control=bath.GetComponentInChildren<FactoryStationControl>();if(!control)continue;
                    var hit=control.GetComponent<BoxCollider>();
                    control.transform.localPosition=new Vector3(0,.43f,0);hit.center=Vector3.zero;
                    // Full vessel plus a small forgiving border, without bridging adjacent baths.
                    hit.size=new Vector3(.94f,.90f,1.48f);hit.isTrigger=true;
                    EditorUtility.SetDirty(control.transform);EditorUtility.SetDirty(hit);
                }
            }
            var hand=Camera.main.GetComponent<CardboardMarkerHand>();
            hand.paperTemplate.SetFloat("_ReceiveShadows",0);hand.paperTemplate.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            EditorUtility.SetDirty(hand.paperTemplate);
            FixTray(game.lunch.physicsTray);
            FixWalls(game);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            Audit();
        }
        static void FixTray(FactoryTrayPhysics tray)
        {
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Cardboard/Generated/LunchContact.physicMaterial");
            material.staticFriction=.65f;material.dynamicFriction=.5f;material.bounciness=0;
            material.frictionCombine=PhysicsMaterialCombine.Average;EditorUtility.SetDirty(material);
            foreach(var item in tray.contents)
            {
                foreach(var mesh in item.GetComponentsInChildren<MeshCollider>())
                {
                    // The Apple visual was replaced without resetting its old collider transform.
                    var visual=mesh.GetComponent<MeshFilter>();
                    if(visual)mesh.sharedMesh=visual.sharedMesh;
                    mesh.convex=true;mesh.sharedMaterial=material;EditorUtility.SetDirty(mesh);
                }
                item.ResetCenterOfMass();item.ResetInertiaTensor();
                item.angularDamping=.8f;item.solverIterations=16;item.solverVelocityIterations=8;
                EditorUtility.SetDirty(item);
            }
            Physics.SyncTransforms();
            // Re-seat each collider from its actual bounds: plate on tray, apple on plate.
            float support=tray.transform.Find("Physical tray support/Tray base").GetComponent<Collider>().bounds.max.y;
            foreach(var item in tray.contents)
            {
                var shapes=item.GetComponentsInChildren<Collider>();
                if(item.name=="Lunch apple")
                {
                    var center=shapes[0].bounds.center;
                    var plateCenter=tray.contents[0].GetComponentInChildren<Collider>().bounds.center;
                    item.transform.position+=new Vector3(plateCenter.x-center.x,0,plateCenter.z-center.z);
                    Physics.SyncTransforms();
                }
                float bottom=shapes.Min(c=>c.bounds.min.y);
                float floor=item.name=="Lunch apple"?tray.contents[0].GetComponentsInChildren<Collider>().Max(c=>c.bounds.max.y):support;
                item.transform.position+=Vector3.up*(floor+.004f-bottom);
                Physics.SyncTransforms();EditorUtility.SetDirty(item.transform);
            }
            tray.homes=tray.contents.Select(item=>item.transform.localPosition).ToArray();
            tray.rotations=tray.contents.Select(item=>item.transform.localRotation).ToArray();EditorUtility.SetDirty(tray);
        }
        static void FixWalls(CardboardFactoryGame game)
        {
            // A lintel's end faces exactly coincide with the passage's inside faces.
            // Those hidden end caps must not be rendered from either side.
            var portal=game.transform.Find("Factory doorways/Canteen door");
            var infill=portal.Cast<Transform>().First(t=>t.name=="Doorway infill");
            var source=infill.GetComponent<MeshFilter>().sharedMesh;
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Canteen lintel without internal end caps";
            var normals=mesh.normals;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var triangles=mesh.GetTriangles(s);var kept=new System.Collections.Generic.List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                    if(Mathf.Abs(normals[triangles[i]].x)<.9f)kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                mesh.SetTriangles(kept,s);
            }
            const string path="Assets/Cardboard/Generated/CanteenLintel.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            infill.GetComponent<MeshFilter>().sharedMesh=saved;EditorUtility.SetDirty(infill.GetComponent<MeshFilter>());
        }
        public static void Audit()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
            var report=new StringBuilder();
            var hand=Camera.main.GetComponent<CardboardMarkerHand>();
            report.AppendLine("Hand shader: "+hand.paperTemplate.shader.name+" keywords="+string.Join(",",hand.paperTemplate.shaderKeywords));
            foreach(var station in game.stations)
            {
                report.AppendLine("STATION "+station.name+" clip="+station.guardClip.name+" length="+station.guardClip.length+" legacy="+station.guardClip.legacy);
                foreach(var binding in AnimationUtility.GetCurveBindings(station.guardClip))
                    report.AppendLine("CURVE "+binding.path+" / "+binding.propertyName+" exists="+(station.guardModel.Find(binding.path)!=null));
            }
            foreach(var control in game.station.GetComponentsInChildren<FactoryStationControl>())
                if(control.part==StationPart.Bath)report.AppendLine("BATH "+control.number+" local="+control.transform.localPosition+" size="+control.GetComponent<BoxCollider>().size);
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>())
                if(renderer.name.Contains("wall") || renderer.name=="Doorway infill")report.AppendLine("WALL "+renderer.name+" center="+renderer.bounds.center+" size="+renderer.bounds.size);
            var tray=game.lunch.physicsTray;
            report.AppendLine("TRAY halfSize="+tray.halfSize);
            foreach(var item in tray.contents)report.AppendLine("ITEM "+item.name+" local="+item.transform.localPosition+" center="+item.centerOfMass+" interpolation="+item.interpolation+" collider="+item.GetComponentInChildren<Collider>().bounds);
            File.WriteAllText("Library/FactoryWebFixAudit.txt",report.ToString());
        }
    }
}
