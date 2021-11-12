using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameObjectHexagons
{
    public class SaveLoadItem : MonoBehaviour
    {
        public SaveLoadMenu menu;
        public string MapName
        {
            get
            {
                return MapName;
            }
            set
            {
                mapName = value;
                transform.GetComponentInChildren<Text>().text = value;
            }
        }

        private string mapName;

        public void Select()
        {
            menu.SelectedItem(mapName);
        }
    }
}