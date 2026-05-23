using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class Thirdpersocamera : NetworkBehaviour
{
    [Header("相機跟隨點")]
    public GameObject CameraTarget;

    [Header("相機靈敏度")]
    [Range(0.1f, 3.0f)]
    public float mouseSensitivity = 1.0f;
    [Range(0.1f, 3.0f)]
    public float horizontalSpeed = 1.0f;
    [Range(0.1f, 3.0f)]
    public float verticalSpeed = 1.0f;

    [Header("視角限制")]
    public float TopClamp = 70.0f;
    public float BottomClamp = -50.0f;

    [Header("控制選項")]
    public bool invertY = false;

    [Header("靈敏度縮放（Mouse.delta 是像素值，預設 0.05 接近原本 InputAction 的手感）")]
    [Range(0.01f, 0.2f)]
    public float deltaScale = 0.05f;

    [Header("輸入平滑（0 = 不平滑，0.08 接近原本 Slerp 的跟手感）")]
    [Range(0f, 0.15f)]
    public float inputSmoothTime = 0.08f;

    private const float _threshold = 0.00001f;
    private float _cinemachineTargetPitch;
    private float _cinemachineTargetYaw;
    private Vector2 _look;
    private Vector2 _smoothedLook;
    private Vector2 _lookVelocity;

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CameraHandler.Instance != null)
            CameraHandler.Instance.SetTarget(CameraTarget.transform);
        else
            Debug.LogError("找不到 CameraHandler！請確認場景裡有掛載 CameraHandler 的物件。");
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        // 遊標解鎖時（例如開啟 UI）停止旋轉
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _look = Vector2.zero;
            _smoothedLook = Vector2.zero;
            _lookVelocity = Vector2.zero;
            return;
        }

        // 每幀先清零，避免 Mouse 裝置不存在時沿用上一幀舊值
        _look = Mouse.current != null
            ? Mouse.current.delta.ReadValue() * deltaScale
            : Vector2.zero;

        // 平滑輸入而非平滑旋轉，不會產生抖動
        _smoothedLook = inputSmoothTime > 0f
            ? Vector2.SmoothDamp(_smoothedLook, _look, ref _lookVelocity, inputSmoothTime, Mathf.Infinity, Time.deltaTime)
            : _look;

        if (_smoothedLook.sqrMagnitude >= _threshold)
        {
            float yawInput   = _smoothedLook.x * horizontalSpeed * mouseSensitivity;
            float pitchInput = _smoothedLook.y * verticalSpeed   * mouseSensitivity;

            if (invertY) pitchInput = -pitchInput;

            _cinemachineTargetYaw   += yawInput;
            _cinemachineTargetPitch += pitchInput;
        }

        // Yaw 正規化：防止長時間遊玩造成浮點精度漂移
        _cinemachineTargetYaw = NormalizeAngle(_cinemachineTargetYaw);

        // Pitch clamp：先正規化到 [-180, 180]，再限制到視角範圍
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        if (CameraTarget != null)
            CameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
    }

    // 正規化到 [-180, 180]，再 clamp 到指定範圍
    // 原版只做一次 ±360 修正，angle 超過 ±720 時行為不正確
    private static float ClampAngle(float angle, float min, float max)
    {
        angle %= 360f;
        if (angle > 180f)  angle -= 360f;
        if (angle < -180f) angle += 360f;
        return Mathf.Clamp(angle, min, max);
    }

    // Yaw 不需要限制，但要週期性正規化避免浮點漂移
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
}
