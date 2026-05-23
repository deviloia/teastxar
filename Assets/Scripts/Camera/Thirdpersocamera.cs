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

    [Header("視角限制（角度，正值）")]
    [Tooltip("最高仰角，建議 60~80")]
    public float TopClamp = 70.0f;
    [Tooltip("最大俯角，建議 20~35，太大相機會貼近角色")]
    public float BottomClamp = 25.0f;

    [Header("垂直視角細調")]
    [Range(0.05f, 1.0f)]
    [Tooltip("往下看的靈敏度倍率：0.3 = 需要移動 3 倍距離才能達到相同俯角")]
    public float downwardSpeedScale = 0.3f;

    [Range(0f, 0.8f)]
    [Tooltip("接近俯角極限的哪個比例開始漸緩：0.33 = 最後三分之一會慢慢停")]
    public float bottomSoftZone = 0.33f;

    [Header("控制選項")]
    public bool invertY = false;

    [Header("靈敏度縮放（預設 0.05 接近 InputAction 手感）")]
    [Range(0.01f, 0.2f)]
    public float deltaScale = 0.05f;

    [Header("輸入平滑（0 = 不平滑，0.08 接近 Slerp 手感）")]
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

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _look = Vector2.zero;
            _smoothedLook = Vector2.zero;
            _lookVelocity = Vector2.zero;
            return;
        }

        _look = Mouse.current != null
            ? Mouse.current.delta.ReadValue() * deltaScale
            : Vector2.zero;

        _smoothedLook = inputSmoothTime > 0f
            ? Vector2.SmoothDamp(_smoothedLook, _look, ref _lookVelocity, inputSmoothTime, Mathf.Infinity, Time.deltaTime)
            : _look;

        if (_smoothedLook.sqrMagnitude >= _threshold)
        {
            float yawInput   =  _smoothedLook.x * horizontalSpeed * mouseSensitivity;
            float pitchInput = -_smoothedLook.y * verticalSpeed   * mouseSensitivity;

            if (invertY) pitchInput = -pitchInput;

            if (pitchInput > 0f) // 往下看
            {
                // 縮減往下的靈敏度
                pitchInput *= downwardSpeedScale;

                // 接近俯角極限時漸漸減速（soft stop）
                float softStart = BottomClamp * (1f - bottomSoftZone);
                if (_cinemachineTargetPitch > softStart && BottomClamp > softStart)
                {
                    float t = Mathf.Clamp01((_cinemachineTargetPitch - softStart) / (BottomClamp - softStart));
                    pitchInput *= 1f - t;
                }
            }

            _cinemachineTargetYaw   += yawInput;
            _cinemachineTargetPitch += pitchInput;
        }

        _cinemachineTargetYaw   = NormalizeAngle(_cinemachineTargetYaw);
        _cinemachineTargetPitch = Mathf.Clamp(_cinemachineTargetPitch, -TopClamp, BottomClamp);

        if (CameraTarget != null)
            CameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
}
