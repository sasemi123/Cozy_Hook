using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>ทางไปฉากอื่น (เช่น ป้าย "ไปหมู่บ้าน") กด E แล้วโหลดฉากปลายทาง และไปโผล่ที่ SpawnPoint ที่กำหนด</summary>
public class SceneTravelPoint : Interactable
{
    [Tooltip("ชื่อฉากปลายทาง (ต้องอยู่ใน Build Settings)")]
    [SerializeField] private string targetScene = "Village";
    [Tooltip("ชื่อ SpawnPoint ในฉากปลายทาง")]
    [SerializeField] private string spawnId = "FromLake";

    public override void Interact(PlayerInteractor player)
    {
        GameState state = GameState.Instance;
        state.PendingSpawnId = spawnId;
        state.Save();
        SceneManager.LoadScene(targetScene);
    }

#if UNITY_EDITOR
    public void EditorSetTarget(string scene, string spawn)
    {
        targetScene = scene;
        spawnId = spawn;
    }
#endif
}
