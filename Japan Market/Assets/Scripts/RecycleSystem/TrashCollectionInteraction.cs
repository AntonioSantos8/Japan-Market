using JapanMarket.Domain;
using JapanMarket.Gameplay;
using UnityEngine;

/// <summary>Collect floor litter through the usual player interaction.</summary>
[DisallowMultipleComponent]
public sealed class TrashCollectionInteraction : InteractableBase
{
    private bool collected;
    private PlayerTrashBag Bag => GameContext.Current != null ? GameContext.Current.PlayerTrashBag : null;

    public override bool OnLookAt()
    {
        PlayerTrashBag bag = Bag;
        interactionText = bag == null || !bag.IsEquipped ? "Equipe o saco de lixo"
            : bag.IsFull ? "Saco cheio — esvazie na lixeira" : "Coletar lixo";
        bool allowed = CanInteract && !collected && bag != null && bag.CanCollect;
        if (allowed) base.OnLookAt();
        else OnLookAway();
        return allowed;
    }

    public override void Interact()
    {
        if (collected || !CanInteract || !gameObject.activeInHierarchy) return;
        TrashItem item = GetComponent<TrashItem>();
        string category = item != null && item.Definition != null && item.Definition.Category != null
            ? item.Definition.Category.Key : "";
        PlayerTrashBag bag = Bag;
        if (bag == null || !bag.CanCollect) return;
        collected = true;
        if (!bag.TryCollect(category)) { collected = false; return; }
        ServiceLocator.Get<TrashSystem>()?.UnregisterTrash(gameObject);
        OnLookAway();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
