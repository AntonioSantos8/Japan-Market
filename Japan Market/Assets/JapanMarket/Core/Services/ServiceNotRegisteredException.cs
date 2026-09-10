using System;

namespace JapanMarket.Core
{
    /// <summary>
    /// Lançada quando um serviço obrigatório não foi registrado.
    ///
    /// Existe para substituir o comportamento antigo de devolver <c>null</c>: um
    /// null silencioso vira NullReferenceException dezenas de frames depois, em
    /// outro arquivo, sem pista da causa. Esta exceção estoura na origem e diz o
    /// nome do serviço que falta.
    /// </summary>
    public sealed class ServiceNotRegisteredException : Exception
    {
        public Type ServiceType { get; }

        public ServiceNotRegisteredException(Type serviceType)
            : base($"[Services] '{serviceType.Name}' não está registrado. " +
                   "Registre-o no GameContext antes de qualquer sistema tentar usá-lo, " +
                   "ou use TryResolve se a ausência for um estado válido.")
        {
            ServiceType = serviceType;
        }
    }
}
