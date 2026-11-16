using UnityEngine;
namespace Cardboard
{
    public class FactoryPaperDramaSettings:ScriptableObject
    {
        public GameObject house1,house2,sofa,flowers,badge;
        public Material boss,floor,paper,ink,cream,heart,rain;
        public AudioClip[] dialogueVoices;
        public AudioClip plop,whoosh,thunder,rainLoop;
    }
}