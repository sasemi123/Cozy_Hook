using UnityEngine;

/// <summary>ตอนเริ่มฉาก ถ้ามาจากฉากอื่นจะย้ายผู้เล่นไปที่ SpawnPoint ที่ตรงกัน วางไว้บนตัวผู้เล่น</summary>
public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        GameState state = GameState.Instance;
        string id = state.PendingSpawnId;
        state.PendingSpawnId = null;
        if (string.IsNullOrEmpty(id)) return;

        foreach (SpawnPoint point in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude))
        {
            if (point.Id != id) continue;

            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(point.transform.position, Quaternion.Euler(0f, point.transform.eulerAngles.y, 0f));
            if (controller != null) controller.enabled = true;

            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam != null) cam.SnapBehindTarget();
            return;
        }
        Debug.LogWarning("[PlayerSpawner] ไม่พบ SpawnPoint: " + id);
    }
}
