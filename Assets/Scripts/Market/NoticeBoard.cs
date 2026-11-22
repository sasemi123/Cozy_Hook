/// <summary>ป้ายประกาศหมู่บ้าน: กด E เพื่อดูราคาปลาวันนี้ (ต้องอ่านก่อน ตลาดถึงจะบอกราคา)</summary>
public class NoticeBoard : Interactable
{
    public override string Prompt => GameState.Instance.HasReadBoardToday ? "ดูป้ายประกาศราคาปลา" : "อ่านป้ายประกาศราคาปลา (วันนี้ยังไม่ได้ดู!)";

    public override void Interact(PlayerInteractor player)
    {
        GameState.Instance.MarkBoardRead();
        if (NoticeBoardUI.Instance != null) NoticeBoardUI.Instance.SetOpen(true);
    }
}
