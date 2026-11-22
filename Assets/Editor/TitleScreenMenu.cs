using UnityEditor;

/// <summary>เปิด/ปิดหน้า Title ตอนกด Play ใน Editor (ช่วยให้ทดสอบเกมเร็วขึ้น) ไม่มีผลกับเกมที่ build</summary>
public static class TitleScreenMenu
{
    private const string MenuPath = "Tools/Cozy Hook/Show Title Screen In Editor";

    [MenuItem(MenuPath)]
    private static void Toggle() => EditorPrefs.SetBool(TitleScreen.SkipTitlePref, !EditorPrefs.GetBool(TitleScreen.SkipTitlePref, false));

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        Menu.SetChecked(MenuPath, !EditorPrefs.GetBool(TitleScreen.SkipTitlePref, false));
        return true;
    }
}
