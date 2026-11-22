using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// เดินด้วย WASD / ลูกศร, กด Shift ค้างเพื่อวิ่ง ทิศทางอิงตามกล้อง
/// กันไม่ให้เดินลงไปในทะเลสาบ (พื้นที่ต่ำกว่าระดับน้ำ)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Min(0f)] private float walkSpeed = 2.2f;
    [SerializeField, Min(0f)] private float runSpeed = 4.5f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Water")]
    [Tooltip("Tag ของผิวน้ำ ห้ามเดินไปจุดที่พื้นอยู่ใต้ผิวน้ำ (ฉากที่ไม่มีน้ำเดินได้ทุกที่)")]
    [SerializeField] private string waterTag = "Water";
    [SerializeField, Min(0f)] private float maxWadeDepth = 0.25f;

    private CharacterController controller;
    private float verticalVelocity;

    /// <summary>ความเร็วแนวราบปัจจุบัน (เมตร/วินาที)</summary>
    public float HorizontalSpeed { get; private set; }
    public float RunSpeed => runSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();
        bool running = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 move = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f) * (running ? runSpeed : walkSpeed);

        if (move.sqrMagnitude > 0.0001f && !IsWalkable(transform.position + move.normalized * 0.6f))
            move = Vector3.zero;

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move + Vector3.up * verticalVelocity) * Time.deltaTime);
        HorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
    }

    private static Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private bool IsWalkable(Vector3 position)
    {
        // หาพื้นที่สูงที่สุด และผิวน้ำ (trigger ที่มี Tag Water) ณ จุดนั้น
        Vector3 origin = position + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 10f, ~0, QueryTriggerInteraction.Collide);
        float ground = float.NegativeInfinity;
        float water = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag(waterTag)) water = Mathf.Max(water, hit.collider.bounds.max.y);
            else if (!hit.collider.isTrigger) ground = Mathf.Max(ground, hit.point.y);
        }

        if (float.IsNegativeInfinity(water)) return true;   // ไม่มีน้ำตรงนี้ (เช่นในหมู่บ้าน)
        return ground >= water - maxWadeDepth;             // พื้นต้องไม่จมใต้น้ำลึกเกินไป
    }
}
