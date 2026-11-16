using UnityEngine;
namespace Cardboard
{
    public partial class FactoryPaperDrama
    {
        void ApplyStory()
        {
            float t=Elapsed;bool inside=t<RoofAt;
            wife.root.gameObject.SetActive(inside);boss.root.gameObject.SetActive(inside);room.gameObject.SetActive(inside);bouquet.gameObject.SetActive(inside);
            door.localRotation=Quaternion.Euler(0,108*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.35f,t)),0);
            var h=roomOrigin+new Vector3(.85f,0,Mathf.Lerp(6.35f,2.8f,Mathf.SmoothStep(0,1,t/2.2f)));
            // The two printed faces meet physically; neither character mesh is deformed.
            float kissTime=Mathf.Min(t,SplitEnd),rock=Mathf.Sin(kissTime*4.2f)*16,lean=9+Mathf.Sin(kissTime*4.2f)*3,roll=Mathf.Cos(kissTime*4.2f)*7;
            PersonPose(wife,roomOrigin+new Vector3(.05f,0,-.65f),Quaternion.Euler(0,68+rock,0)*Quaternion.Euler(lean,0,roll),true,ground);
            PersonPose(boss,roomOrigin+new Vector3(.85f,0,-.65f),Quaternion.Euler(0,-68+rock,0)*Quaternion.Euler(lean,0,-roll),true,ground);
            boss.root.position+=wife.Contact(true)-boss.Contact(false)+wife.root.forward*.004f;
            var kissingPosition=boss.root.position;var kissingRotation=boss.root.rotation;
            float stand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(BossTalkAt,BossTalkAt+.65f,t));
            var standingPosition=roomOrigin+new Vector3(1.3f,0,.02f);var toWife=wife.root.position-standingPosition;toWife.y=0;
            if(stand>0)PersonPose(boss,Vector3.Lerp(kissingPosition,standingPosition,stand),Quaternion.Slerp(kissingRotation,Quaternion.LookRotation(toWife),stand),true,ground);
            hero.face.SetState(t>=ShockAt && t<BossTalkAt?FactoryFaceState.Surprised:FactoryFaceState.Normal);
            if(inside)
            {
                float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.65f,2.8f,t)),step=Mathf.Sin((t-2.2f)*3.6f);
                float rocking=Mathf.Lerp(Mathf.Sin(t*12)*2,step*2.8f,settle);
                PersonPose(hero,h+Vector3.right*(step*.025f*settle),Quaternion.Euler(0,180,rocking),true,ground);
                hero.root.position+=Vector3.up*(.024f*(.5f-.5f*Mathf.Cos((t-2.2f)*7.2f))*settle);
                var hand=hero.root.TransformPoint(new Vector3(.42f,.83f,.18f));var held=hero.root.rotation*Quaternion.Euler(0,0,-20);
                if(t<ShockAt)bouquet.SetPositionAndRotation(hand,held);
                else
                {
                    float drop=Mathf.Clamp01((t-ShockAt)/.55f);var down=Quaternion.Euler(-90,0,-35);bouquet.rotation=down;
                    var end=hero.root.position+new Vector3(-.5f,0,-.4f);bouquet.position=end;
                    float min=float.PositiveInfinity;foreach(var filter in bouquet.GetComponentsInChildren<MeshFilter>())foreach(var v in filter.sharedMesh.vertices)min=Mathf.Min(min,filter.transform.TransformPoint(v).y);
                    end.y+=ground+.002f-min;bouquet.position=Vector3.Lerp(hand,end,drop*drop);bouquet.rotation=Quaternion.Slerp(held,down,Mathf.SmoothStep(0,1,drop));
                }
            }
            else if(t<FallAt)PoseOnRoof(t);
            else if(t<DiveAt)PoseRoofFall(Mathf.InverseLerp(FallAt,DiveAt,t));
            else if(FastFall)
            {
                PoseRoofFall(1);var start=hero.root.position;
                float u=Mathf.InverseLerp(DiveAt,GlideAt,t);
                PersonPose(hero,Vector3.Lerp(start,GlideStart,u*u),Quaternion.LookRotation(Vector3.down,Vector3.forward));
            }
            else if(t<LandingAt)
            {
                float u=Mathf.InverseLerp(GlideAt,LandingAt,t),phase=(t-GlideAt)*Mathf.PI/1.6f;
                var position=Vector3.Lerp(GlideStart,GlideEnd,u)+new Vector3(.12f*Mathf.Cos(phase),0,Mathf.Sin(phase)*1.35f);
                PersonPose(hero,position,Quaternion.Euler(0,90+Mathf.Sin(phase)*9,0)*Quaternion.Euler(-68+Mathf.Sin(phase)*8,0,Mathf.Sin(phase)*22));
            }
            else
            {
                float local=t-LandingAt;var flat=Quaternion.Euler(0,90,0)*Quaternion.Euler(-90,0,0);float standUp=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.75f,local));
                var upright=Quaternion.Euler(0,90,0)*Quaternion.Euler(0,0,local>1.75f?Mathf.Sin((local-1.75f)*18)*Mathf.Exp(-(local-1.75f)*1.6f)*5:0);
                float soft=Mathf.Lerp(.1f,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.22f,local)));
                PersonPose(hero,landing,Quaternion.Slerp(flat,upright,standUp),true,ground+soft);
            }
            for(int i=0;i<hearts.Length;i++)
            {
                hearts[i].gameObject.SetActive(t>=GreetingEnd && t<SplitEnd);
                hearts[i].position=(wife.Head+boss.Head)*.5f+new Vector3(i*.3f-.15f,.85f+Mathf.Repeat(kissTime*.38f+i*.3f,.4f),0);
                hearts[i].localScale=Vector3.one*(.75f+Mathf.Sin(kissTime*5+i)*.15f);
            }
            questionLine.gameObject.SetActive(t>=SplitEnd+.1f && t<RecognitionAt);
            heroLine.gameObject.SetActive(t>=.85f && t<GreetingEnd);bossLine.gameObject.SetActive(t>=BossTalkAt+.1f && t<RoofAt);finalLine.gameObject.SetActive(t>=LandingAt+2.2f);
            roofLine.gameObject.SetActive(t>=RoofAt+.2f && t<FallAt-.15f);roofLine.text=t<RoofAt+2.1f?"Хуанг,\nкак ты мог":t<FarewellAt?"это уже 3-я\nлюбовница":"Не поминайте\nлихом";
            cold.weight=t>=ShockAt && t<BossTalkAt?1:0;underlight.intensity=cold.weight*7;underlight.transform.position=hero.root.position+hero.root.forward*.72f+Vector3.up*.18f;
            redlight.intensity=Recognition && !BadgeShot?8:0;redlight.transform.position=boss.Head+boss.root.forward*.8f+Vector3.up*.1f;
            var bounds=badge.GetComponent<MeshFilter>().sharedMesh.bounds;var glint=new Vector3(Mathf.Lerp(bounds.max.x,bounds.min.x,BadgeSweep),bounds.center.y,bounds.max.z+.024f);
            badgeGlint.localPosition=glint;badgeGlint.gameObject.SetActive(BadgeShot);
            badgeLight.intensity=BadgeShot?5:0;var target=badge.TransformPoint(glint);badgeLight.transform.SetPositionAndRotation(target+boss.root.forward*.18f,Quaternion.LookRotation(-boss.root.forward));
            interiorKey.enabled=inside;moon.intensity=inside?.65f:.8f;streetKey.transform.position=t<LandingAt?roof+new Vector3(5,4,5):landing+new Vector3(4,3,2);streetKey.enabled=!inside;
            lens.backgroundColor=inside?new Color(.12f,.09f,.065f):new Color(.13f,.18f,.24f);
            RenderSettings.ambientLight=inside?new Color(.5f,.39f,.26f):new Color(.25f,.32f,.42f);RenderSettings.fog=!inside;RenderSettings.fogColor=new Color(.17f,.22f,.28f);RenderSettings.fogStartDistance=24;RenderSettings.fogEndDistance=95;
            ApplyWeather();
        }
        void PoseOnRoof(float time)
        {
            float u=time-RoofAt,blown=Mathf.SmoothStep(0,1,Mathf.InverseLerp(FallAt-1.1f,FallAt,time));
            var position=roof+new Vector3(Mathf.Sin(u*4)*.025f+blown*.23f,0,Mathf.Sin(u*2.8f)*.012f);
            PersonPose(hero,position,Quaternion.Euler(0,0,Mathf.Sin(u*5)*4-blown*25),true,roof.y);
        }
        void PoseRoofFall(float progress)
        {
            PoseOnRoof(FallAt);roofFallOrigin=hero.root.position;var tilt=hero.root.rotation;
            var position=roofFallOrigin+new Vector3(1.4f*progress,-1.8f*progress*progress,0);
            var rotation=Quaternion.Slerp(tilt,Quaternion.LookRotation(Vector3.down,Vector3.forward),Mathf.SmoothStep(0,1,progress));
            PersonPose(hero,position,rotation);
        }
        void ApplyWeather()
        {
            bool visible=Elapsed>=RoofAt && Elapsed<LandingAt;rainRoot.gameObject.SetActive(visible);float t=Elapsed;
            for(int i=0;i<rainLines.Count;i++)
            {
                var seed=rainSeeds[i];var p=new Vector3(homeBounds.center.x+seed.x+Mathf.Sin(t*.9f)*.5f,ground+Mathf.Repeat(seed.y-t*9,22),homeBounds.center.z+seed.z);
                rainLines[i].SetPosition(0,p);rainLines[i].SetPosition(1,p+new Vector3(-.18f,-.9f,.03f));
            }
            bool strike=visible && ((t>=RoofAt+1.1f && t<RoofAt+1.21f) || (t>=RoofAt+2.5f && t<RoofAt+2.62f) || (t>=GlideAt+5.2f && t<GlideAt+5.3f));
            lightning.enabled=strike;flash.intensity=strike?4:0;
        }
        void View(Vector3 eye,Vector3 target,float fov,float roll=0)
        {CameraPosition=eye;CameraRotation=Quaternion.LookRotation(target-eye)*Quaternion.Euler(0,0,roll);lens.transform.SetPositionAndRotation(eye,CameraRotation);lens.fieldOfView=fov;}
        void ShakenCloseup(Vector3 target,Vector3 front,float distance,float fov,float strength)
        {
            var eye=target+front*distance;View(eye,target,fov);
            var jitter=new Vector3(Mathf.PerlinNoise(Elapsed*13,2)-.5f,Mathf.PerlinNoise(4,Elapsed*17)-.5f,Mathf.PerlinNoise(Elapsed*11,7)-.5f)*2;
            CameraPosition+=CameraRotation*(jitter*.012f*strength);CameraRotation*=Quaternion.Euler(jitter.x*1.6f*strength,jitter.y*1.6f*strength,jitter.z*2.8f*strength);lens.transform.SetPositionAndRotation(CameraPosition,CameraRotation);
        }
        void ApplyShot()
        {
            float t=Elapsed;
            if(t<GreetingEnd)
                View(roomOrigin+new Vector3(.85f,1.75f,-1.1f),hero.root.position+Vector3.up*1.55f,54);
            else if(t<SplitAt)
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(GreetingEnd,SplitAt,t));
                var target=(wife.Head+boss.Head)*.5f;View(roomOrigin+new Vector3(Mathf.Lerp(2.4f,1.8f,u),2,3),target,52);
            }
            else if(t<SplitEnd)
            {
                View(hero.root.position+hero.root.forward*3.4f+Vector3.up*1.85f,hero.root.position+Vector3.up*1.55f,51);
                var eye=roomOrigin+new Vector3(.6f,1.9f,1.85f);var target=(wife.Head+boss.Head)*.5f;
                secondCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));secondCamera.fieldOfView=42;
            }
            else if(t<RecognitionAt)
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(SplitEnd,RecognitionAt,t));
                View(hero.root.position+hero.root.forward*Mathf.Lerp(2.3f,2.05f,u)+Vector3.up*1.95f,hero.root.position+Vector3.up*1.98f,70);
            }
            else if(t<ShockAt)
            {
                if(BadgeShot)
                {
                    var b=badge.GetComponent<MeshFilter>().sharedMesh.bounds;var target=badge.TransformPoint(b.center);
                    float zoom=Mathf.SmoothStep(0,1,Mathf.InverseLerp(BadgeAt,BadgeEnd,t));View(target+boss.root.forward*Mathf.Lerp(1.02f,.88f,zoom),target,48);
                }
                else
                {
                    float from=t<BadgeAt?RecognitionAt:BadgeEnd,to=t<BadgeAt?BadgeAt:ShockAt;
                    float zoom=Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,t));ShakenCloseup(boss.Head,boss.root.forward,Mathf.Lerp(1.1f,.72f,zoom),100,1.4f);
                }
            }
            else if(t<BossTalkAt)
            {
                float zoom=Mathf.SmoothStep(0,1,Mathf.InverseLerp(ShockAt,ShockAt+.2f,t));ShakenCloseup(hero.Head,hero.root.forward,Mathf.Lerp(2.3f,.74f,zoom),Mathf.Lerp(54,108,zoom),.8f);
            }
            else if(t<RoofAt)
                View(boss.root.position+boss.root.forward*3.4f+Vector3.up*1.95f,boss.root.position+Vector3.up*1.92f,58);
            else if(t<DiveAt)
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RoofAt,FallAt,t));
                var target=(t<FallAt?hero.root.position:roofFallOrigin)+Vector3.up*1.5f+Vector3.left*.22f;
                View(roof+new Vector3(Mathf.Lerp(3.4f,2.6f,u),3.45f,Mathf.Lerp(4.8f,5.2f,u)),target,60,-2);
            }
            else if(FastFall)
            {
                var target=hero.Head;var eye=target+Vector3.down*.8f+Vector3.right*(Mathf.Sin(t*39)*.018f);
                CameraPosition=eye;CameraRotation=Quaternion.LookRotation(target-eye,Vector3.forward)*Quaternion.Euler(Mathf.Sin(t*47)*1.7f,Mathf.Sin(t*41)*1.7f,Mathf.Sin(t*33)*4);
                lens.transform.SetPositionAndRotation(CameraPosition,CameraRotation);lens.fieldOfView=105;
            }
            else if(t<LandingAt)
            {
                var center=hero.model.TransformPoint(new Vector3(0,1.15f,0));var close=center+new Vector3(4,0,4.6f);close.y=GlideStart.y+3.8f;
                var closeTarget=center;closeTarget.y=Mathf.Lerp(GlideStart.y+.45f,center.y,.25f);
                float pull=Mathf.SmoothStep(0,1,Mathf.InverseLerp(GlideAt+3.4f,LandingAt-.8f,t));var wide=homeBounds.center+new Vector3(18,3,9);
                var wideTarget=Vector3.Lerp(roof+new Vector3(1.2f,-1.5f,0),center,.35f);
                View(Vector3.Lerp(close,wide,pull),Vector3.Lerp(closeTarget,wideTarget,pull),Mathf.Lerp(54,48,pull),Mathf.Sin(t*1.8f)*2*(1-pull));
            }
            else
            {
                float stand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(LandingAt+.55f,LandingAt+1.9f,t));var eye=landing+new Vector3(3.5f,Mathf.Lerp(2.6f,1.85f,stand),2.6f);
                View(eye,landing+Vector3.up*Mathf.Lerp(.35f,1.6f,stand),55);
            }
            Billboard(heroLine,hero.root.position+Vector3.up*2.72f,lens);Billboard(questionLine,hero.root.position+Vector3.up*2.72f,lens);Billboard(bossLine,boss.root.position+Vector3.up*2.85f,lens);Billboard(finalLine,hero.root.position+Vector3.up*2.95f,lens);Billboard(roofLine,hero.root.position+Vector3.up*2.78f,lens);
            foreach(var heart in hearts)heart.rotation=(SplitScreen?secondCamera:lens).transform.rotation;
            for(int i=0;i<speedLines.Count;i++)
            {
                var line=speedLines[i];line.enabled=FastFall;if(!FastFall)continue;
                float a=i*Mathf.PI*2/speedLines.Count,r=.68f+Mathf.Repeat((t-DiveAt)*40+i*.137f,1.6f);
                line.SetPosition(0,CameraPosition+CameraRotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,1.7f));line.SetPosition(1,CameraPosition+CameraRotation*new Vector3(Mathf.Cos(a)*(r+.8f),Mathf.Sin(a)*(r+.8f),1.7f));
            }
            fadeRoot.SetPositionAndRotation(CameraPosition+CameraRotation*Vector3.forward*.08f,CameraRotation);fadeMaterial.SetFloat("_Opacity",FadeOpacity);fadeRenderer.enabled=FadeOpacity>0;
        }
        static void Billboard(TextMesh label,Vector3 position,Camera camera){label.transform.SetPositionAndRotation(position,camera.transform.rotation);}
    }
}