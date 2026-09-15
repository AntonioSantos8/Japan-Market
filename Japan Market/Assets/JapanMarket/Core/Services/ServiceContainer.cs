using System;
using System.Collections.Generic;

namespace JapanMarket.Core
{
    /// <summary>
    /// Implementação do <see cref="IServiceContainer"/>.
    ///
    /// <see cref="Current"/> existe apenas como ponte para o código legado
    /// (o <c>ServiceLocator</c> estático em Assembly-CSharp delega para cá) e
    /// para o composition root. Código novo recebe o container por injeção —
    /// não vá buscar <c>Current</c> de dentro de um sistema novo.
    /// </summary>
    public sealed class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new();

        // ── ponte para o código legado ───────────────────────────────────────

        private static ServiceContainer _current;

        /// <summary>
        /// Container ambiente. Criado sob demanda para que uma cena sem
        /// GameContext (todas as cenas atuais, hoje) continue funcionando
        /// exatamente como antes durante a migração.
        /// </summary>
        public static ServiceContainer Current => _current ??= new ServiceContainer();

        /// <summary>Chamado pelo GameContext no Awake.</summary>
        public static void SetCurrent(ServiceContainer container) =>
            _current = container ?? throw new ArgumentNullException(nameof(container));

        /// <summary>
        /// Descarta o container global se ainda for este.
        ///
        /// Chamado pelo <c>GameContext.Teardown()</c>, e NÃO pelo OnDestroy: o
        /// OnDestroy do contexto roda antes do de todo MonoBehaviour legado (é o
        /// preço do DefaultExecutionOrder(-10000)), e vários deles consultam
        /// serviços ao morrer. Quem quiser derrubar de verdade chama Teardown.
        /// </summary>
        public static void ClearCurrent(ServiceContainer container)
        {
            if (ReferenceEquals(_current, container)) _current = null;
        }

        // ── API ──────────────────────────────────────────────────────────────

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// Registro sem tipo estático. Só para o adaptador do ServiceLocator
        /// legado, onde o T vem de call sites que não podemos reescrever ainda.
        /// </summary>
        public void RegisterAs(Type serviceType, object service)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[serviceType] = service;
        }

        public void Unregister<T>() where T : class => _services.Remove(typeof(T));

        /// <summary>Versão sem tipo estático, para o adaptador legado.</summary>
        public void UnregisterAs(Type serviceType)
        {
            if (serviceType != null) _services.Remove(serviceType);
        }

        public T Resolve<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out object service) && IsAlive(service))
                return (T)service;

            throw new ServiceNotRegisteredException(typeof(T));
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out object found) && IsAlive(found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Versão sem tipo estático, para o adaptador legado.</summary>
        public bool TryResolveAs(Type serviceType, out object service)
        {
            if (_services.TryGetValue(serviceType, out object found) && IsAlive(found))
            {
                service = found;
                return true;
            }
            service = null;
            return false;
        }

        public bool IsRegistered<T>() where T : class =>
            _services.TryGetValue(typeof(T), out object s) && IsAlive(s);

        public void Clear() => _services.Clear();

        /// <summary>
        /// Um MonoBehaviour destruído continua sendo um objeto C# não-nulo, mas o
        /// Unity sobrecarrega <c>==</c> para reportá-lo como null. Esta checagem
        /// respeita essa sobrecarga, então um serviço cujo GameObject foi
        /// destruído conta como ausente em vez de virar MissingReferenceException
        /// no primeiro uso.
        /// </summary>
        private static bool IsAlive(object service)
        {
            if (service is UnityEngine.Object unityObject) return unityObject != null;
            return service != null;
        }
    }
}
