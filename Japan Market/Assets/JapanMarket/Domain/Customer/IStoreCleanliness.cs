namespace JapanMarket.Domain
{
    /// <summary>
    /// Quão suja está a loja. O cliente consulta para decidir se vai embora.
    ///
    /// Existe como contrato porque hoje o NPC lê <c>Clean.ActiveDustCount</c>,
    /// um contador estático — o que amarra a IA do cliente à implementação
    /// concreta da sujeira e torna impossível testar "cliente desiste de loja
    /// suja" sem instanciar sujeira de verdade na cena.
    ///
    /// A Fase 8 implementa isto junto com o sistema de ferramentas.
    /// </summary>
    public interface IStoreCleanliness
    {
        int ActiveDirtCount { get; }

        /// <summary>0 = impecável, 1 = no limite do tolerável.</summary>
        float Normalized { get; }
    }
}
