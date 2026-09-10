namespace JapanMarket.Core
{
    /// <summary>
    /// Marcador para eventos de domínio publicados no <see cref="IEventBus"/>.
    ///
    /// Eventos são sempre <c>readonly struct</c>: sem alocação por publicação e
    /// impossíveis de mutar depois de disparados, o que elimina a classe de bug
    /// "o segundo handler recebeu um evento alterado pelo primeiro".
    /// </summary>
    public interface IGameEvent
    {
    }
}
