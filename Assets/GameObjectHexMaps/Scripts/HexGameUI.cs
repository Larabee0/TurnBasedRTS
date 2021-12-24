using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameObjectHexagons {
    public class HexGameUI : MonoBehaviour
    {
        public HexGrid grid;

        private HexCell currentCell;
        private HexUnit selectedUnit;

        // Start is called before the first frame update
        void Start()
        {
            SetEditMode(true);
            grid.ClearPathAggressive();
        }

        private void Update()
        {
            if (!EventSystem.current.IsPointerOverGameObject()|| EventSystem.current.IsPointerOverGameObject())
            {
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
            }
        }

        private bool UpdateCurrentCell()
        {
            HexCell cell =  grid.GetCell(Camera.main.ScreenPointToRay(Input.mousePosition));
            if(cell!= currentCell)
            {
                currentCell = cell;
                return true;
            }
            return false;
        }

        private void DoMove()
        {
            Debug.Log(grid.HasPath);
            if (grid.HasPath)
            {
                
                selectedUnit.Travel(grid.GetPath());
                grid.ClearPath();
            }
        }

        private void DoPathFinding()
        {
            if (UpdateCurrentCell())
            {
                if (currentCell && selectedUnit.IsValidDestination(currentCell))
                {
                    grid.FindPath(selectedUnit.Location, currentCell, selectedUnit);
                }
                else
                {
                    grid.ClearPath();
                }
            }
        }

        private void DoSelection()
        {
            grid.ClearPath();
            UpdateCurrentCell();
            if (currentCell)
            {
                selectedUnit = currentCell.Unit;
            }
        }

        public void SetEditMode(bool toggle)
        {
            enabled = !toggle;
            grid.ShowUI(!toggle);
            grid.ClearPath();
            if (toggle)
            {
                Shader.EnableKeyword("BOOLEAN_B964DA9E23BC467FA33B192E46E0502F_ON");
            }
            else
            {
                Shader.DisableKeyword("BOOLEAN_B964DA9E23BC467FA33B192E46E0502F_ON");
            }
        }
    }
}