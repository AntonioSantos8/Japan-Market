using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.Art.Editor
{
    /// <summary>Editor-only, reusable geometry for the market's art pass.</summary>
    public static class MarketArtGeometry
    {
        private const string MeshFolder = "Assets/Art/StylizedMarket/Meshes";
        private const string MeshVersion = "rounded-box-v1";
        private static readonly Dictionary<string, Mesh> BoxMeshes = new Dictionary<string, Mesh>();
        private static Mesh sphereMesh;

        /// <summary>
        /// Creates a box in the parent's local coordinates. The mesh carries the dimensions;
        /// its transform stays at unit scale so normals and bevel widths remain consistent.
        /// </summary>
        public static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material mat, float bevel = .035f, bool solid = false)
        {
            ValidateSize(size);
            if (float.IsNaN(bevel) || float.IsInfinity(bevel))
                throw new ArgumentOutOfRangeException(nameof(bevel));

            float radius = Mathf.Clamp(bevel, 0f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .45f);
            var instance = NewObject(parent, name, position);
            instance.AddComponent<MeshFilter>().sharedMesh = GetBoxMesh(size, radius);
            instance.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (solid)
            {
                // A simple collision hull deliberately ignores the small decorative bevel.
                var collider = instance.AddComponent<BoxCollider>();
                collider.size = size;
            }
            return instance;
        }

        /// <summary>Creates a collider-free ellipsoid using Unity's shared sphere mesh.</summary>
        public static GameObject Ellipsoid(Transform parent, string name, Vector3 position,
            Vector3 size, Material mat)
        {
            ValidateSize(size);
            if (sphereMesh == null)
            {
                var source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = source.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(source);
            }

            var instance = NewObject(parent, name, position);
            instance.transform.localScale = size;
            instance.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            instance.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return instance;
        }

        private static GameObject NewObject(Transform parent, string name, Vector3 position)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }

        private static void ValidateSize(Vector3 size)
        {
            for (int axis = 0; axis < 3; axis++)
                if (size[axis] <= 0f || float.IsNaN(size[axis]) || float.IsInfinity(size[axis]))
                    throw new ArgumentOutOfRangeException(nameof(size), "Geometry dimensions must be finite and positive.");
        }

        private static Mesh GetBoxMesh(Vector3 size, float radius)
        {
            string key = MeshVersion + "|" + Number(size.x) + "|" + Number(size.y)
                + "|" + Number(size.z) + "|" + Number(radius);
            if (BoxMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            string path = MeshFolder + "/BeveledBox_" + Hash(key) + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                EnsureFolder(MeshFolder);
                mesh = BuildBoxMesh(size, radius);
                mesh.name = "Beveled box " + Number(size.x) + " x " + Number(size.y) + " x " + Number(size.z);
                AssetDatabase.CreateAsset(mesh, path);
            }
            BoxMeshes[key] = mesh;
            return mesh;
        }

        private static Mesh BuildBoxMesh(Vector3 size, float radius)
        {
            Vector3 half = size * .5f;
            Vector3 inner = half - Vector3.one * radius;
            bool rounded = radius > 0f;
            int grid = rounded ? 4 : 2;
            int faceVertexCount = grid * grid;
            var vertices = new Vector3[6 * faceVertexCount];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[6 * (grid - 1) * (grid - 1) * 6];
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up,
                Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] faceU = { Vector3.back, Vector3.forward, Vector3.right,
                Vector3.right, Vector3.right, Vector3.left };
            Vector3[] faceV = { Vector3.up, Vector3.up, Vector3.back,
                Vector3.forward, Vector3.up, Vector3.up };

            int triangle = 0;
            for (int face = 0; face < 6; face++)
            {
                Vector3 normal = faceNormals[face];
                Vector3 u = faceU[face];
                Vector3 v = faceV[face];
                float normalHalf = AxisExtent(half, normal);
                float uHalf = AxisExtent(half, u);
                float vHalf = AxisExtent(half, v);
                int offset = face * faceVertexCount;
                for (int row = 0; row < grid; row++)
                {
                    float vPosition = GridPosition(row, grid, vHalf, radius);
                    for (int column = 0; column < grid; column++)
                    {
                        float uPosition = GridPosition(column, grid, uHalf, radius);
                        Vector3 cubePoint = normal * normalHalf + u * uPosition + v * vPosition;
                        Vector3 vertexNormal = normal;
                        Vector3 point = cubePoint;
                        if (rounded)
                        {
                            // Project the face grid onto a radius around the inset cube.
                            // Adjacent faces calculate identical edge vertices and normals.
                            Vector3 closest = new Vector3(
                                Mathf.Clamp(cubePoint.x, -inner.x, inner.x),
                                Mathf.Clamp(cubePoint.y, -inner.y, inner.y),
                                Mathf.Clamp(cubePoint.z, -inner.z, inner.z));
                            vertexNormal = (cubePoint - closest).normalized;
                            point = closest + vertexNormal * radius;
                        }
                        int index = offset + row * grid + column;
                        vertices[index] = point;
                        normals[index] = vertexNormal;
                        // Each face has its own full 0..1 painted surface with edge padding.
                        uv[index] = new Vector2((uPosition + uHalf) / (2f * uHalf),
                            (vPosition + vHalf) / (2f * vHalf));
                    }
                }

                for (int row = 0; row < grid - 1; row++)
                for (int column = 0; column < grid - 1; column++)
                {
                    int a = offset + row * grid + column;
                    int b = a + 1;
                    int c = a + grid;
                    int d = c + 1;
                    triangles[triangle++] = a;
                    triangles[triangle++] = b;
                    triangles[triangle++] = d;
                    triangles[triangle++] = a;
                    triangles[triangle++] = d;
                    triangles[triangle++] = c;
                }
            }

            var mesh = new Mesh { vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float AxisExtent(Vector3 half, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * half.x + Mathf.Abs(axis.y) * half.y + Mathf.Abs(axis.z) * half.z;
        }

        private static float GridPosition(int index, int grid, float half, float radius)
        {
            if (grid == 2) return index == 0 ? -half : half;
            switch (index)
            {
                case 0: return -half;
                case 1: return -half + radius;
                case 2: return half - radius;
                default: return half;
            }
        }

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Hash(string value)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(24);
                for (int i = 0; i < 12; i++) result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
