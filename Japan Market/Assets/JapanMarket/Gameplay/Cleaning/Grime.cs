using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Uma mancha de sujeira na loja. Encolhe conforme é esfregada e some.
    ///
    /// Substitui o <c>Clean</c> legado. O que muda:
    ///
    ///  • A superfície é um asset, não a tag "Vassoura" no collider da
    ///    ferramenta. Com a tag, cada ferramenta nova exigia uma tag nova e um
    ///    ramo novo em cada sujeira; com a superfície, a esponja e o rodo são
    ///    dois assets e esta classe não sabe que nenhum dos dois existe.
    ///
    ///  • O contador não é estático. O <c>Clean.ActiveDustCount</c> atravessava
    ///    entradas em Play no editor e deixava a loja permanentemente suja; aqui
    ///    quem conta é um serviço que nasce e morre com o GameContext.
    ///
    ///  • Limpar é uma CHAMADA, não uma colisão com um trigger. É o jogador
    ///    usando a ferramenta que decide, e é o cinto que diz se aquela
    ///    ferramenta serve — a sujeira não precisa saber de nada disso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Grime : MonoBehaviour, IDirtSource
    {
        [Tooltip("Em que superfície esta sujeira está. Define que ferramenta a limpa.")]
        [SerializeField] private ToolSurface _surface;

        [Min(0.1f)]
        [Tooltip("Quanto de esfrega ela aguenta antes de sumir.")]
        [SerializeField] private float _toughness = 1f;

        [Range(0f, 1f)]
        [Tooltip("Escala mínima antes de sumir — só o visual.")]
        [SerializeField] private float _minScale = 0.2f;

        [Tooltip("Filho que encolhe durante a limpeza. Vazio procura um filho chamado Visual.")]
        [SerializeField] private Transform _visual;

        private IDirtRegistry _cleanliness;
        private Vector3 _fullScale;
        private float _remaining;
        private bool _registered;

        public ToolSurface Surface => _surface;
        public float Remaining => _remaining;
        public bool IsClean => _remaining <= 0f;

        private void Awake()
        {
            if (_visual == null) _visual = transform.Find("Visual");
            if (_visual == null) _visual = transform;

            _fullScale = _visual.localScale;
            _remaining = _toughness;
        }

        private void OnEnable() => Register();

        private void OnDisable() => Unregister();

        /// <summary>
        /// Esfrega. <paramref name="power"/> vem da ferramenta — é o cinto que
        /// decide se ela serve aqui, não esta classe.
        ///
        /// Devolve true quando ESTA esfregada terminou a sujeira, para quem
        /// chamou tocar o som e contar o objetivo uma vez só.
        /// </summary>
        public bool Scrub(float power)
        {
            if (IsClean || power <= 0f) return false;

            _remaining -= power;

            if (_remaining > 0f)
            {
                float fraction = Mathf.Clamp01(_remaining / _toughness);
                _visual.localScale = _fullScale * Mathf.Lerp(_minScale, 1f, fraction);
                return false;
            }

            _remaining = 0f;
            _visual.localScale = _fullScale * _minScale;

            // Desregistra ANTES de destruir: o Destroy só acontece no fim do
            // frame, e até lá um cliente que consultasse a limpeza ainda contaria
            // esta mancha.
            Unregister();
            Destroy(gameObject);

            return true;
        }

        // ── registro ─────────────────────────────────────────────────────────

        private void Register()
        {
            if (_registered) return;

            GameContext game = GameContext.Current;
            if (game == null) return;

            if (!game.Services.TryResolve(out IDirtRegistry service)) return;

            _cleanliness = service;
            _cleanliness.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered) return;

            _cleanliness?.Unregister(this);
            _cleanliness = null;
            _registered = false;
        }

        /// <summary>
        /// Uma sujeira posta na cena à mão nasce antes do GameContext quando a
        /// cena carrega; o OnEnable falha e ela nunca contaria. Tentar de novo no
        /// Start resolve sem transformar isto num Update.
        /// </summary>
        private void Start() => Register();
    }
}
