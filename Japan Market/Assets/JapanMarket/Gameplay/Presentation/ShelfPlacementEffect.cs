using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Adds a short, material-safe celebration pass over every mesh in a product.
    /// The original renderers and their materials are never modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShelfPlacementEffect : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/ShelfPlacementCelebration";
        private const string ShaderName = "JapanMarket/Shelf Placement Celebration";
        private const string BurstShaderResourcePath = "Shaders/ShelfPlacementBurst";
        private const string BurstShaderName = "JapanMarket/Shelf Placement Burst";
        private const float Duration = 0.62f;

        private static readonly int GlowColorAId = Shader.PropertyToID("_GlowColorA");
        private static readonly int GlowColorBId = Shader.PropertyToID("_GlowColorB");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ProgressId = Shader.PropertyToID("_EffectProgress");
        private static readonly int SweepYId = Shader.PropertyToID("_SweepY");
        private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");

        private readonly List<Overlay> _overlays = new List<Overlay>();
        private MaterialPropertyBlock _propertyBlock;
        private MaterialPropertyBlock _burstPropertyBlock;

        private Material _effectMaterial;
        private Material _burstMaterial;
        private Mesh _burstMesh;
        private MeshRenderer _burstRenderer;
        private GameObject _burstObject;
        private Vector3 _burstOrigin;
        private float _burstDiameter;
        private Vector3 _restScale;
        private bool _ownsScale;
        private float _startedAt;

        private sealed class Overlay
        {
            public Renderer Source;
            public Renderer Effect;
        }

        private sealed class OverlayMarker : MonoBehaviour
        {
        }

        /// <summary>
        /// Plays (or restarts) the placement highlight on the complete item hierarchy.
        /// Returns false when the item has no supported renderer or the shader is unavailable.
        /// </summary>
        public static bool Play(GameObject itemRoot)
        {
            if (itemRoot == null) return false;

            ShelfPlacementEffect effect = itemRoot.GetComponent<ShelfPlacementEffect>();
            if (effect == null) effect = itemRoot.AddComponent<ShelfPlacementEffect>();

            return effect.Restart();
        }

        private bool Restart()
        {
            Cleanup();
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            if (_burstPropertyBlock == null) _burstPropertyBlock = new MaterialPropertyBlock();

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogWarning($"[ShelfPlacementEffect] Shader '{ShaderName}' not found.", this);
                DestroySelf();
                return false;
            }

            _effectMaterial = new Material(shader)
            {
                name = "Shelf Placement Celebration (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };

            _effectMaterial.SetColor(GlowColorAId, new Color(0.26f, 1f, 0.72f, 1f));
            _effectMaterial.SetColor(GlowColorBId, new Color(1f, 0.58f, 0.14f, 1f));

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Bounds combinedBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer source = renderers[i];
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                if (source.GetComponent<OverlayMarker>() != null) continue;

                Renderer glowOverlay = CreateOverlay(source, _effectMaterial, "ShelfPlacementGlowFx");

                if (glowOverlay != null)
                {
                    _overlays.Add(new Overlay { Source = source, Effect = glowOverlay });
                }

                if (glowOverlay != null)
                {
                    if (!hasBounds)
                    {
                        combinedBounds = source.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(source.bounds);
                    }
                }
            }

            if (_overlays.Count == 0)
            {
                Cleanup();
                DestroySelf();
                return false;
            }

            Shader burstShader = Resources.Load<Shader>(BurstShaderResourcePath);
            if (burstShader == null) burstShader = Shader.Find(BurstShaderName);
            if (burstShader != null && hasBounds) CreateBurst(combinedBounds, burstShader);

            _restScale = transform.localScale;
            _ownsScale = true;
            _startedAt = Time.unscaledTime;
            ApplyFrame(0f);
            return true;
        }

        private Renderer CreateOverlay(Renderer source, Material material, string objectName)
        {
            GameObject overlayObject = new GameObject(objectName)
            {
                layer = source.gameObject.layer,
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };

            Transform overlayTransform = overlayObject.transform;
            overlayTransform.SetParent(source.transform, false);
            overlayTransform.localPosition = Vector3.zero;
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;
            overlayObject.AddComponent<OverlayMarker>();

            Renderer overlayRenderer;
            int subMeshCount;

            if (source is MeshRenderer meshRenderer)
            {
                MeshFilter sourceFilter = meshRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    DestroyUnityObject(overlayObject);
                    return null;
                }

                MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
                overlayFilter.sharedMesh = sourceFilter.sharedMesh;
                overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
                subMeshCount = sourceFilter.sharedMesh.subMeshCount;
            }
            else if (source is SkinnedMeshRenderer skinnedRenderer)
            {
                if (skinnedRenderer.sharedMesh == null)
                {
                    DestroyUnityObject(overlayObject);
                    return null;
                }

                SkinnedMeshRenderer overlaySkinned = overlayObject.AddComponent<SkinnedMeshRenderer>();
                overlaySkinned.sharedMesh = skinnedRenderer.sharedMesh;
                overlaySkinned.rootBone = skinnedRenderer.rootBone;
                overlaySkinned.bones = skinnedRenderer.bones;
                overlaySkinned.localBounds = skinnedRenderer.localBounds;
                overlaySkinned.quality = skinnedRenderer.quality;
                overlaySkinned.updateWhenOffscreen = true;
                overlayRenderer = overlaySkinned;
                subMeshCount = skinnedRenderer.sharedMesh.subMeshCount;
            }
            else
            {
                DestroyUnityObject(overlayObject);
                return null;
            }

            Material[] materials = new Material[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;

            overlayRenderer.sharedMaterials = materials;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlayRenderer.sortingLayerID = source.sortingLayerID;
            overlayRenderer.sortingOrder = source.sortingOrder + 1;
            overlayRenderer.renderingLayerMask = source.renderingLayerMask;

            return overlayRenderer;
        }

        private void CreateBurst(Bounds itemBounds, Shader shader)
        {
            _burstMaterial = new Material(shader)
            {
                name = "Shelf Placement Burst (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };

            _burstMaterial.SetColor(GlowColorAId, new Color(0.2f, 1f, 0.72f, 1f));
            _burstMaterial.SetColor(GlowColorBId, new Color(1f, 0.55f, 0.08f, 1f));

            _burstMesh = new Mesh
            {
                name = "Shelf Placement Burst Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3( 0.5f, 0f, -0.5f),
                    new Vector3( 0.5f, 0f,  0.5f),
                    new Vector3(-0.5f, 0f,  0.5f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            _burstMesh.RecalculateBounds();

            _burstObject = new GameObject("ShelfPlacementBurst")
            {
                layer = gameObject.layer,
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };

            _burstOrigin = new Vector3(itemBounds.center.x, itemBounds.min.y + 0.012f, itemBounds.center.z);
            _burstDiameter = Mathf.Max(0.18f, Mathf.Max(itemBounds.size.x, itemBounds.size.z) * 1.2f);
            _burstObject.transform.position = _burstOrigin;
            _burstObject.transform.rotation = Quaternion.identity;

            MeshFilter filter = _burstObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _burstMesh;

            _burstRenderer = _burstObject.AddComponent<MeshRenderer>();
            _burstRenderer.sharedMaterial = _burstMaterial;
            _burstRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _burstRenderer.receiveShadows = false;
            _burstRenderer.lightProbeUsage = LightProbeUsage.Off;
            _burstRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _burstRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void Update()
        {
            float normalized = Mathf.Clamp01((Time.unscaledTime - _startedAt) / Duration);
            ApplyFrame(normalized);

            if (normalized >= 1f)
            {
                Cleanup();
                DestroySelf();
            }
        }

        private void ApplyFrame(float normalized)
        {
            float eased = normalized * normalized * (3f - 2f * normalized);
            float pulseTime = Mathf.Clamp01(normalized / 0.88f);
            float envelope = Mathf.Sin(pulseTime * Mathf.PI) * 0.68f;
            float impact = Mathf.Pow(1f - normalized, 3.2f) * 0.35f;

            ApplyScale(normalized);
            ApplyBurst(normalized, eased, envelope);

            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                Overlay overlay = _overlays[i];
                if (overlay.Source == null || overlay.Effect == null)
                {
                    _overlays.RemoveAt(i);
                    continue;
                }

                Bounds bounds = overlay.Source.bounds;
                float height = Mathf.Max(0.02f, bounds.size.y);
                float sweepPadding = height * 0.2f;

                _propertyBlock.Clear();
                _propertyBlock.SetFloat(IntensityId, envelope);
                _propertyBlock.SetFloat(ProgressId, normalized);
                _propertyBlock.SetFloat(ImpactId, impact);
                _propertyBlock.SetFloat(
                    SweepYId,
                    Mathf.Lerp(bounds.min.y - sweepPadding, bounds.max.y + sweepPadding, eased));
                _propertyBlock.SetFloat(SweepWidthId, Mathf.Max(0.018f, height * 0.18f));
                overlay.Effect.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ApplyScale(float normalized)
        {
            if (!_ownsScale) return;

            Vector3 multiplier;
            if (normalized < 0.1f)
            {
                float t = 1f - Mathf.Pow(1f - normalized / 0.1f, 3f);
                multiplier = Vector3.Lerp(Vector3.one, new Vector3(1.025f, 0.975f, 1.025f), t);
            }
            else if (normalized < 0.24f)
            {
                float t = (normalized - 0.1f) / 0.14f;
                t = t * t * (3f - 2f * t);
                multiplier = Vector3.Lerp(
                    new Vector3(1.025f, 0.975f, 1.025f),
                    new Vector3(0.99f, 1.025f, 0.99f),
                    t);
            }
            else
            {
                float t = (normalized - 0.24f) / 0.76f;
                float bounce = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.025f;
                multiplier = new Vector3(1f - bounce * 0.48f, 1f + bounce, 1f - bounce * 0.48f);
            }

            transform.localScale = Vector3.Scale(_restScale, multiplier);
        }

        private void ApplyBurst(float normalized, float eased, float envelope)
        {
            if (_burstObject == null || _burstRenderer == null) return;

            float expansion = 1f - Mathf.Pow(1f - eased, 2.4f);
            float diameter = _burstDiameter * Mathf.Lerp(0.5f, 1.22f, expansion);
            _burstObject.transform.position = _burstOrigin + Vector3.up * (normalized * 0.006f);
            _burstObject.transform.localScale = new Vector3(diameter, 1f, diameter);

            float fade = Mathf.Pow(1f - normalized, 1.35f);
            _burstPropertyBlock.Clear();
            _burstPropertyBlock.SetFloat(ProgressId, normalized);
            _burstPropertyBlock.SetFloat(IntensityId, Mathf.Clamp01(envelope * 0.32f + fade * 0.16f));
            _burstPropertyBlock.SetFloat(ImpactId, fade);
            _burstRenderer.SetPropertyBlock(_burstPropertyBlock);
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_ownsScale)
            {
                transform.localScale = _restScale;
                _ownsScale = false;
            }

            for (int i = 0; i < _overlays.Count; i++)
            {
                Renderer effect = _overlays[i].Effect;
                if (effect != null)
                {
                    effect.enabled = false;
                    DestroyUnityObject(effect.gameObject);
                }
            }

            _overlays.Clear();

            if (_effectMaterial != null)
            {
                DestroyUnityObject(_effectMaterial);
                _effectMaterial = null;
            }

            if (_burstRenderer != null) _burstRenderer.enabled = false;
            if (_burstObject != null) DestroyUnityObject(_burstObject);
            if (_burstMaterial != null) DestroyUnityObject(_burstMaterial);
            if (_burstMesh != null) DestroyUnityObject(_burstMesh);

            _burstRenderer = null;
            _burstObject = null;
            _burstMaterial = null;
            _burstMesh = null;
        }

        private void DestroySelf()
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }

        private static void DestroyUnityObject(Object value)
        {
            if (value == null) return;

            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
