namespace LetMeSleep.Gameplay
{
    public readonly struct PickupSwapOffer
    {
        public readonly uint PickupId, PickupRevision, InventoryRevision, ExpiresAtTick;
        public readonly int SlotIndex;
        public PickupSwapOffer(uint pickupId, uint pickupRevision, uint inventoryRevision, int slotIndex, uint expiresAtTick)
        { PickupId=pickupId;PickupRevision=pickupRevision;InventoryRevision=inventoryRevision;SlotIndex=slotIndex;ExpiresAtTick=expiresAtTick; }
    }
}
