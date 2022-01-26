using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTSHexagonsV2
{
    public class HexMapCamera : MonoBehaviour
    {
        static HexMapCamera instance;
        public static bool Locked
        {
            set
            {
                instance.enabled = !value;
            }
        }

        public static bool ForceWrapUpdate = false;

        public static void ValidatePosition()
        {
            if (GridAPI.ActiveGridEntity == Entity.Null)
            {
                return;
            }
            instance.AdjustPosition(0f, 0f);
        }
        private World mainWorld;
        private EntityManager entityManager;
        private Transform swivel;
        private Transform stick;

        private float rotationAngle;
        private float zoom = 1f;
        private bool cameraStarted = false;

        private HexGridComponent gridData;

        [SerializeField] private float stickMinZoom = -250; // units
        [SerializeField] private float stickMaxZoom = -45; // units
        [SerializeField] private float swivelMinZoom = 90; // degrees
        [SerializeField] private float swivelMaxZoom = 45; // degrees
        [SerializeField] private float moveSpeedMinZoom = 100; // mult
        [SerializeField] private float moveSpeedMaxZoom = 100; // mult
        [SerializeField] private float rotationSpeed = 180;

        private void Awake()
        {
            instance = this;
            swivel = transform.GetChild(0);
            stick = swivel.GetChild(0);
        }
        void OnEnable()
        {
            mainWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = mainWorld.EntityManager;
            AdjustZoom(0);
        }

        // Update is called once per frame
        void Update()
        {
            if (GridAPI.ActiveGridEntity == Entity.Null)
            {
                return;
            }
            gridData = entityManager.GetComponentData<HexGridComponent>(GridAPI.ActiveGridEntity);
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
            if (xDelta != 0f || zDelta != 0f|| ForceWrapUpdate)
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
            float3 direction = transform.localRotation * ExtraTurretMathsFunctions.Normalise(new float3(xDelta, 0f, zDelta));
            float damping = math.max(math.abs(xDelta), math.abs(zDelta));
            float distance = math.lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;
            float3 position = transform.localPosition;
            position += direction * distance;
            transform.localPosition = gridData.wrapping ? WrapPosition(position) : ClampPosition(position);
        }

        private float3 ClampPosition(float3 position)
        {
            float xMax = (gridData.cellCountX - 0.5f) * HexFunctions.innerDiameter;
            float zMax = (gridData.cellCountZ - 1f) * (1.5f * HexFunctions.outerRadius);
            position.x = math.clamp(position.x, 0f, xMax);
            position.z = math.clamp(position.z, 0f, zMax);
            return position;
        }

        private float3 WrapPosition(float3 position)
        {
            float width = gridData.cellCountX * HexFunctions.innerDiameter;
            while (position.x < 0f)
            {
                position.x += width;
            }
            while (position.x > width)
            {
                position.x -= width;
            }

            float zMax = (gridData.cellCountZ - 1) * (1.5f * HexFunctions.outerRadius);

            position.z = math.clamp(position.z, 0f, zMax);

            entityManager.AddComponentData(GridAPI.ActiveGridEntity, new CentreMap { Value = position.x });
            return position;
        }

        private void AdjustZoom(float delta)
        {
            zoom = HexFunctions.Clamp01(zoom + delta);
            float distance = math.lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);

            float angle = math.lerp(swivelMinZoom, swivelMaxZoom, zoom);
            swivel.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}