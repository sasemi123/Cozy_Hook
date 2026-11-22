using UnityEngine;

/// <summary>จุดเกิดของผู้เล่นเมื่อเดินทางมาจากฉากอื่น (ทิศที่หันคือแกน Z ของวัตถุนี้)</summary>
public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string id = "FromLake";

    public string Id => id;

#if UNITY_EDITOR
    public void EditorSetId(string value) => id = value;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward);
    }
#endif
}
