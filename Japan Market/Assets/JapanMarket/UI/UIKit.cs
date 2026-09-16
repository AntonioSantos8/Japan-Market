using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// As peças de interface, montadas por código.
    ///
    /// Por que por código e não por prefab: cada tela destas é uma lista ligada a
    /// um serviço, e a lista muda de tamanho em runtime. Um prefab resolveria a
    /// moldura e não resolveria a linha — a linha teria que ser clonada de um
    /// template de qualquer jeito. E um prefab por tela seria seis hierarquias
    /// para alguém montar à mão antes de conseguir ver a primeira tela funcionar.
    ///
    /// O que isto NÃO é: arte. É cinza, quadrado e legível de propósito. Os
    /// controladores são ligados aos dados, não ao layout — trocar isto por
    /// prefabs bonitos depois não toca em nenhuma regra.
    /// </summary>
    public static class UIKit
    {
        // ── tema ─────────────────────────────────────────────────────────────

        public static readonly Color Background = new(0.11f, 0.12f, 0.15f, 0.98f);
        public static readonly Color Surface = new(0.17f, 0.19f, 0.23f, 1f);
        public static readonly Color SurfaceAlt = new(0.21f, 0.23f, 0.28f, 1f);
        public static readonly Color Accent = new(0.95f, 0.36f, 0.38f, 1f);
        public static readonly Color AccentDim = new(0.42f, 0.22f, 0.24f, 1f);
        public static readonly Color Text = new(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color TextDim = new(0.62f, 0.65f, 0.70f, 1f);
        public static readonly Color Good = new(0.44f, 0.80f, 0.52f, 1f);
        public static readonly Color Bad = new(0.92f, 0.45f, 0.42f, 1f);

        public const int TitleSize = 30;
        public const int HeadingSize = 20;
        public const int BodySize = 16;
        public const int SmallSize = 13;

        // ── estrutura ────────────────────────────────────────────────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;

            rect.SetParent(parent, worldPositionStays: false);
            rect.localScale = Vector3.one;

            return rect;
        }

        /// <summary>Estica para preencher o pai inteiro, com margem opcional.</summary>
        public static RectTransform Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);

            return rect;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text,
                                            int size = BodySize, Color? color = null,
                                            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            RectTransform rect = Rect(name, parent);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color ?? Text;
            label.alignment = align;
            label.raycastTarget = false;

            // Sem fonte padrão configurada no projeto o texto não desenha e nada
            // no console explica. Melhor gritar uma vez do que entregar uma tela
            // em branco que parece bug de layout.
            if (label.font == null && TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning(
                    "[UI] O projeto não tem uma fonte padrão do TextMeshPro. " +
                    "Window → TextMeshPro → Import TMP Essential Resources.");
            }

            return label;
        }

        public static Button Button(string name, Transform parent, string text,
                                    UnityEngine.Events.UnityAction onClick,
                                    int size = BodySize, Color? background = null)
        {
            Image image = Panel(name, parent, background ?? SurfaceAlt);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(onClick);

            TextMeshProUGUI label = Label("Label", image.transform, text, size, Text,
                                          TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 4f);

            return button;
        }

        /// <summary>
        /// Uma linha horizontal com filhos lado a lado. Devolve o RectTransform
        /// para quem quiser ajustar a altura.
        /// </summary>
        public static RectTransform Row(string name, Transform parent, float height,
                                        float spacing = 8f, RectOffset padding = null)
        {
            RectTransform rect = Rect(name, parent);

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = height;
            size.preferredHeight = height;

            return rect;
        }

        public static RectTransform Column(string name, Transform parent, float spacing = 6f,
                                           RectOffset padding = null)
        {
            RectTransform rect = Rect(name, parent);

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            return rect;
        }

        /// <summary>
        /// Dá largura fixa ou flexível a um filho de layout. Sem isto o
        /// HorizontalLayoutGroup distribui tudo igual e as colunas saem tortas.
        /// </summary>
        public static T Width<T>(this T component, float preferred, float flexible = 0f)
            where T : Component
        {
            LayoutElement element = component.GetComponent<LayoutElement>()
                                    ?? component.gameObject.AddComponent<LayoutElement>();

            element.preferredWidth = preferred;
            element.minWidth = preferred;
            element.flexibleWidth = flexible;

            return component;
        }

        /// <summary>Ocupa o espaço que sobrar na linha.</summary>
        public static T Grow<T>(this T component, float weight = 1f) where T : Component
        {
            LayoutElement element = component.GetComponent<LayoutElement>()
                                    ?? component.gameObject.AddComponent<LayoutElement>();

            element.flexibleWidth = weight;
            element.minWidth = 0f;

            return component;
        }

        /// <summary>
        /// Uma lista rolável. Devolve o CONTEÚDO — é nele que as linhas entram.
        ///
        /// O ContentSizeFitter no conteúdo é o que faz a barra de rolagem saber o
        /// tamanho real: sem ele a lista rola até o infinito ou não rola nada,
        /// dependendo do que o layout do pai resolver.
        /// </summary>
        public static RectTransform ScrollList(string name, Transform parent,
                                               out ScrollRect scroll, float spacing = 4f)
        {
            Image viewport = Panel(name, parent, new Color(0f, 0f, 0f, 0.15f));
            Stretch((RectTransform)viewport.transform);

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RectTransform clip = Rect("Viewport", viewport.transform);
            Stretch(clip, 2f);
            clip.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Column("Content", clip, spacing,
                                           new RectOffset(4, 4, 4, 4));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, content.offsetMax.y);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = clip;
            scroll.content = content;

            return content;
        }

        /// <summary>Apaga os filhos de um container. É como toda lista se redesenha.</summary>
        public static void Clear(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
