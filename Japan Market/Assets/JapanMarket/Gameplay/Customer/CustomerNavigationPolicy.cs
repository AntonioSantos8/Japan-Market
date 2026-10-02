using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>Customers share walkable space without blocking each other.</summary>
    public static class CustomerNavigationPolicy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void IgnoreCustomerCollisions()
        {
            int layer = LayerMask.NameToLayer("Npc");
            if (layer >= 0) Physics.IgnoreLayerCollision(layer, layer, true);
        }

        public static void Configure(NavMeshAgent agent)
        {
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            // None still resolves overlapping agent cylinders. Keep only a
            // millimetre of runtime radius: exactly zero can reject destinations.
            // Clearance from walls stays defined by the baked mesh.
            agent.radius = 0.001f;
        }

        public static bool TryCalculatePath(NavMeshAgent agent, Vector3 destination,
            NavMeshPath path, out Vector3 reachablePosition)
        {
            reachablePosition = default;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;

            // Shelf interaction points can lie inside the clearance baked around
            // furniture. The tiny crowd radius cannot project those destinations
            // by itself, so find a nearby walkable point before requesting a path.
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.25f, filter)
                || !agent.CalculatePath(hit.position, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;

            reachablePosition = hit.position;
            return true;
        }
    }
}
