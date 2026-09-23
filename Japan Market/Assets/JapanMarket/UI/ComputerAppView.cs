using System;
using TMPro;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Liga um ícone persistente do prefab à janela persistente do mesmo app.
    /// Para adicionar um app, duplique um AppTemplate no prefab e troque seu
    /// ícone, nome e conteúdo da janela.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComputerAppView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _iconButton;
        [SerializeField] private GameObject _windowRoot;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private RectTransform _contentRoot;

        private Action<ComputerAppView> _openAction;
        private Action _closeAction;
        private ComputerApp _controller;

        internal string AppName
        {
            get
            {
                TextMeshProUGUI label = FindText("App Name");
                if (label != null && !string.IsNullOrWhiteSpace(label.text))
                    return label.text.Trim();

                const string prefix = "App - ";
                return gameObject.name.StartsWith(prefix, StringComparison.Ordinal)
                    ? gameObject.name.Substring(prefix.Length)
                    : gameObject.name;
            }
        }

        internal RectTransform ContentRoot
        {
            get
            {
                if (_contentRoot == null && _windowRoot != null)
                    _contentRoot = FindRect(_windowRoot.transform, "Content - Edit Here");

                return _contentRoot;
            }
        }

        internal void Bind(Action<ComputerAppView> openAction, Action closeAction)
        {
            Unbind();

            _openAction = openAction;
            _closeAction = closeAction;

            if (_iconButton != null) _iconButton.onClick.AddListener(RequestOpen);
            if (_closeButton != null) _closeButton.onClick.AddListener(RequestClose);
        }

        internal void Unbind()
        {
            if (_iconButton != null) _iconButton.onClick.RemoveListener(RequestOpen);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(RequestClose);

            _openAction = null;
            _closeAction = null;
        }

        internal void AttachController(ComputerApp controller)
        {
            _controller = controller;
            if (_controller == null || ContentRoot == null) return;

            Transform placeholder = FindTransform(ContentRoot, "Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            _controller.Attach(ContentRoot);
        }

        internal void OpenController() => _controller?.Open();

        internal void CloseController() => _controller?.Close();

        internal void ConfigureDisplayName(string appName, string initial)
        {
            gameObject.name = "App - " + appName;
            SetText("App Name", appName);
            SetText("Title", appName);
            SetText("Icon", initial);
            SetText("Placeholder", $"Conteúdo do app {appName}");
        }

        internal void SetIconLayout(int index)
        {
            if (_iconButton == null || _iconButton.transform is not RectTransform icon) return;

            icon.anchorMin = new Vector2(0f, 1f);
            icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0f, 1f);
            icon.anchoredPosition = new Vector2(24f + index * 154f, -34f);
            icon.sizeDelta = new Vector2(134f, 126f);
        }

        internal void SetIconVisible(bool visible)
        {
            if (_iconButton != null) _iconButton.gameObject.SetActive(visible);
        }

        internal void SetWindowVisible(bool visible)
        {
            if (_windowRoot != null) _windowRoot.SetActive(visible);
        }

        private void RequestOpen() => _openAction?.Invoke(this);
        private void RequestClose() => _closeAction?.Invoke();

        private void SetText(string objectName, string value)
        {
            TextMeshProUGUI text = FindText(objectName);
            if (text != null) text.text = value;
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
                if (labels[i].name == objectName) return labels[i];

            return null;
        }

        private static RectTransform FindRect(Transform root, string objectName) =>
            FindTransform(root, objectName) as RectTransform;

        private static Transform FindTransform(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransform(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
