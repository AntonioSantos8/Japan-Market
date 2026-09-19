using System;
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

        private Action<ComputerAppView> _openAction;
        private Action _closeAction;

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
    }
}
