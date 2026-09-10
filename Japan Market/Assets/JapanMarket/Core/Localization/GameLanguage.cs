namespace JapanMarket.Core
{
    /// <summary>
    /// Idiomas suportados pelo jogo.
    ///
    /// Aqui um enum é adequado — e a distinção importa: idioma é um conjunto
    /// pequeno, controlado pela engenharia e que muda uma vez por ano. Produto é
    /// conteúdo, cresce toda semana e é editado por quem não abre o Visual Studio.
    /// Foi confundir as duas coisas que produziu o <c>enum Items</c>.
    ///
    /// Valores explícitos para que a preferência salva do jogador sobreviva a
    /// uma reordenação futura.
    /// </summary>
    public enum GameLanguage
    {
        PortugueseBrazil = 0,
        English          = 1,
        Japanese         = 2,
    }

    /// <summary>Idioma corrente. Um lugar só, lido por todo <see cref="LocalizedText"/>.</summary>
    public static class Localization
    {
        private static GameLanguage _current = GameLanguage.PortugueseBrazil;

        public static GameLanguage Current
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                LanguageChanged?.Invoke(value);
            }
        }

        /// <summary>Quem já desenhou texto na tela precisa redesenhar ao ouvir isto.</summary>
        public static event System.Action<GameLanguage> LanguageChanged;

        /// <summary>
        /// Ordem de fallback quando falta a tradução: idioma pedido, depois
        /// inglês, depois português. Nunca devolve string vazia se houver
        /// qualquer texto preenchido.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<GameLanguage> FallbackOrder
            => Fallbacks;

        private static readonly GameLanguage[] Fallbacks =
        {
            GameLanguage.English,
            GameLanguage.PortugueseBrazil,
            GameLanguage.Japanese,
        };
    }
}
