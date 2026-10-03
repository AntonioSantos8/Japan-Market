using System;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JapanMarket.Tests
{
    public class PlayerTrashBagTests
    {
        private ToolDefinition _tool;
        private ToolBeltLayout _layout;
        private ToolBelt _belt;
        private PlayerTrashBag _bag;

        [SetUp]
        public void Setup()
        {
            _tool = ScriptableObject.CreateInstance<ToolDefinition>();
            var data = new SerializedObject(_tool);
            data.FindProperty("_isTrashBag").boolValue = true;
            data.FindProperty("_trashCapacity").intValue = 10;
            data.ApplyModifiedPropertiesWithoutUndo();
            _layout = ScriptableObject.CreateInstance<ToolBeltLayout>();
            _layout.EditorSetSlots(new[] { new ToolSlotLayout { Tool = _tool } });
            _belt = new ToolBelt(_layout);
            _bag = new PlayerTrashBag(_belt);
        }

        [TearDown]
        public void Teardown()
        {
            _belt.Dispose();
            Object.DestroyImmediate(_layout);
            Object.DestroyImmediate(_tool);
        }

        [Test]
        public void CollectionAndDeliveryRequireEquippedBag()
        {
            _bag.Restore(2, 0);
            Assert.IsFalse(_bag.TryCollect());
            Assert.IsFalse(_bag.TryEmpty(out _));
            Assert.AreEqual(2, _bag.Count);
            Assert.AreEqual(0, _bag.Scraps);
        }

        [Test]
        public void FullBagRejectsEleventhItem()
        {
            _belt.TrySelect(0);
            for (int i = 0; i < 10; i++) Assert.IsTrue(_bag.TryCollect());
            Assert.IsFalse(_bag.TryCollect());
            Assert.AreEqual(10, _bag.Count);
            Assert.IsTrue(_bag.IsFull);
        }

        [Test]
        public void TenFullBagsPayExactlyOneHundredScraps()
        {
            _belt.TrySelect(0);
            for (int bag = 0; bag < 10; bag++)
            {
                for (int i = 0; i < 10; i++) Assert.IsTrue(_bag.TryCollect());
                Assert.IsTrue(_bag.TryEmpty(out int paid));
                Assert.AreEqual(10, paid);
                Assert.AreEqual(0, _bag.Count);
            }
            Assert.AreEqual(100, _bag.Scraps);
            Assert.IsFalse(_bag.TryEmpty(out _));
            Assert.AreEqual(100, _bag.Scraps);
        }

        [Test]
        public void UnequippingPreservesContents()
        {
            _belt.TrySelect(0);
            _bag.TryCollect();
            _bag.TryCollect();
            _belt.Deselect();
            Assert.AreEqual(2, _bag.Count);
            Assert.IsFalse(_bag.CanCollect);
            _belt.TrySelect(0);
            Assert.AreEqual(2, _bag.Count);
            Assert.IsTrue(_bag.CanCollect);
        }

        [Test]
        public void PartialBagPaysOnlyForItsContentsAndCanBeReused()
        {
            _belt.TrySelect(0);
            _bag.TryCollect();
            _bag.TryCollect();
            Assert.IsTrue(_bag.TryEmpty(out int paid));
            Assert.AreEqual(2, paid);
            Assert.AreEqual(2, _bag.Scraps);
            Assert.IsTrue(_bag.TryCollect());
            Assert.AreEqual(1, _bag.Count);
        }

        [Test]
        public void SaveRoundTripRestoresContentsAndScraps()
        {
            var save = new GameSave { Version = GameSave.CurrentVersion, PlayerTrashCount = 7, Scraps = 100 };
            var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
            _bag.Restore(restored.PlayerTrashCount, restored.Scraps);
            Assert.AreEqual(7, _bag.Count);
            Assert.AreEqual(100, _bag.Scraps);
        }

        [Test]
        public void OldSaveStartsWithEmptyBagAndZeroScraps()
        {
            var save = JsonUtility.FromJson<GameSave>("{\"Version\":1}");
            _bag.Restore(save.PlayerTrashCount, save.Scraps);
            Assert.AreEqual(0, _bag.Count);
            Assert.AreEqual(0, _bag.Scraps);
        }

        [Test]
        public void InvalidSavedValuesAreClamped()
        {
            _bag.Restore(999, -50);
            Assert.AreEqual(10, _bag.Count);
            Assert.AreEqual(0, _bag.Scraps);
            _bag.Restore(-1, 100);
            Assert.AreEqual(0, _bag.Count);
            Assert.AreEqual(100, _bag.Scraps);
        }

        [Test]
        public void OverflowDoesNotLoseTrashOrCreateNegativeScraps()
        {
            _belt.TrySelect(0);
            _bag.Restore(10, long.MaxValue - 5);
            Assert.IsFalse(_bag.TryEmpty(out _));
            Assert.AreEqual(10, _bag.Count);
            Assert.AreEqual(long.MaxValue - 5, _bag.Scraps);
        }
    }
}
