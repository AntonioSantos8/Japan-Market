using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests.PlayMode
{
    public sealed class ShelfSegmentOriginTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(0f)]
        [TestCase(0.35f)]
        [TestCase(1f)]
        public void Segment_origins_survive_furniture_spawn_scale_and_relocation(float spawnScale)
        {
            Type segmentType = Type.GetType("Segment, Assembly-CSharp", true);
            MethodInfo ensureParent = segmentType.GetMethod("EnsureItemsParent", PrivateInstance);
            FieldInfo parentField = segmentType.GetField("itemsParent", PrivateInstance);
            var furniture = new GameObject("Shelf spawn regression");
            furniture.SetActive(false);
            try
            {
                furniture.transform.SetPositionAndRotation(new Vector3(20f, 2f, -10f), Quaternion.Euler(0f, 73f, 0f));
                furniture.transform.localScale = Vector3.one * spawnScale;
                var segments = new Transform[6];
                var anchors = new Transform[6];
                for (int i = 0; i < segments.Length; i++)
                {
                    var segmentObject = new GameObject($"Segment {i}");
                    segments[i] = segmentObject.transform;
                    segments[i].SetParent(furniture.transform, false);
                    segments[i].localPosition = new Vector3(i % 2 * 0.9f, i / 2 * 0.55f, 0.1f);
                    segments[i].localRotation = Quaternion.Euler(0f, 90f, 0f);
                    // The interaction volume's scale must not scale the item grid.
                    segments[i].localScale = new Vector3(0.7f, 0.1f, 0.8f);
                    Component segment = segmentObject.AddComponent(segmentType);
                    ensureParent.Invoke(segment, null);
                    anchors[i] = (Transform)parentField.GetValue(segment);
                    ensureParent.Invoke(segment, null);
                    Assert.That(parentField.GetValue(segment), Is.SameAs(anchors[i]));
                }

                furniture.transform.localScale = Vector3.one;
                AssertOrigins(segments, anchors);
                furniture.transform.SetPositionAndRotation(new Vector3(-5f, 0f, 30f), Quaternion.Euler(0f, -42f, 0f));
                furniture.transform.localScale = Vector3.one * 1.5f;
                AssertOrigins(segments, anchors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(furniture);
            }
        }

        private static void AssertOrigins(Transform[] segments, Transform[] anchors)
        {
            Vector3 gridOffset = new(-0.12f, -0.1f, -0.25f);
            for (int i = 0; i < anchors.Length; i++)
            {
                Assert.That(Vector3.Distance(anchors[i].position, segments[i].position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(anchors[i].rotation, segments[i].rotation), Is.LessThan(0.001f));
                Assert.That(anchors[i].localScale, Is.EqualTo(Vector3.one));
                Vector3 firstSlot = anchors[i].TransformPoint(gridOffset);
                Vector3 nextSlot = anchors[i].TransformPoint(gridOffset + Vector3.forward * 0.2f);
                Assert.That(Vector3.Distance(firstSlot, nextSlot), Is.GreaterThan(0.19f));
                for (int j = 0; j < i; j++)
                    Assert.That(Vector3.Distance(firstSlot, anchors[j].TransformPoint(gridOffset)), Is.GreaterThan(0.5f));
            }
        }
    }
}
