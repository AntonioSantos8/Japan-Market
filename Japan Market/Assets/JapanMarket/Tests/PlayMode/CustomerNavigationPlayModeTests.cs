using System.Collections;
using System.Collections.Generic;
using JapanMarket.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace JapanMarket.Tests.PlayMode
{
    public sealed class CustomerNavigationPlayModeTests
    {
        private static readonly Vector3 Origin = new(1000f, 0f, 1000f);
        private readonly List<GameObject> _objects = new();
        private NavMeshData _data;
        private ScriptableObject _furnitureData;
        private NavMeshDataInstance _instance;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var sources = new List<NavMeshBuildSource>
            {
                new()
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(12f, 0.2f, 2f),
                    transform = Matrix4x4.TRS(Origin - Vector3.up * 0.1f,
                        Quaternion.identity, Vector3.one),
                    area = 0
                }
            };
            _data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Origin, new Vector3(14f, 10f, 4f)), Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null);
            _instance = NavMesh.AddNavMeshData(_data);
            Assert.That(_instance.valid, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
            if (_instance.valid) _instance.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
            if (_furnitureData != null) Object.DestroyImmediate(_furnitureData);
            Time.timeScale = _previousTimeScale;
        }

        private CustomerLocomotion Customer(float x)
        {
            var obj = new GameObject("Customer navigation regression");
            _objects.Add(obj);
            int layer = LayerMask.NameToLayer("Npc");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0));
            obj.layer = layer;
            Assert.That(NavMesh.SamplePosition(Origin + Vector3.right * x,
                out NavMeshHit hit, 0.5f, NavMesh.AllAreas), Is.True);
            obj.transform.position = hit.position;
            var body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var collider = obj.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            collider.center = Vector3.up;
            var agent = obj.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            var locomotion = obj.AddComponent<CustomerLocomotion>();
            locomotion.Configure(2f, 480f);
            agent.stoppingDistance = 0.05f;
            Assert.That(agent.Warp(hit.position), Is.True,
                $"Agent failed to bind: type={agent.agentTypeID}, radius={agent.radius}, position={hit.position}");
            Assert.That(agent.isOnNavMesh, Is.True);
            return locomotion;
        }

        [UnityTest]
        public IEnumerator Customers_cross_head_on_in_a_narrow_corridor()
        {
            CustomerLocomotion left = Customer(-3f), right = Customer(3f);
            // Newly enabled agents bind to the mesh on the next navigation update.
            yield return null;
            Assert.That(left.MoveTo(Origin + Vector3.right * 3f), Is.True);
            Assert.That(right.MoveTo(Origin - Vector3.right * 3f), Is.True);
            float closest = float.PositiveInfinity;
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((!left.HasArrived || !right.HasArrived) && Time.realtimeSinceStartup < deadline)
            {
                closest = Mathf.Min(closest, Vector3.Distance(left.transform.position, right.transform.position));
                Assert.That(left.PathFailed || right.PathFailed, Is.False);
                yield return null;
            }
            Assert.That(left.transform.position.x, Is.GreaterThan(Origin.x + 2.7f));
            Assert.That(right.transform.position.x, Is.LessThan(Origin.x - 2.7f));
            Assert.That(closest, Is.LessThan(0.25f), "The customers must actually overlap while passing.");
        }

        [UnityTest]
        public IEnumerator Waiting_customer_does_not_block_travel_after_halt_and_resume()
        {
            CustomerLocomotion waiting = Customer(0f), moving = Customer(-3f);
            yield return null;
            Vector3 waitingPosition = waiting.transform.position;
            foreach (float targetX in new[] { 3f, -3f })
            {
                moving.Halt();
                Assert.That(moving.MoveTo(Origin + Vector3.right * targetX), Is.True);
                float closest = float.PositiveInfinity;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!moving.HasArrived && Time.realtimeSinceStartup < deadline)
                {
                    closest = Mathf.Min(closest, Vector3.Distance(moving.transform.position, waitingPosition));
                    Assert.That(moving.PathFailed, Is.False);
                    yield return null;
                }
                Assert.That(moving.transform.position.x, Is.EqualTo(Origin.x + targetX).Within(0.3f));
                Assert.That(closest, Is.LessThan(0.25f));
                Assert.That(Vector3.Distance(waiting.transform.position, waitingPosition), Is.LessThan(0.02f));
            }
            int layer = LayerMask.NameToLayer("Npc");
            Assert.That(Physics.GetIgnoreLayerCollision(layer, layer), Is.True);
            Assert.That(Physics.GetIgnoreLayerCollision(layer, 0), Is.False,
                "Customers must still interact with the world and entrance triggers.");
        }

        [UnityTest]
        public IEnumerator Shelf_point_outside_baked_clearance_is_projected_to_a_reachable_destination()
        {
            CustomerLocomotion customer = Customer(-3f);
            yield return null;
            Vector3 shelfPoint = Origin + new Vector3(3f, 0f, 0.9f);
            Assert.That(NavMesh.SamplePosition(shelfPoint, out _, 0.01f, NavMesh.AllAreas), Is.False);
            Assert.That(customer.MoveTo(shelfPoint), Is.True);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!customer.HasArrived && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(customer.PathFailed, Is.False);
                yield return null;
            }
            Assert.That(customer.HasArrived, Is.True);
            Assert.That(Vector3.Distance(customer.transform.position, customer.Destination), Is.LessThan(0.3f));
            Assert.That(customer.transform.position.x, Is.GreaterThan(Origin.x + 2.7f));
        }

        private Behaviour LegacyCustomer()
        {
            CustomerLocomotion locomotion = Customer(-3f);
            GameObject obj = locomotion.gameObject;
            Object.DestroyImmediate(locomotion);
            System.Type type = System.Type.GetType("NpcTraject, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var legacy = (Behaviour)obj.AddComponent(type);
            // Run the real movement routine without starting shop/checkout services.
            legacy.enabled = false;
            return legacy;
        }

        private static IEnumerator Travel(Behaviour legacy, Vector3 target, System.Action<bool> completed)
        {
            return (IEnumerator)legacy.GetType().GetMethod("GoToDest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(legacy, new object[] { target, completed });
        }

        [UnityTest]
        public IEnumerator Legacy_customer_resumes_from_a_stop_and_confirms_physical_arrival()
        {
            Behaviour legacy = LegacyCustomer();
            yield return null;
            var agent = legacy.GetComponent<NavMeshAgent>();
            agent.isStopped = true;
            bool arrived = false;
            Vector3 target = Origin + new Vector3(3f, 0f, 0.9f);
            yield return Travel(legacy, target, result => arrived = result);
            Assert.That(arrived, Is.True);
            Assert.That(legacy.transform.position.x, Is.GreaterThan(Origin.x + 2.7f));
            Assert.That(agent.isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator Legacy_customer_does_not_confirm_arrival_for_an_unreachable_shelf()
        {
            Behaviour legacy = LegacyCustomer();
            yield return null;
            Vector3 start = legacy.transform.position;
            bool? arrived = null;
            yield return Travel(legacy, Origin + Vector3.right * 20f, result => arrived = result);
            Assert.That(arrived, Is.False);
            Assert.That(Vector3.Distance(legacy.transform.position, start), Is.LessThan(0.02f));
        }

        [UnityTest]
        public IEnumerator Legacy_customer_reaches_a_shelf_with_a_marker_at_product_height()
        {
            Behaviour legacy = LegacyCustomer();
            var furnitureObject = new GameObject("Shelf with elevated interaction marker");
            _objects.Add(furnitureObject);
            furnitureObject.transform.position = Origin + Vector3.right * 3f + Vector3.up * 1.56f;
            Component furniture = furnitureObject.AddComponent(System.Type.GetType("FurnitureInstance, Assembly-CSharp", true));
            _furnitureData = ScriptableObject.CreateInstance(System.Type.GetType("FurnitureData, Assembly-CSharp", true));
            _furnitureData.GetType().GetField("floorDistance").SetValue(_furnitureData, 1.56f);
            furniture.GetType().GetProperty("Data").SetValue(furniture, _furnitureData);
            var marker = new GameObject("Interaction marker 2.32m above floor");
            marker.transform.SetParent(furnitureObject.transform, false);
            marker.transform.localPosition = Vector3.up * 2.32f;
            furniture.GetType().GetField("interactionPoint").SetValue(furniture, marker.transform);
            Vector3 target = (Vector3)furniture.GetType().GetProperty("InteractionPosition").GetValue(furniture);
            Assert.That(target.y, Is.EqualTo(Origin.y));
            yield return null;
            bool arrived = false;
            yield return Travel(legacy, target, result => arrived = result);
            Assert.That(arrived, Is.True);
            Assert.That(legacy.transform.position.x, Is.GreaterThan(Origin.x + 2.7f));
        }

        [UnityTest]
        public IEnumerator Manager_spawns_agents_with_prefab_base_offset_and_they_can_walk()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Type managerType = System.Type.GetType("NpcManager, Assembly-CSharp", true);
            System.Type trajectoryType = System.Type.GetType("NpcTraject, Assembly-CSharp", true);
            var templates = new GameObject("Inactive NPC spawn templates");
            _objects.Add(templates);
            templates.SetActive(false);
            Component manager = templates.AddComponent(managerType);
            var spawnPoint = new GameObject("NPC spawn point");
            spawnPoint.transform.SetParent(templates.transform, false);
            spawnPoint.transform.position = Origin + Vector3.up * 0.4f;
            managerType.GetField("_spawnPoint", fields).SetValue(manager, spawnPoint.transform);
            managerType.GetField("_spawnRadius", fields).SetValue(manager, 0f);
            var activeNpcs = (List<GameObject>)managerType.GetField("_activeNpcs", fields).GetValue(manager);

            foreach (float offset in new[] { 0f, 1f })
            {
                var prefab = new GameObject($"NPC template offset {offset}");
                prefab.transform.SetParent(templates.transform, false);
                var prefabAgent = prefab.AddComponent<NavMeshAgent>();
                prefabAgent.radius = 0.54f;
                prefabAgent.height = 2.07f;
                prefabAgent.baseOffset = offset;
                prefabAgent.speed = 2f;
                prefabAgent.stoppingDistance = 0.05f;
                prefab.AddComponent(trajectoryType);
                managerType.GetField("_npcPrefabs", fields).SetValue(manager, new[] { prefab });
                int previousCount = activeNpcs.Count;
                managerType.GetMethod("SpawnNpc", fields).Invoke(manager, null);
                Assert.That(activeNpcs.Count, Is.EqualTo(previousCount + 1), "A valid mesh point must create an NPC.");
                GameObject npc = activeNpcs[previousCount];
                _objects.Add(npc);
                var trajectory = (Behaviour)npc.GetComponent(trajectoryType);
                trajectory.enabled = false;
                var agent = npc.GetComponent<NavMeshAgent>();
                Assert.That(agent.isOnNavMesh, Is.True);
                Assert.That(agent.radius, Is.EqualTo(0.001f).Within(0.00001f));
                Assert.That(NavMesh.SamplePosition(Origin, out NavMeshHit hit, 1f, NavMesh.AllAreas), Is.True);
                yield return null;
                Assert.That(npc.transform.position.y, Is.EqualTo(hit.position.y + offset).Within(0.01f));
                bool? arrived = null;
                yield return Travel(trajectory, Origin + Vector3.right * 3f, result => arrived = result);
                Assert.That(arrived, Is.True);
                Assert.That(agent.isOnNavMesh, Is.True);
                Assert.That(npc.transform.position.x, Is.GreaterThan(Origin.x + 2.7f));
            }
        }

        [UnityTest]
        public IEnumerator Failed_shopping_trip_preserves_ketchup_and_releases_the_shelf_slot()
        {
            Behaviour legacy = LegacyCustomer();
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Type shelfType = System.Type.GetType("Shelf, Assembly-CSharp");
            System.Type segmentType = System.Type.GetType("Segment, Assembly-CSharp");
            System.Type groupType = System.Type.GetType("SegmentTypeGroup, Assembly-CSharp");
            System.Type itemsType = System.Type.GetType("Items, Assembly-CSharp");
            object ketchup = System.Enum.Parse(itemsType, "Ketchup");
            var shelfObject = new GameObject("Unreachable stocked shelf");
            _objects.Add(shelfObject);
            shelfObject.SetActive(false);
            shelfObject.transform.position = Origin + Vector3.right * 20f;
            Component shelf = shelfObject.AddComponent(shelfType);
            Component segment = shelfObject.AddComponent(segmentType);
            var bottle = new GameObject("Ketchup must remain on shelf");
            bottle.transform.SetParent(shelfObject.transform, false);
            object group = System.Activator.CreateInstance(groupType);
            groupType.GetField("type").SetValue(group, ketchup);
            ((IList)groupType.GetField("spaces").GetValue(group)).Add(bottle.transform);
            System.Array groups = System.Array.CreateInstance(groupType, 1);
            groups.SetValue(group, 0);
            segmentType.GetField("groups", fields).SetValue(segment, groups);
            segmentType.GetField("_isInitialized", fields).SetValue(segment, true);
            segmentType.GetField("mySegment", fields).SetValue(segment, ketchup);
            segmentType.GetField("shelf", fields).SetValue(segment, shelf);
            var stockField = shelfType.GetField("shelf", fields);
            object stock = System.Activator.CreateInstance(stockField.FieldType);
            stockField.SetValue(shelf, stock);
            stockField.FieldType.GetMethod("Add").Invoke(stock, new[] { (object)segment, ketchup });
            Component furniture = shelfObject.AddComponent(System.Type.GetType("FurnitureInstance, Assembly-CSharp"));
            furniture.GetType().GetField("shelf").SetValue(furniture, shelf);
            Component occupancy = shelfObject.AddComponent(System.Type.GetType("FurnitureOccupancy, Assembly-CSharp"));
            occupancy.GetType().GetField("_maxOccupants", fields).SetValue(occupancy, 1);
            ((Behaviour)segment).enabled = false;
            shelfObject.SetActive(true);

            var managerObject = new GameObject("Shopping regression furniture manager");
            _objects.Add(managerObject);
            managerObject.SetActive(false);
            Component manager = managerObject.AddComponent(System.Type.GetType("FurnitureManager, Assembly-CSharp"));
            ((IList)manager.GetType().GetField("_placedFurnitures", fields).GetValue(manager)).Add(furniture);
            legacy.GetType().GetField("_furnitureManager", fields).SetValue(legacy, manager);
            var inventory = (IList)legacy.GetType().GetField("_inventory", fields).GetValue(legacy);
            var shopping = (IEnumerator)legacy.GetType().GetMethod("ShoppingRoutine", fields).Invoke(legacy, null);
            yield return shopping;
            Assert.That(bottle != null, Is.True, "An unreachable customer cannot remove the ketchup.");
            Assert.That(shelfType.GetMethod("PeekRandomItemType").Invoke(shelf, null), Is.EqualTo(ketchup));
            Assert.That(inventory.Count, Is.Zero, "The customer must not invent a checkout basket.");
            Assert.That((bool)occupancy.GetType().GetProperty("HasFreeSlot").GetValue(occupancy), Is.True);
        }
    }
}
