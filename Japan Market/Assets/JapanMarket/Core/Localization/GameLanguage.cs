namespace JapanMarket.Core
{

    public enum GameLanguage
    {
        PortugueseBrazil = 0,
        English          = 1,
        Japanese         = 2,
    }

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

        public static event System.Action<GameLanguage> LanguageChanged;

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
