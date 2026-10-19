using UnityEngine;
using static Cardboard.CardboardArt;

namespace Cardboard
{
    public static class FactoryPunchcard
    {
        public static Transform Create(Transform parent,int type)
        {
            var card=Group("Punched work order • gear diagram",parent,Vector3.zero);
            Box("Card",card,Vector3.zero,new Vector3(.42f,.60f,.018f),Cream);
            var view=card.gameObject.AddComponent<FactoryPunchcardView>();view.icons=new Transform[3];
            for(int i=0;i<3;i++)
            {
                var icon=Group("Gear diagram "+(i+1),card,new Vector3(0,.105f,.024f));
                icon.localScale=Vector3.one*(.29f/2.16f);ImportedFactoryGears.Create(icon,i+1);view.icons[i]=icon;
            }
            view.ShowType(type);
            for(int row=0;row<3;row++)for(int column=0;column<6;column++)
                if((column+row*2)%3!=0)
                    Box("Punch",card,new Vector3(-.15f+column*.06f,-.12f-row*.055f,.012f),new Vector3(.027f,.034f,.006f),Ink);
            return card;
        }
    }
}
