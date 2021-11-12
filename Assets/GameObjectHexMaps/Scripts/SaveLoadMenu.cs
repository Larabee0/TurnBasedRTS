using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace GameObjectHexagons
{
    public class SaveLoadMenu : MenuBase
    {
        const int mapFileVersion = 5;

        public Text menuLabel;
        public Text actionButtonLabel;

        public InputField nameInput;

        public RectTransform listContent;

        public SaveLoadItem itemPrefab;
        public DeleteConfirmMenu deleteConfirmMenu;
        private bool saveMode;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void MenuOpen(bool saveMode)
        {
            this.saveMode = saveMode;
            if (saveMode)
            {
                menuLabel.text = "Save Map";
                actionButtonLabel.text = "Save";
            }
            else
            {
                menuLabel.text = "Load Map";
                actionButtonLabel.text = "Load";
            }
            FillList();
            Open();
        }

        public void Action()
        {
            string path = GetSelectedPath();
            if(path == null)
            {
                return;
            }
            if (saveMode)
            {
                Save(path);
            }
            else
            {
                Load(path);
            }
            Close();
        }

        public void SelectedItem(string name)
        {
            nameInput.text = name;
        }

        public void Save(string path)
        {
            //string path = Path.Combine(Application.persistentDataPath, "test.map");
            using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.OpenOrCreate));
            //writer.
            writer.Write(mapFileVersion);
            hexGrid.Save(writer);

        }

        public void Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.Log("File does not exist " + path);
                return;
            }
            using BinaryReader reader = new BinaryReader(File.OpenRead(path));
            int header = reader.ReadInt32();
            if (header <= mapFileVersion)
            {
                hexGrid.Load(reader, header);
                HexMapCamera.ValidatePosition();
            }
            else
            {
                Debug.LogWarning("Unknown map format " + header);
            }
        }
        public void PreDelete()
        {
            deleteConfirmMenu.OpenMenu(nameInput.text);
        }
        public void Delete()
        {
            string path = GetSelectedPath();
            if(path == null)
            {
                return;
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            nameInput.text = "";
            FillList();
        }

        private void FillList()
        {
            for (int i = 0; i < listContent.childCount; i++)
            {
                Destroy(listContent.GetChild(i).gameObject);
            }
            string[] paths = Directory.GetFiles(Application.persistentDataPath, "*.map");
            Array.Sort(paths);
            for (int i = 0; i < paths.Length; i++)
            {
                SaveLoadItem item = Instantiate(itemPrefab,listContent,false);
                item.menu = this;
                item.MapName = Path.GetFileNameWithoutExtension(paths[i]);
                //item.transform.SetParent(listContent, false);
            }
        }

        private string GetSelectedPath()
        {
            string MapName = nameInput.text;
            if(MapName.Length  == 0)
            {
                return null;
            }
            return Path.Combine(Application.persistentDataPath, MapName + ".map");
        }
    }
}