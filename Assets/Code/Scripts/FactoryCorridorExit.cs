using UnityEngine;
namespace Cardboard { public class FactoryCorridorExit:MonoBehaviour { public CardboardFactoryGame game; public GameObject barrier; void LateUpdate(){if(barrier)barrier.SetActive(!game.ReadyToLeave);} void OnTriggerEnter(Collider other){var runner=other.GetComponent<CardboardRunner>();if(runner && runner.isPlayer)game.TryExit(true);} }}
