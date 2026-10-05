using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Contestants
{
    public enum PrototypeActorKind { Player, Npc, Chef }

    public sealed class PrototypeActor : MonoBehaviour
    {
        public string StableId;
        public TeamId Team;
        public PrototypeActorKind Kind;
    }
}
