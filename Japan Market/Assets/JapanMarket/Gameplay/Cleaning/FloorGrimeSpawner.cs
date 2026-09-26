using System;
using JapanMarket.Domain;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{
    /// <summary>Cria manchas apenas nas áreas de piso liberadas da loja.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorGrimeSpawner : MonoBehaviour
    {
        [Serializable]
        private struct FloorArea
        {
            [Tooltip("Seção da expansão; 1 é a área inicial da loja.")]
            public int section;
            [Tooltip("Centro da área em coordenadas do mundo.")]
            public Vector3 center;
            [Tooltip("Largura e comprimento da área em metros.")]
            public Vector2 size;
        }

        [SerializeField] private Grime _floorPrefab;
        [SerializeField] private FloorArea[] _areas = Array.Empty<FloorArea>();
        [Min(1f)] [SerializeField] private float _secondsBetweenTries = 30f;
        [Range(0f, 1f)] [SerializeField] private float _chancePerTry = 0.5f;
        [Min(1)] [SerializeField] private int _maxActive = 12;
        [Min(0.1f)] [SerializeField] private float _minimumSeparation = 1f;
        [SerializeField] private LayerMask _floorMask = ~0;

        private float _timer;

        private void OnEnable() => _timer = _secondsBetweenTries;

        private void Update()
        {
            GameContext game = GameContext.Current;
            if (game == null || game.Clock == null || !game.Clock.StoreIsOpen) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _secondsBetweenTries;

            if (_floorPrefab == null || _areas == null || _areas.Length == 0
                || CountFloorGrime() >= _maxActive
                || UnityEngine.Random.value > _chancePerTry) return;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (!TryPickArea(game.Expansions, out FloorArea area)) return;

                Vector3 candidate = area.center + new Vector3(
                    UnityEngine.Random.Range(-area.size.x * 0.5f, area.size.x * 0.5f),
                    0f,
                    UnityEngine.Random.Range(-area.size.y * 0.5f, area.size.y * 0.5f));

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit nav, 0.7f, NavMesh.AllAreas)
                    || !InsideArea(nav.position, area)) continue;

                Vector3 rayStart = nav.position + Vector3.up * 0.5f;
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit floor, 1f,
                        _floorMask, QueryTriggerInteraction.Ignore)) continue;

                if (Vector3.Dot(floor.normal, Vector3.up) < 0.8f
                    || Mathf.Abs(floor.point.y - nav.position.y) > 0.15f
                    || !InsideArea(floor.point, area)
                    || TooClose(floor.point)) continue;

                Instantiate(_floorPrefab, floor.point + floor.normal * 0.02f,
                    Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                return;
            }
        }

        private bool TryPickArea(IStoreExpansionService expansions, out FloorArea selected)
        {
            int count = 0;
            for (int i = 0; i < _areas.Length; i++)
                if (IsAvailable(_areas[i], expansions)) count++;

            if (count == 0) { selected = default; return false; }

            int index = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < _areas.Length; i++)
            {
                if (!IsAvailable(_areas[i], expansions)) continue;
                if (index-- != 0) continue;
                selected = _areas[i];
                return true;
            }

            selected = default;
            return false;
        }

        private static bool IsAvailable(FloorArea area, IStoreExpansionService expansions) =>
            area.size.x > 0f && area.size.y > 0f
            && (area.section <= 1 || expansions != null && expansions.IsOwned(area.section));

        private static bool InsideArea(Vector3 position, FloorArea area) =>
            Mathf.Abs(position.x - area.center.x) <= area.size.x * 0.5f
            && Mathf.Abs(position.z - area.center.z) <= area.size.y * 0.5f;

        private int CountFloorGrime()
        {
            int count = 0;
            Grime[] grime = FindObjectsByType<Grime>(FindObjectsSortMode.None);
            for (int i = 0; i < grime.Length; i++)
                if (grime[i].Surface == _floorPrefab.Surface) count++;
            return count;
        }

        private bool TooClose(Vector3 position)
        {
            Grime[] grime = FindObjectsByType<Grime>(FindObjectsSortMode.None);
            float minimumSquare = _minimumSeparation * _minimumSeparation;
            for (int i = 0; i < grime.Length; i++)
            {
                if (grime[i] == null || grime[i].Surface != _floorPrefab.Surface) continue;
                Vector3 delta = grime[i].transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minimumSquare) return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (_areas == null) return;
            Gizmos.color = new Color(0.7f, 0.4f, 0.1f, 0.8f);
            for (int i = 0; i < _areas.Length; i++)
            {
                FloorArea area = _areas[i];
                Gizmos.DrawWireCube(area.center,
                    new Vector3(area.size.x, 0.1f, area.size.y));
            }
        }
    }
}
