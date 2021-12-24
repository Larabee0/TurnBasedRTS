using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DOTSHexagonsV2
{
    public class HexUnit : MonoBehaviour
    {
        private Entity entity;
        public bool IsActive { get { return gameObject.activeInHierarchy; } set { gameObject.SetActive(value); } }
        public Entity Entity { get { return entity; } set { if (value != entity) { entity = value; } } }
    }
}