using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameObjectHexagons
{
    public class MenuBase : MonoBehaviour
    {
        public HexGrid hexGrid;

        public void Open()
        {
            gameObject.SetActive(true);
            HexMapCamera.Locked = true;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            HexMapCamera.Locked = false;
        }
    }
}
