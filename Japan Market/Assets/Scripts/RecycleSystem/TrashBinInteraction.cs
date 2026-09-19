using UnityEngine;

[RequireComponent(typeof(JapanMarket.Gameplay.TrashBin))]
public sealed class TrashBinInteraction : InteractableBase
{
    private JapanMarket.Gameplay.TrashBin bin;

    public override void Awake()
    {
        base.Awake();
        bin = GetComponent<JapanMarket.Gameplay.TrashBin>();
        interactionText = "Retirar saco";
    }

    public override void Interact() => bin.TryReleaseBag(out _);
    public override bool OnLookAt() { base.OnLookAt(); return CanInteract; }
}
