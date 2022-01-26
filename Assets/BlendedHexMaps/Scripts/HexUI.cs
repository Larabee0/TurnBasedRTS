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
        public LayerMask unitLayerMask;

        public EntityManager entityManager;
        public HexGridComponent hexGridInfo;
        private DynamicBuffer<HexCell> cells;
        private NativeArray<HexGridChunkBuffer> chunks;

        private CellHighlightManager pathVisual;
        public NativeArray<HexCell> path;

        // Start is called before the first frame update
        void Start()
        {
            SetEditMode(true);
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            grid = GridAPI.Instance;
            pathVisual = new CellHighlightManager(grid);
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
                        //DoMove();
                    }
                    else
                    {
                        DoPathFinding();
                    }

                }
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
            if (entityManager.HasComponent<FoundPath>(selectedUnit.Entity))
            {
                DynamicBuffer<HexCellQueueElement> path = entityManager.GetBuffer<HexCellQueueElement>(selectedUnit.Entity);
                // get buffer & set up movement system.
            }
            
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

        private HexUnit GetUnitUnderCursor()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitLayerMask))
            {
                return hit.collider.gameObject.GetComponentInParent<HexUnit>();
            }
            return null;
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
                if (entityManager.HasComponent<FindPath>(selectedUnit.Entity))
                {
                    return;
                }
                if (entityManager.HasComponent<FoundPath>(selectedUnit.Entity))
                {
                    if (currentCell && HexCell.IsValidDestination(currentCell))
                    {
                        if (entityManager.HasComponent<HexToCell>(selectedUnit.Entity))
                        {
                            if (entityManager.GetComponentData<HexToCell>(selectedUnit.Entity) != currentCell || entityManager.GetComponentData<HexFromCell>(selectedUnit.Entity) != (HexCell)entityManager.GetComponentData<HexUnitLocation>(selectedUnit.Entity))
                            {
                                Debug.Log("Path Changed. Scheduling path finding...");
                                entityManager.SetComponentData<HexFromCell>(selectedUnit.Entity, (HexCell)entityManager.GetComponentData<HexUnitLocation>(selectedUnit.Entity));
                                entityManager.SetComponentData<HexToCell>(selectedUnit.Entity, currentCell);
                                entityManager.AddComponentData(selectedUnit.Entity, new FindPath { Options = entityManager.GetComponentData<HexUnitComp>(selectedUnit.Entity) });
                                entityManager.RemoveComponent(selectedUnit.Entity, new ComponentTypes(typeof(FoundPath), typeof(NotFoundPath)));
                                return;
                            }
                        }
                    }
                    Debug.Log("Path recieved.");
                    DynamicBuffer<HexCell> path = entityManager.GetBuffer<HexCell>(selectedUnit.Entity);
                    RecievePathFinding(path);
                }
                else if (currentCell && HexCell.IsValidDestination(currentCell))
                {
                    Debug.Log("Scheduling path finding...");
                    if (entityManager.HasComponent<HexToCell>(selectedUnit.Entity))
                    {
                        if (entityManager.GetComponentData<HexToCell>(selectedUnit.Entity) != currentCell || entityManager.GetComponentData<HexFromCell>(selectedUnit.Entity) != (HexCell)entityManager.GetComponentData<HexUnitLocation>(selectedUnit.Entity))
                        {
                            entityManager.SetComponentData<HexFromCell>(selectedUnit.Entity, (HexCell)entityManager.GetComponentData<HexUnitLocation>(selectedUnit.Entity));
                            entityManager.SetComponentData<HexToCell>(selectedUnit.Entity, currentCell);
                            entityManager.AddComponentData(selectedUnit.Entity, new FindPath { Options = entityManager.GetComponentData<HexUnitComp>(selectedUnit.Entity) });
                        }
                    }
                    else
                    {
                        entityManager.AddComponentData(selectedUnit.Entity, new HexFromCell { Cell = entityManager.GetComponentData<HexUnitLocation>(selectedUnit.Entity) });
                        entityManager.AddComponentData(selectedUnit.Entity, new HexToCell { Cell = currentCell });
                        entityManager.AddComponentData(selectedUnit.Entity, new FindPath { Options = entityManager.GetComponentData<HexUnitComp>(selectedUnit.Entity) });
                    }
                    entityManager.RemoveComponent(selectedUnit.Entity, new ComponentTypes(typeof(FoundPath), typeof(NotFoundPath)));
                }
                else
                {
                    entityManager.RemoveComponent(selectedUnit.Entity, new ComponentTypes(typeof(HexFromCell), typeof(HexToCell), typeof(FoundPath), typeof(NotFoundPath), typeof(FindPath)));
                    ClearPath();
                }
            }
        }

        public void RecievePathFinding(DynamicBuffer<HexCell> pathCells)
        {
            path = new NativeArray<HexCell>(pathCells.AsNativeArray(), Allocator.Persistent);
            pathVisual.ShowPath(path);
        }

        public void ClearPath()
        {
            Debug.Log("Clearing Path");
            pathVisual.SetEnabledAll();
            try
            {
                path.Dispose();
            }
            catch { }
        }

        private void DoSelection()
        {
            UpdateCurrentCell();
            if (currentCell)
            {
                selectedUnit = GetUnitUnderCursor();
                Debug.Log("Unit exists: " + (bool)selectedUnit);
            }
        }

        public void SetEditMode(bool toggle)
        {
            enabled = !toggle;
            //grid.ShowUI(!toggle);
            //grid.ClearPath();
            //if (toggle)
            //{
            //    Shader.EnableKeyword("BOOLEAN_B964DA9E23BC467FA33B192E46E0502F_ON");
            //}
            //else
            //{
            //    Shader.DisableKeyword("BOOLEAN_B964DA9E23BC467FA33B192E46E0502F_ON");
            //}
        }

        private void OnDestroy()
        {
            try
            {
                path.Dispose();
            }
            catch { }
        }
    }
}