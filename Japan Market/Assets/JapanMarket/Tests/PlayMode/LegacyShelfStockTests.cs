using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests.PlayMode
{
    public sealed class LegacyShelfStockTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new();
        private static Type Legacy(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Item(string name) => Enum.Parse(Legacy("Items"), name);

        private GameObject Create(string name, bool active = false)
        {
            var obj = new GameObject(name);
            obj.SetActive(active);
            _objects.Add(obj);
            return obj;
        }

        private Component Stock(Component shelf, string itemName, bool hasItem)
        {
            var obj = Create("Stock segment");
            obj.transform.SetParent(shelf.transform, false);
            Component segment = obj.AddComponent(Legacy("Segment"));
            Type groupType = Legacy("SegmentTypeGroup");
            object item = Item(itemName);
            object group = Activator.CreateInstance(groupType);
            groupType.GetField("type").SetValue(group, item);
            if (hasItem)
            {
                var product = new GameObject(itemName);
                product.transform.SetParent(obj.transform, false);
                ((IList)groupType.GetField("spaces").GetValue(group)).Add(product.transform);
            }
            Array groups = Array.CreateInstance(groupType, 1);
            groups.SetValue(group, 0);
            segment.GetType().GetField("groups", Fields).SetValue(segment, groups);
            segment.GetType().GetField("_isInitialized", Fields).SetValue(segment, true);
            segment.GetType().GetField("mySegment", Fields).SetValue(segment, item);
            shelf.GetType().GetMethod("RegisterSegment").Invoke(shelf, new[] { item, segment });
            return segment;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void Empty_registration_does_not_hide_another_stocked_segment_of_the_same_type()
        {
            Component shelf = Create("Shelf").AddComponent(Legacy("Shelf"));
            Stock(shelf, "Ketchup", false);
            Stock(shelf, "Ketchup", true);
            Assert.That(shelf.GetType().GetProperty("HasItems").GetValue(shelf), Is.True);
            Assert.That(shelf.GetType().GetMethod("PeekRandomItemType").Invoke(shelf, null), Is.EqualTo(Item("Ketchup")));
            Assert.That(shelf.GetType().GetMethod("TakeItemOfType").Invoke(shelf, new[] { Item("Ketchup") }), Is.EqualTo(Item("Ketchup")));
            Assert.That(shelf.GetType().GetProperty("HasItems").GetValue(shelf), Is.False);
        }

        [Test]
        public void Restocking_a_segment_with_a_different_product_updates_the_registered_type()
        {
            Component shelf = Create("Shelf").AddComponent(Legacy("Shelf"));
            Component segment = Stock(shelf, "Ketchup", false);
            object milk = Item("Milk");
            Array groups = (Array)segment.GetType().GetField("groups", Fields).GetValue(segment);
            object group = groups.GetValue(0);
            group.GetType().GetField("type").SetValue(group, milk);
            ((IList)group.GetType().GetField("spaces").GetValue(group)).Add(Create("Milk").transform);
            segment.GetType().GetField("mySegment", Fields).SetValue(segment, milk);
            shelf.GetType().GetMethod("RegisterSegment").Invoke(shelf, new[] { milk, segment });
            Assert.That(shelf.GetType().GetMethod("PeekRandomItemType").Invoke(shelf, null), Is.EqualTo(milk));
            Assert.That(shelf.GetType().GetMethod("TakeItemOfType").Invoke(shelf, new[] { milk }), Is.EqualTo(milk));
        }

        [Test]
        public void Manager_discovers_scene_furniture_and_its_child_shelf_without_duplicates()
        {
            Component manager = Create("Furniture manager").AddComponent(Legacy("FurnitureManager"));
            GameObject furnitureObject = Create("Preplaced furniture", true);
            Component furniture = furnitureObject.AddComponent(Legacy("FurnitureInstance"));
            var child = Create("Child shelf", true);
            child.transform.SetParent(furnitureObject.transform, false);
            Component shelf = child.AddComponent(Legacy("Shelf"));
            var list = (IList)manager.GetType().GetMethod("GetPlacedFurnitures").Invoke(manager, null);
            Assert.That(list.Contains(furniture), Is.True);
            Assert.That(furniture.GetType().GetField("shelf").GetValue(furniture), Is.SameAs(shelf));
            int count = list.Count;
            manager.GetType().GetMethod("GetPlacedFurnitures").Invoke(manager, null);
            Assert.That(list.Count, Is.EqualTo(count));
            GameObject inactive = Create("Unplaced furniture");
            Component inactiveFurniture = inactive.AddComponent(Legacy("FurnitureInstance"));
            manager.GetType().GetMethod("GetPlacedFurnitures").Invoke(manager, null);
            Assert.That(list.Contains(inactiveFurniture), Is.False);
        }

        [Test]
        public void Customer_buys_affordable_stock_even_when_an_expensive_product_is_registered_first()
        {
            Component shelf = Create("Shelf with mixed prices").AddComponent(Legacy("Shelf"));
            Stock(shelf, "Milk", true);
            Stock(shelf, "Ketchup", true);
            Type priceType = Legacy("GlobalPrices");
            Component prices = Create("Prices").AddComponent(priceType);
            var priceMap = (IDictionary)priceType.GetField("_globalItemsPrice", Fields).GetValue(prices);
            priceMap.Add(Item("Milk"), 800f);
            priceMap.Add(Item("Ketchup"), 100f);
            var services = JapanMarket.Core.ServiceContainer.Current;
            services.TryResolveAs(priceType, out object previous);
            services.RegisterAs(priceType, prices);
            try
            {
                Component customer = Create("Customer").AddComponent(Legacy("NpcTraject"));
                customer.GetType().GetField("_itemsPerFurnitureWeights", Fields).SetValue(customer, new[] { 1 });
                customer.GetType().GetMethod("CollectItemsFromShelf", Fields).Invoke(customer, new object[] { shelf });
                var inventory = (IList)customer.GetType().GetField("_inventory", Fields).GetValue(customer);
                Assert.That(inventory.Count, Is.EqualTo(1));
                Assert.That(inventory[0].GetType().GetField("Type").GetValue(inventory[0]), Is.EqualTo(Item("Ketchup")));
                Assert.That(shelf.GetType().GetMethod("PeekRandomItemType").Invoke(shelf, null), Is.EqualTo(Item("Milk")));
            }
            finally
            {
                if (previous == null) services.UnregisterAs(priceType);
                else services.RegisterAs(priceType, previous);
            }
        }
    }
}
