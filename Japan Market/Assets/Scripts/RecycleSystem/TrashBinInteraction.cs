using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrashBinInteraction : InteractableBase
{
    public override void Awake()
    {
        base.Awake();
        interactionText = "Esvaziar saco de lixo";
    }

    public override void Interact()
    {
        var game = JapanMarket.Gameplay.GameContext.Current;
        if (!CanInteract || game == null || !game.PlayerTrashBag.TryEmpty(out int earned)) return;
        ServiceLocator.Get<Warnings>()?.ShowWarning($"+{earned} scraps", true);
    }

    public override bool OnLookAt()
    {
        var game = JapanMarket.Gameplay.GameContext.Current;
        var bag = game != null ? game.PlayerTrashBag : null;
        interactionText = bag == null || !bag.IsEquipped ? "Equipe o saco de lixo"
            : bag.Count == 0 ? "Saco vazio" : $"Esvaziar saco (+{bag.Count} scraps)";
        bool allowed = CanInteract && bag != null && bag.CanEmpty;
        if (allowed) base.OnLookAt();
        else OnLookAway();
        return allowed;
    }
}
