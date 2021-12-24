using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTSHexagonsV2 {
    public class HexUI : MonoBehaviour
    {
        private GridAPI grid;
        private HexCell currentCell;
        private HexUnit selectedUnit;

        public EntityManager entityManager;
        public HexGridComponent hexGridInfo;
        private DynamicBuffer<HexCell> cells;
        private NativeArray<HexGridChunkBuffer> chunks;
        // Start is called before the first frame update
        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            grid = GridAPI.Instance;
        }

        // Update is called once per frame
        void Update()
        {
            if (!EventSystem.current.IsPointerOverGameObject() || EventSystem.current.IsPointerOverGameObject())
            {
                GetGridData();

                if (Input.GetMouseButtonDown(0))
                {
                    DoSelection();
                }
                else if (selectedUnit)
                {
                    if (Input.GetMouseButtonDown(1))
                    {
                        DoMove();
                    }
                    else
                    {
                        DoPathFinding();
                    }

                }
                chunks.Dispose();
            }
        }

        public void GetGridData()
        {
            hexGridInfo = entityManager.GetComponentData<HexGridComponent>(GridAPI.ActiveGridEntity);
            cells = entityManager.GetBuffer<HexCell>(GridAPI.ActiveGridEntity);
            chunks = entityManager.GetBuffer<HexGridChunkBuffer>(GridAPI.ActiveGridEntity).ToNativeArray(Allocator.Temp);
        }


        private void DoMove()
        {

        }

        private bool UpdateCurrentCell()
        {
            HexCell cell = GetCellUnderCursor();
            if (cell != currentCell)
            {
                currentCell = cell;
                return true;
            }
            return false;
        }

        private HexCell GetCellUnderCursor()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return GetCellFromPosition(hit.point);
            }
            return HexCell.Null;
        }

        private HexCell GetCellFromPosition(float3 point)
        {
            Transform gridTransform = GridAPI.Instance.GridContainer;

            float3 position = gridTransform.InverseTransformPoint(point);
            HexCoordinates coordinates = HexCoordinates.FromPosition(position, hexGridInfo.wrapSize);
            return GetCellFromCoordinates(coordinates);
        }

        private HexCell GetCellFromCoordinates(HexCoordinates coordinates)
        {
            int z = coordinates.Z;
            if (z < 0 || z >= hexGridInfo.cellCountZ)
            {
                return HexCell.Null;
            }
            int x = coordinates.X + z / 2;
            if (x < 0 || x >= hexGridInfo.cellCountX)
            {
                return HexCell.Null;
            }
            return cells[x + z * hexGridInfo.cellCountX];
        }


        private void DoPathFinding()
        {
            if (UpdateCurrentCell())
            {
                if (currentCell && HexCell.IsValidDestination(currentCell))
                {

                }
                else
                {

                }
            }
        }

        private void DoSelection()
        {
            UpdateCurrentCell();
            if (currentCell)
            {

            }
        }

    }
}