using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new();

    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
            services[type] = service;
        else
            services.Add(type, service);
    }

    /// <summary>
    /// Devolve o serviço registado, ou default(T) se ainda não existir.
    /// O caso "não registado" é sempre logado: antes era silencioso e transformava-se
    /// numa NullReferenceException longe da origem — tipicamente dentro de uma callback
    /// de tween ou corrotina, onde a excecao aborta o resto do bloco sem ninguem notar.
    /// </summary>
    public static T Get<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
            return (T)service;

        UnityEngine.Debug.LogError(
            $"[ServiceLocator] Servico '{type.Name}' nao registado. " +
            "Verifica se o objeto existe na cena e se faz Register em Awake (nunca em Start).");
        return default;
    }
}
