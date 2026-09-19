using System;

namespace JapanMarket.Core
{

    public sealed class ServiceNotRegisteredException : Exception
    {
        public Type ServiceType { get; }

        public ServiceNotRegisteredException(Type serviceType)
            : base($"[Services] '{serviceType.Name}' is not registered. " +
                   "Register it in the GameContext before any system tries to use it, " +
                   "or use TryResolve if its absence is a valid state.")
        {
            ServiceType = serviceType;
        }
    }
}
