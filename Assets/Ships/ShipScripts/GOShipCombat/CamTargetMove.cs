using UnityEngine;
public class CamTargetMove : MonoBehaviour
{
    static CamTargetMove instance;
    public static bool Locked
    {
        set
        {
            instance.enabled = !value;
        }
    }

    public static void ValidatePosition()
    {
        instance.AdjustPosition(0f, 0f);
    }


    private Transform swivel;
    private Transform stick;

    private float rotationAngle;
    private float zoom = 1f;
    private bool cameraStarted = false;

    [SerializeField] private float stickMinZoom = -250; // units
    [SerializeField] private float stickMaxZoom = -45; // units
    [SerializeField] private float swivelMinZoom = 90; // degrees
    [SerializeField] private float swivelMaxZoom = 45; // degrees
    [SerializeField] private float moveSpeedMinZoom = 100; // mult
    [SerializeField] private float moveSpeedMaxZoom = 100; // mult
    [SerializeField] private float rotationSpeed = 180;

    private void Awake()
    {
        swivel = transform.GetChild(0);
        stick = swivel.GetChild(0);
    }

    void OnEnable()
    {
        instance = this;
        AdjustZoom(0);
    }

    // Update is called once per frame
    void Update()
    {
        if (!cameraStarted)
        {
            ValidatePosition();
            cameraStarted = true;
        }
        float zoomDelta = Input.GetAxis("Mouse ScrollWheel");
        if (zoomDelta != 0f)
        {
            AdjustZoom(zoomDelta);
        }

        float rotationDelta = Input.GetAxis("Rotation");
        if (rotationDelta != 0f)
        {
            AdjustRotation(rotationDelta);
        }

        float xDelta = Input.GetAxis("Horizontal");
        float zDelta = Input.GetAxis("Vertical");
        if (xDelta != 0f || zDelta != 0f)
        {
            AdjustPosition(xDelta, zDelta);
        }
    }

    private void AdjustRotation(float delta)
    {

        rotationAngle += delta * rotationSpeed * Time.deltaTime;
        if (rotationAngle < 0f)
        {
            rotationAngle += 360f;
        }
        else if (rotationAngle >= 360f)
        {
            rotationAngle -= 360f;
        }
        transform.localRotation = Quaternion.Euler(0f, rotationAngle, 0f);
    }

    private void AdjustPosition(float xDelta, float zDelta)
    {
        Vector3 direction = transform.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        float damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        float distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;
        Vector3 position = transform.localPosition;
        position += direction * distance;
        transform.localPosition = position;
    }

    private void AdjustZoom(float delta)
    {
        zoom = Mathf.Clamp01(zoom + delta);
        float distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
        stick.localPosition = new Vector3(0f, 0f, distance);

        float angle = Mathf.Lerp(swivelMinZoom, swivelMaxZoom, zoom);
        swivel.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}