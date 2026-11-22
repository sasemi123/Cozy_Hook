/// <summary>แผงตลาดปลา: กด E เพื่อเปิดหน้าต่างซื้อขายปลา</summary>
public class MarketStall : Interactable
{
    public override void Interact(PlayerInteractor player)
    {
        if (MarketUI.Instance != null) MarketUI.Instance.SetOpen(true);
    }
}
