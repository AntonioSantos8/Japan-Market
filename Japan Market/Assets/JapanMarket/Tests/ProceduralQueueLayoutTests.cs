using System.Collections.Generic;
using JapanMarket.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Tests
{
    public sealed class ProceduralQueueLayoutTests
    {
        private static readonly Vector3 Origin = new(1000f, 0f, 1000f);
        private readonly List<GameObject> _objects = new();
        private readonly List<Vector3> _positions = new();
        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        [SetUp]
        public void SetUp() => BuildFloor();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
            if (_instance.valid) _instance.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
        }

        private void BuildFloor(bool divideWithWall = false)
        {
            if (_instance.valid) _instance.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
            var settings = NavMesh.GetSettingsByID(0);
            var sources = new List<NavMeshBuildSource>
            {
                new()
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(80f, 0.2f, 80f),
                    transform = Matrix4x4.TRS(Origin - Vector3.up * 0.1f, Quaternion.identity, Vector3.one),
                    area = 0
                }
            };
            if (divideWithWall)
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(0.2f, 3f, 80f),
                    transform = Matrix4x4.TRS(Origin + new Vector3(2.1f, 1.5f, 0f), Quaternion.identity, Vector3.one),
                    area = 1
                });
            _data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Origin, new Vector3(82f, 10f, 82f)), Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(_data);
            _instance = NavMesh.AddNavMeshData(_data);
            Assert.IsTrue(_instance.valid);
        }

        private BoxCollider Wall(Vector3 offset, Vector3 size, bool trigger = false)
        {
            var obj = new GameObject("Queue test obstacle");
            _objects.Add(obj);
            obj.transform.position = Origin + offset;
            var collider = obj.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = trigger;
            Physics.SyncTransforms();
            return collider;
        }

        private void Generate(int count = 20, Vector3? start = null, Vector3? direction = null, float spacing = 1.4f)
        {
            ProceduralQueueLayout.Generate(_positions, start ?? Origin, direction ?? Vector3.right,
                spacing, count, 0.54f, 2.07f, ~0);
        }

        [Test]
        public void Open_floor_generates_more_than_the_old_eleven_points()
        {
            Generate();
            Assert.AreEqual(20, _positions.Count);
            for (int i = 0; i < _positions.Count; i++)
            {
                Assert.That(_positions[i].x, Is.EqualTo(Origin.x + i * 1.4f).Within(0.02f));
                Assert.That(_positions[i].z, Is.EqualTo(Origin.z).Within(0.02f));
            }
        }

        [Test]
        public void Thin_unbaked_wall_is_never_crossed_even_with_walkable_floor_on_both_sides()
        {
            Wall(new Vector3(2.1f, 1.5f, 0f), new Vector3(0.05f, 3f, 80f));
            Generate();
            Assert.Greater(_positions.Count, 2, "There is room to turn along the wall.");
            foreach (Vector3 point in _positions)
                Assert.Less(point.x, Origin.x + 2.1f - 0.54f);
        }

        [Test]
        public void Baked_wall_without_collider_is_not_crossed()
        {
            BuildFloor(true);
            Generate();
            Assert.Greater(_positions.Count, 1);
            foreach (Vector3 point in _positions) Assert.Less(point.x, Origin.x + 2.1f);
        }

        [Test]
        public void Enclosed_start_only_has_one_safe_place()
        {
            Wall(new Vector3(0.9f, 1.5f, 0f), new Vector3(0.1f, 3f, 2f));
            Wall(new Vector3(-0.9f, 1.5f, 0f), new Vector3(0.1f, 3f, 2f));
            Wall(new Vector3(0f, 1.5f, 0.9f), new Vector3(2f, 3f, 0.1f));
            Wall(new Vector3(0f, 1.5f, -0.9f), new Vector3(2f, 3f, 0.1f));
            Generate();
            Assert.AreEqual(1, _positions.Count);
        }

        [Test]
        public void Start_inside_solid_obstacle_has_no_capacity()
        {
            Wall(Vector3.up, new Vector3(2f, 2f, 2f));
            Generate();
            Assert.IsEmpty(_positions);
        }

        [Test]
        public void Trigger_does_not_block_queue()
        {
            Wall(Vector3.up, new Vector3(10f, 2f, 10f), true);
            Generate();
            Assert.AreEqual(20, _positions.Count);
        }

        [Test]
        public void Missing_navmesh_does_not_invent_positions()
        {
            Generate(start: Origin + Vector3.right * 100f);
            Assert.IsEmpty(_positions);
        }

        [Test]
        public void Coincident_markers_do_not_invent_a_direction()
        {
            Generate(direction: Vector3.zero);
            Assert.IsEmpty(_positions);
        }

        [Test]
        public void Invalid_numeric_configuration_is_safe()
        {
            Assert.DoesNotThrow(() => Generate(spacing: float.NaN));
            Assert.IsEmpty(_positions);
            Generate(start: new Vector3(float.PositiveInfinity, 0f, 0f));
            Assert.IsEmpty(_positions);
        }

        [Test]
        public void Small_spacing_cannot_overlap_customer_bodies()
        {
            Generate(spacing: 0f);
            Assert.AreEqual(20, _positions.Count);
            for (int i = 1; i < _positions.Count; i++)
                Assert.GreaterOrEqual(Vector3.Distance(_positions[i - 1], _positions[i]), 1.13f);
        }

        [Test]
        public void Queue_is_bounded_and_does_not_overlap_itself_when_turning()
        {
            Wall(new Vector3(4f, 1.5f, 0f), new Vector3(1f, 3f, 4f));
            Generate(64);
            Assert.That(_positions.Count, Is.InRange(3, 64));
            for (int i = 0; i < _positions.Count; i++)
                for (int j = i + 1; j < _positions.Count; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(_positions[i], _positions[j]), 1.34f);
        }

        [Test]
        public void Rebuilding_discards_stale_positions_when_space_is_blocked()
        {
            Generate();
            Assert.IsNotEmpty(_positions);
            Wall(Vector3.up, new Vector3(2f, 2f, 2f));
            Generate();
            Assert.IsEmpty(_positions);
        }

        [Test]
        public void Legacy_npc_initializes_native_path_on_demand_and_rejects_offline_agent()
        {
            var type = System.Type.GetType("NpcTraject, Assembly-CSharp", true);
            var obj = new GameObject("Offline queue customer");
            _objects.Add(obj);
            obj.SetActive(false);
            var npc = obj.AddComponent(type);
            Assert.IsFalse((bool)type.GetMethod("CanReachQueuePosition").Invoke(npc, new object[] { Origin }));
            var path = type.GetField("_queuePath", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).GetValue(npc);
            Assert.IsNotNull(path, "Native NavMeshPath must be created outside the MonoBehaviour constructor.");
        }

        [Test]
        public void Legacy_register_without_markers_rejects_null_customer_without_throwing()
        {
            var type = System.Type.GetType("CashRegister, Assembly-CSharp", true);
            var obj = new GameObject("Unconfigured queue register");
            _objects.Add(obj);
            obj.SetActive(false);
            var register = obj.AddComponent(type);
            Assert.IsFalse((bool)type.GetMethod("TryEnterQueue").Invoke(register, new object[] { null }));
            type.GetMethod("RegenerateQueue").Invoke(register, null);
            Assert.AreEqual(0, type.GetProperty("QueueCapacity").GetValue(register));
        }
    }
}
