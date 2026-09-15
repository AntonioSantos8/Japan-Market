using System;
using System.Collections.Generic;

namespace JapanMarket.Core
{

    public sealed class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new();

        private static ServiceContainer _current;

        public static ServiceContainer Current => _current ??= new ServiceContainer();

        public static void SetCurrent(ServiceContainer container) =>
            _current = container ?? throw new ArgumentNullException(nameof(container));

        public static void ClearCurrent(ServiceContainer container)
        {
            if (ReferenceEquals(_current, container)) _current = null;
        }

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public void RegisterAs(Type serviceType, object service)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[serviceType] = service;
        }

        public void Unregister<T>() where T : class => _services.Remove(typeof(T));

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

        private static bool IsAlive(object service)
        {
            if (service is UnityEngine.Object unityObject) return unityObject != null;
            return service != null;
        }
    }
}
