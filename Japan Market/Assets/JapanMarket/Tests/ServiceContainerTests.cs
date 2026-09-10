using JapanMarket.Core;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    public sealed class ServiceContainerTests
    {
        private interface IThing { int Value { get; } }
        private sealed class Thing : IThing { public int Value => 7; }

        [Test]
        public void Resolve_devolve_o_que_foi_registrado()
        {
            var container = new ServiceContainer();
            container.Register<IThing>(new Thing());

            Assert.AreEqual(7, container.Resolve<IThing>().Value);
        }

        [Test]
        public void Resolve_ausente_lanca_com_o_nome_do_servico()
        {
            var container = new ServiceContainer();

            var ex = Assert.Throws<ServiceNotRegisteredException>(() => container.Resolve<IThing>());
            StringAssert.Contains(nameof(IThing), ex.Message,
                "A mensagem precisa dizer QUAL serviço falta — é a diferença entre " +
                "achar o bug na origem e caçar um NRE em outro arquivo.");
        }

        [Test]
        public void TryResolve_ausente_devolve_false_sem_lancar()
        {
            var container = new ServiceContainer();

            Assert.IsFalse(container.TryResolve<IThing>(out IThing found));
            Assert.IsNull(found);
        }

        [Test]
        public void Registrar_de_novo_substitui()
        {
            var container = new ServiceContainer();
            container.Register<IThing>(new Thing());
            container.Register<IThing>(new Thing());

            Assert.IsTrue(container.IsRegistered<IThing>());
        }

        [Test]
        public void Unregister_e_Clear_removem()
        {
            var container = new ServiceContainer();
            container.Register<IThing>(new Thing());

            container.Unregister<IThing>();
            Assert.IsFalse(container.IsRegistered<IThing>());

            container.Register<IThing>(new Thing());
            container.Clear();
            Assert.IsFalse(container.IsRegistered<IThing>());
        }
    }
}
