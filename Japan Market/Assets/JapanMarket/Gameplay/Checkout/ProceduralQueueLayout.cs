using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>Builds a bounded, continuous queue on the walkable floor.</summary>
    public static class ProceduralQueueLayout
    {
        private static readonly float[] TurnAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };
        private const float FloorClearance = 0.2f;

        public static void Generate(List<Vector3> positions, Vector3 start, Vector3 direction,
            float spacing, int maxSlots, float radius, float height, int obstacleMask,
            int agentType = 0, int areaMask = NavMesh.AllAreas)
        {
            positions.Clear();
            if (!IsFinite(start) || !IsFinite(direction) || !IsFinite(spacing)
                || !IsFinite(radius) || !IsFinite(height) || maxSlots <= 0) return;

            radius = Mathf.Max(0.1f, radius);
            height = Mathf.Max(radius * 2f, height);
            spacing = Mathf.Max(radius * 2f + 0.1f, spacing);
            maxSlots = Mathf.Clamp(maxSlots, 1, 128);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = areaMask };
            if (!TrySample(start, filter, out Vector3 first)
                || !HasRoom(first, radius, height, obstacleMask)) return;
            positions.Add(first);

            while (positions.Count < maxSlots)
            {
                Vector3 previous = positions[positions.Count - 1];
                bool found = false;
                foreach (float angle in TurnAngles)
                {
                    Vector3 heading = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                    Vector3 proposed = previous + heading * spacing;
                    if (!TrySample(proposed, filter, out Vector3 next)
                        || Mathf.Abs(next.y - previous.y) > 0.25f
                        || HorizontalDistance(previous, next) < spacing - 0.05f
                        || !HasRoom(next, radius, height, obstacleMask)
                        || NavMesh.Raycast(previous, next, out _, filter)
                        || !ClearSegment(previous, next, radius, height, obstacleMask)
                        || !AvoidsQueue(positions, next, spacing, radius)) continue;

                    positions.Add(next);
                    direction = next - previous;
                    direction.y = 0f;
                    direction.Normalize();
                    found = true;
                    break;
                }
                if (!found) break;
            }
        }

        private static bool TrySample(Vector3 point, NavMeshQueryFilter filter, out Vector3 position)
        {
            position = default;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.75f, filter)
                || HorizontalDistance(point, hit.position) > 0.35f) return false;
            position = hit.position;
            return true;
        }

        private static bool HasRoom(Vector3 feet, float radius, float height, int mask)
        {
            Capsule(feet, radius, height, out Vector3 bottom, out Vector3 top);
            foreach (Collider collider in Physics.OverlapCapsule(bottom, top, radius, mask,
                         QueryTriggerInteraction.Ignore))
                if (Blocks(collider, feet.y)) return false;
            return true;
        }

        private static bool ClearSegment(Vector3 from, Vector3 to, float radius, float height, int mask)
        {
            Capsule(from, radius, height, out Vector3 bottom, out Vector3 top);
            Vector3 delta = to - from;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized,
                         delta.magnitude, mask, QueryTriggerInteraction.Ignore))
                if (Blocks(hit.collider, Mathf.Min(from.y, to.y))) return false;
            return true;
        }

        private static void Capsule(Vector3 feet, float radius, float height,
                                    out Vector3 bottom, out Vector3 top)
        {
            bottom = feet + Vector3.up * (radius + FloorClearance);
            top = feet + Vector3.up * (height - radius + FloorClearance);
        }

        private static bool Blocks(Collider collider, float floorY) =>
            collider != null && collider.bounds.max.y > floorY + FloorClearance
            && collider.GetComponentInParent<NavMeshAgent>() == null
            && collider.GetComponentInParent<CharacterController>() == null;

        private static bool AvoidsQueue(List<Vector3> positions, Vector3 next, float spacing, float radius)
        {
            Vector3 from = positions[positions.Count - 1];
            for (int i = 0; i < positions.Count - 1; i++)
            {
                if (HorizontalDistance(positions[i], next) < spacing - 0.05f) return false;
                // Keep the new segment away from every earlier customer, including
                // the inside of a bend, rather than only checking its endpoint.
                if (DistanceToSegment(positions[i], from, next) < radius * 2f + 0.05f) return false;
                if (i + 1 >= positions.Count - 1) continue;
                Vector3 a = positions[i], b = positions[i + 1];
                if (SegmentsIntersect(a, b, from, next)
                    || DistanceToSegment(next, a, b) < radius * 2f + 0.05f
                    || DistanceToSegment(from, a, b) < radius * 2f + 0.05f) return false;
            }
            return true;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + segment * t);
        }

        private static bool SegmentsIntersect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float Cross(Vector3 p, Vector3 q, Vector3 r) =>
                (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x);
            return Cross(a, b, c) * Cross(a, b, d) < 0f
                && Cross(c, d, a) * Cross(c, d, b) < 0f;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
