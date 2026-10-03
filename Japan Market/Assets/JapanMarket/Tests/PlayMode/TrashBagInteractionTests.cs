#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using JapanMarket.Data;
using JapanMarket.Domain;
using JapanMarket.Gameplay;
using JapanMarket.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JapanMarket.Tests.PlayMode
{
    public class TrashBagInteractionTests
    {
        private GameObject _root;
        private GameContext _game;
        private Component _litter;
        private Component _bin;
        private TrashBagHud _hud;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.IsNull(GameContext.Current, "Integration tests need an empty test scene.");
            _root = new GameObject("Trash bag integration test");
            _root.SetActive(false);
            _game = _root.AddComponent<GameContext>();
            Set(_game, "_toolBelt", AssetDatabase.LoadAssetAtPath<ToolBeltLayout>("Assets/JapanMarket/Setup/ToolBeltLayout.asset"));
            Set(_game, "_validateCatalogOnPlay", false);
            Set(_game, "_saveOnDayEnd", false);
            _hud = _root.AddComponent<TrashBagHud>();
            _root.SetActive(true);
            var definition = AssetDatabase.LoadAssetAtPath<TrashDefinition>("Assets/JapanMarket/Setup/Lixo_plastico.asset");
            Assert.IsNotNull(definition);
            GameObject litter = Object.Instantiate(definition.Prefab, _root.transform);
            litter.GetComponent<TrashItem>().Initialize(definition);
            _litter = litter.GetComponent(Type.GetType("TrashCollectionInteraction, Assembly-CSharp"));
            Assert.IsNotNull(_litter);
            var binObject = new GameObject("Test bin");
            binObject.transform.SetParent(_root.transform);
            _bin = binObject.AddComponent(Type.GetType("TrashBinInteraction, Assembly-CSharp"));
            yield return null;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Interact(Component target) => target.GetType().GetMethod("Interact").Invoke(target, null);
        private void Equip()
        {
            for (int i = 0; i < _game.Tools.Slots.Count; i++)
                if (_game.Tools.Slots[i].Tool.IsTrashBag) { Assert.IsTrue(_game.Tools.TrySelect(i)); return; }
            Assert.Fail("Bag missing from wheel.");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_game != null) _game.Teardown();
            if (_root != null) Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FloorLitterRequiresBagAndIsDestroyedExactlyOnce()
        {
            Interact(_litter);
            Assert.AreEqual(0, _game.PlayerTrashBag.Count);
            Assert.IsTrue(_litter.gameObject.activeSelf);
            Equip();
            Interact(_litter);
            Interact(_litter);
            Assert.AreEqual(1, _game.PlayerTrashBag.Count);
            Assert.IsFalse(_litter.gameObject.activeSelf);
            yield return null;
            Assert.IsTrue(_litter == null);
        }

        [UnityTest]
        public IEnumerator FullBagKeepsLitterAndBinPaysScrapsWithoutChangingYen()
        {
            Equip();
            _game.PlayerTrashBag.Restore(10, 0);
            Interact(_litter);
            Assert.IsTrue(_litter.gameObject.activeSelf);
            long yen = _game.Ledger.Balance.Yen;
            Interact(_bin);
            Interact(_bin);
            Assert.AreEqual(10, _game.PlayerTrashBag.Scraps);
            Assert.AreEqual(0, _game.PlayerTrashBag.Count);
            Assert.AreEqual(yen, _game.Ledger.Balance.Yen);
            Interact(_litter);
            Assert.AreEqual(1, _game.PlayerTrashBag.Count);
            yield return null;
            Assert.IsTrue(_litter == null);
        }

        [UnityTest]
        public IEnumerator HudAndSaveFollowTheEquippedBag()
        {
            var panel = _root.transform.Find("Trash Bag HUD/Capacity");
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.gameObject.activeSelf);
            Equip();
            Assert.IsTrue(panel.gameObject.activeSelf);
            _game.PlayerTrashBag.Restore(2, 100);
            var saveService = new SaveService(_game);
            GameSave saved = saveService.Capture();
            _game.PlayerTrashBag.Restore(0, 0);
            saveService.Apply(saved);
            Assert.AreEqual(2, _game.PlayerTrashBag.Count);
            Assert.AreEqual(100, _game.PlayerTrashBag.Scraps);
            _game.Tools.Deselect();
            Assert.IsFalse(panel.gameObject.activeSelf);
            Assert.AreEqual(2, _game.PlayerTrashBag.Count);
            yield return null;
        }
    }
}
#endif
