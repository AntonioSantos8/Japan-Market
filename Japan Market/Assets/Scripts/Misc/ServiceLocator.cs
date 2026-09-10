using JapanMarket.Core;

/// <summary>
/// Fachada estática legada. NÃO USE EM CÓDIGO NOVO.
///
/// Continua existindo, com exatamente o mesmo comportamento de antes, para que
/// os scripts atuais não precisem mudar de uma vez. Por dentro, ela agora delega
/// para o <see cref="ServiceContainer"/> — então código legado e código novo
/// enxergam o mesmo registro de serviços durante toda a migração.
///
/// Duas coisas foram mantidas de propósito:
///
///  • <see cref="Get{T}"/> continua devolvendo default (null) quando o serviço
///    não existe. Vários call sites dependem disso — <c>if (_tutorialManager)</c>,
///    <c>ServiceLocator.Get&lt;TutorialManager&gt;()?.NotifyGameEvent(...)</c>.
///    Fazer este método lançar quebraria o jogo hoje.
///
///  • A assinatura genérica é idêntica, então <c>ServiceLocator.Register(this)</c>
///    continua registrando pelo tipo concreto, como antes.
///
/// Em código novo, peça <see cref="IServiceContainer"/> por injeção e use
/// <c>Resolve&lt;T&gt;()</c>, que lança com o nome do serviço faltando, ou
/// <c>TryResolve&lt;T&gt;()</c> quando a ausência for um estado válido.
/// </summary>
public static class ServiceLocator
{
    public static void Register<T>(T service)
    {
        if (service == null) return;
        ServiceContainer.Current.RegisterAs(typeof(T), service);
    }

    /// <summary>Devolve default quando ausente — comportamento legado preservado.</summary>
    public static T Get<T>()
    {
        return ServiceContainer.Current.TryResolveAs(typeof(T), out object service)
            ? (T)service
            : default;
    }

    /// <summary>Ponte para quem já quer o comportamento novo sem migrar o resto.</summary>
    public static bool TryGet<T>(out T service) where T : class
    {
        if (ServiceContainer.Current.TryResolveAs(typeof(T), out object found))
        {
            service = (T)found;
            return true;
        }
        service = null;
        return false;
    }

    /// <summary>Remove um serviço. Não existia antes; útil ao descarregar cena.</summary>
    public static void Unregister<T>()
    {
        ServiceContainer.Current.UnregisterAs(typeof(T));
    }
}
