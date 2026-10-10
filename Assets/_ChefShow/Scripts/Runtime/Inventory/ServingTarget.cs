using UnityEngine;
namespace ChefShow.Inventory
{
    public sealed class ServingTarget : MonoBehaviour
    {
        public ServingStation Station;
        public int Index = -1;
        public bool Submit;
        public bool ResetSubmission;
    }
}
