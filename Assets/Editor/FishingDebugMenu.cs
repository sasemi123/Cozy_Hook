using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>เมนูทดสอบระหว่าง Play mode: สั่งเหวี่ยงเบ็ดโดยไม่ต้องใช้เมาส์ และดูสถานะทุ่น</summary>
public static class FishingDebugMenu
{
    [MenuItem("Tools/Cozy Hook/Debug/Test Cast (Play Mode)")]
    private static void TestCast()
    {
        var caster = Object.FindAnyObjectByType<FishingRodCaster>();
        if (caster == null) { Debug.LogWarning("[FishingDebug] ไม่พบ FishingRodCaster"); return; }
        if (!caster.isActiveAndEnabled) { Debug.Log("[FishingDebug] คันเบ็ดถูกเก็บอยู่ เหวี่ยงไม่ได้"); return; }

        MethodInfo cast = typeof(FishingRodCaster).GetMethod("Cast", BindingFlags.NonPublic | BindingFlags.Instance);
        cast.Invoke(caster, new object[] { 0.6f });
        Debug.Log($"[FishingDebug] เหวี่ยงเบ็ดแล้ว t={Time.time:F2}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Test Cast (Play Mode)", true)]
    private static bool TestCastValidate() => Application.isPlaying;

    [MenuItem("Tools/Cozy Hook/Debug/Test Reel In (Play Mode)")]
    private static void TestReelIn()
    {
        var caster = Object.FindAnyObjectByType<FishingRodCaster>();
        if (caster == null) return;
        caster.ReelIn();
        Debug.Log($"[FishingDebug] ดึงทุ่นกลับ reeling={caster.IsReeling} t={Time.time:F2}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Toggle Rod Holster (Play Mode)")]
    private static void ToggleHolster()
    {
        var holster = Object.FindAnyObjectByType<RodHolster>();
        if (holster == null) return;
        holster.SetStowed(!holster.IsStowed);
        Debug.Log($"[FishingDebug] เก็บคันเบ็ด={holster.IsStowed} t={Time.time:F2}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Force Fish Bite (Play Mode)")]
    private static void ForceBite()
    {
        var bobber = Object.FindAnyObjectByType<FishingBobber>();
        if (bobber == null || bobber.State != BobberState.Floating) { Debug.Log("[FishingDebug] ต้องมีทุ่นลอยอยู่บนน้ำก่อน"); return; }
        typeof(FishingBobber).GetMethod("EnterFishBite", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bobber, null);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Toggle Reel Hold (Play Mode)")]
    private static void ToggleReelHold()
    {
        var tension = Object.FindAnyObjectByType<FishingLineTension>();
        if (tension == null) return;
        tension.ReelButtonHeld = !tension.ReelButtonHeld;
        Debug.Log($"[FishingDebug] กดดึงค้าง={tension.ReelButtonHeld} fighting={tension.IsFighting} tension={tension.Tension01:P0} ระยะ={tension.DistanceToPlayer:F1} t={Time.time:F2}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Log Tension")]
    private static void LogTension()
    {
        var tension = Object.FindAnyObjectByType<FishingLineTension>();
        if (tension == null) return;
        Debug.Log($"[FishingDebug] fighting={tension.IsFighting} reeling={tension.IsReeling} tension={tension.Tension01:P0} ระยะ={tension.DistanceToPlayer:F1} t={Time.time:F2}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Force Catch Fish (Play Mode)")]
    private static void ForceCatch()
    {
        var manager = Object.FindAnyObjectByType<FishCatchManager>();
        if (manager != null) manager.CatchRandomFish();
    }

    [MenuItem("Tools/Cozy Hook/Debug/Toggle Fish Bag (Play Mode)")]
    private static void ToggleBag()
    {
        var bag = Object.FindAnyObjectByType<FishInventoryUI>();
        if (bag != null) bag.SetOpen(!bag.IsOpen);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Bag Select First Fish (Play Mode)")]
    private static void SelectFirst()
    {
        var bag = Object.FindAnyObjectByType<FishInventoryUI>();
        if (bag == null) return;
        typeof(FishInventoryUI).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bag, new object[] { 0 });
    }

    [MenuItem("Tools/Cozy Hook/Debug/Close Catch Result (Play Mode)")]
    private static void CloseResult()
    {
        var result = Object.FindAnyObjectByType<CatchResultUI>();
        if (result != null) result.Close();
    }

    [MenuItem("Tools/Cozy Hook/Debug/Next Day (Play Mode)")]
    private static void NextDay() => GameState.Instance.NextDay();

    [MenuItem("Tools/Cozy Hook/Debug/Add 500 Coins (Play Mode)")]
    private static void AddCoins() => GameState.Instance.AddCoins(500);

    [MenuItem("Tools/Cozy Hook/Debug/Open Notice Board (Play Mode)")]
    private static void OpenBoard()
    {
        GameState.Instance.MarkBoardRead();
        if (NoticeBoardUI.Instance != null) NoticeBoardUI.Instance.SetOpen(true);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Open Market (Play Mode)")]
    private static void OpenMarket()
    {
        if (MarketUI.Instance != null) MarketUI.Instance.SetOpen(true);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Talk To Nearest Vendor (Play Mode)")]
    private static void TalkToVendor()
    {
        var player = Object.FindAnyObjectByType<PlayerInteractor>();
        VendorNPC best = null;
        foreach (var v in Object.FindObjectsByType<VendorNPC>(FindObjectsInactive.Exclude))
            if (best == null || (v.transform.position - player.transform.position).sqrMagnitude < (best.transform.position - player.transform.position).sqrMagnitude) best = v;
        if (best != null) best.Interact(player);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Close Windows (Play Mode)")]
    private static void CloseWindows()
    {
        if (NoticeBoardUI.Instance != null) NoticeBoardUI.Instance.SetOpen(false);
        if (MarketUI.Instance != null) MarketUI.Instance.SetOpen(false);
        if (BaitShopUI.Instance != null) BaitShopUI.Instance.SetOpen(false);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Market Sell All (Play Mode)")]
    private static void MarketSellAll()
    {
        if (MarketUI.Instance == null) return;
        typeof(MarketUI).GetMethod("SellAll", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(MarketUI.Instance, null);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Market Buy First (Play Mode)")]
    private static void MarketBuyFirst()
    {
        if (MarketUI.Instance == null) return;
        typeof(MarketUI).GetMethod("Buy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(MarketUI.Instance, new object[] { 0 });
    }

    [MenuItem("Tools/Cozy Hook/Debug/Travel Via Nearest Sign (Play Mode)")]
    private static void Travel()
    {
        var point = Object.FindAnyObjectByType<SceneTravelPoint>();
        if (point != null) point.Interact(null);
    }

    [MenuItem("Tools/Cozy Hook/Debug/Log Game State")]
    private static void LogState()
    {
        if (!Application.isPlaying) return;
        GameState s = GameState.Instance;
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        Debug.Log($"[FishingDebug] ฉาก={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} วัน={s.Day} เวลา={s.ClockText} เหรียญ={s.Coins} ปลา={s.Fish.Count} อ่านป้าย={s.HasReadBoardToday} ผู้เล่น={(player != null ? player.transform.position.ToString() : "-")}");
    }

    [MenuItem("Tools/Cozy Hook/Debug/Reset Save (Play Mode)")]
    private static void ResetSave() => GameState.Instance.ResetSave();

    [MenuItem("Tools/Cozy Hook/Debug/Log Bobber State")]
    private static void LogBobber()
    {
        var bobber = Object.FindAnyObjectByType<FishingBobber>();
        if (bobber == null) { Debug.Log($"[FishingDebug] ไม่มีทุ่นในฉาก t={Time.time:F2}"); return; }
        Debug.Log($"[FishingDebug] ทุ่น state={bobber.State} pos={bobber.transform.position} รออีก={bobber.TimeUntilBite:F1}s t={Time.time:F2}");
    }
}
