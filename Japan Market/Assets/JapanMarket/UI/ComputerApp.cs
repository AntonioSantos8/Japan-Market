using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Um app do computador da loja: Mercado, Preços, Banco, Relatório.
    ///
    /// A regra que vale para todos: o app NÃO guarda estado de jogo. Ele lê do
    /// serviço quando abre e quando o serviço avisa que algo mudou, e escreve
    /// chamando o serviço. Qualquer cópia local do saldo, do preço ou do estoque
    /// é uma cópia que um dia diverge — e divergir aqui significa o jogador ver
    /// um número e a loja cobrar outro.
    ///
    /// <see cref="Refresh"/> pode ser chamado a qualquer momento e quantas vezes
    /// for: ele sempre redesenha a partir do serviço.
    /// </summary>
    public abstract class ComputerApp : MonoBehaviour
    {
        /// <summary>Nome na barra de apps.</summary>
        public abstract string Title { get; }

        /// <summary>Onde o conteúdo do app é desenhado. Preenchido pelo host.</summary>
        protected RectTransform Content { get; private set; }

        private bool _built;

        internal void Attach(RectTransform content)
        {
            Content = content;
        }

        /// <summary>
        /// Chamado ao abrir. Constrói na primeira vez e só redesenha depois —
        /// reconstruir a moldura a cada abertura perderia a posição da rolagem e
        /// piscaria a tela inteira.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);

            if (!_built)
            {
                Build();
                _built = true;
            }

            Subscribe();
            Refresh();
        }

        public void Close()
        {
            Unsubscribe();
            gameObject.SetActive(false);
        }

        protected virtual void OnDestroy() => Unsubscribe();

        /// <summary>Monta a moldura fixa. Chamado UMA vez.</summary>
        protected abstract void Build();

        /// <summary>Redesenha tudo o que vem do serviço.</summary>
        public abstract void Refresh();

        /// <summary>
        /// Assina o que faz a tela mudar sozinha. Chamado ao abrir e desfeito ao
        /// fechar: um app fechado que continua assinado redesenha uma hierarquia
        /// que ninguém está vendo, a cada venda, para sempre.
        /// </summary>
        protected virtual void Subscribe() { }

        protected virtual void Unsubscribe() { }

        /// <summary>
        /// Resolve um serviço pelo container de Core.
        ///
        /// Pelo container, e não pelo <c>GameContext</c>, porque o assembly de UI
        /// não referencia Gameplay de propósito — é o que impede uma tela de
        /// passar a mexer em MonoBehaviour de cena e deixar de ser substituível.
        /// </summary>
        protected static bool TryGet<T>(out T service) where T : class =>
            ServiceContainer.Current.TryResolve(out service);
    }
}
