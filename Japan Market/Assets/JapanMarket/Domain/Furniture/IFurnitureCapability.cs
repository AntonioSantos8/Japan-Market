namespace JapanMarket.Domain
{
    /// <summary>
    /// Marcador para uma capacidade de móvel.
    ///
    /// Esta interface é o coração do item 6 do refatoramento: um móvel não "é"
    /// uma prateleira ou um caixa — ele TEM capacidades. Prateleira é um prefab
    /// com <c>IProductStorage</c>; caixa é um prefab com <c>ICheckoutStation</c>;
    /// um balcão refrigerado com leitor tem as duas.
    ///
    /// Nenhum sistema pergunta "que tipo de móvel é esse?". Todos perguntam
    /// "esse móvel tem a capacidade que eu preciso?" — e é por isso que criar um
    /// móvel novo não exige tocar em código nenhum.
    ///
    /// As INTERFACES moram aqui, em Domain, e não em Gameplay. Assim a fila do
    /// caixa e o serviço de checkout (Fase 5) trabalham contra o contrato sem
    /// nunca conhecer um MonoBehaviour, e continuam testáveis sem abrir cena.
    /// </summary>
    public interface IFurnitureCapability
    {
        /// <summary>O móvel que possui esta capacidade.</summary>
        IFurniture Owner { get; }
    }
}
