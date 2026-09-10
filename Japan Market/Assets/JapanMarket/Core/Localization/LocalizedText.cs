using System;
using UnityEngine;

namespace JapanMarket.Core
{
    /// <summary>
    /// Texto com uma entrada por idioma.
    ///
    /// Nasce na fundação de propósito: trocar <c>string</c> por texto localizável
    /// depois obrigaria a reabrir e reeditar todos os assets de produto. Fazer
    /// agora, com 16 produtos, custa quase nada; fazer com 160, custa uma tarde.
    /// </summary>
    [Serializable]
    public struct LocalizedText
    {
        [Serializable]
        public struct Entry
        {
            public GameLanguage language;
            [TextArea(1, 4)] public string text;
        }

        [SerializeField] private Entry[] _entries;

        public LocalizedText(params Entry[] entries) => _entries = entries;

        /// <summary>Atalho para conteúdo ainda não traduzido.</summary>
        public static LocalizedText FromSingle(GameLanguage language, string text) =>
            new(new Entry { language = language, text = text });

        /// <summary>Texto no idioma corrente do jogo.</summary>
        public string Value => Get(Localization.Current);

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        /// <summary>
        /// Texto no idioma pedido. Se faltar, percorre a ordem de fallback e, em
        /// último caso, devolve a primeira entrada não vazia — mostrar o texto no
        /// idioma errado é sempre melhor do que mostrar um espaço em branco.
        /// </summary>
        public string Get(GameLanguage language)
        {
            if (_entries == null || _entries.Length == 0) return string.Empty;

            if (TryFind(language, out string found)) return found;

            foreach (GameLanguage fallback in Localization.FallbackOrder)
                if (TryFind(fallback, out found)) return found;

            foreach (Entry entry in _entries)
                if (!string.IsNullOrEmpty(entry.text)) return entry.text;

            return string.Empty;
        }

        public bool HasTranslation(GameLanguage language) => TryFind(language, out _);

        private bool TryFind(GameLanguage language, out string text)
        {
            // HasTranslation chama direto, e a struct default tem _entries null.
            if (_entries == null) { text = null; return false; }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].language != language) continue;
                if (string.IsNullOrEmpty(_entries[i].text)) continue;
                text = _entries[i].text;
                return true;
            }
            text = null;
            return false;
        }

        public override string ToString() => Value;

        public static implicit operator string(LocalizedText t) => t.Value;
    }
}
