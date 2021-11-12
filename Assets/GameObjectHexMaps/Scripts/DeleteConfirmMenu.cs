using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameObjectHexagons
{


    public class DeleteConfirmMenu :MenuBase
    {

        public SaveLoadMenu saveLoadMenu;
        public Text FileName;
        public void OpenMenu(string fileName)
        {
            Open();
            FileName.text  = "\""+fileName+"\"?";
        }
        public void CloseMenu()
        {
            Close();
            HexMapCamera.Locked = true;
        }
        public void Delete()
        {
            saveLoadMenu.Delete();
            CloseMenu();
        }
    }
}