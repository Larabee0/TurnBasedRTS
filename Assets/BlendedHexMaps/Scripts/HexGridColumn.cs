using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DOTSHexagonsV2
{
    public class HexGridColumn : MonoBehaviour, IEquatable<HexGridColumn>, IComparable<HexGridColumn>
    {
        public int ColumnIndex = int.MinValue;
        public List<HexGridChunk> chunks = new List<HexGridChunk>();

        public void AddChunkToColumn(HexGridChunk chunk)
        {
            chunk.transform.parent = transform;
            chunks.Add(chunk);
        }

        public void RemoveLastChunkColumn()
        {
            chunks.RemoveAt(chunks.Count - 1);
        }

        public void RemoveChunkAt(int index)
        {
            chunks.RemoveAt(index);
        }

        public void RemoveChunk(HexGridChunk chunk)
        {
            chunks.Remove(chunk);
        }

        public void TrimColumnsTo(int TargetCount)
        {
            for (int i = chunks.Count-1; i > TargetCount; i++)
            {
                Destroy(chunks[i].gameObject);
                chunks.RemoveAt(i);
            }
        }

        public bool Equals(HexGridColumn other)
        {
            if (other == null) return false;
            return ColumnIndex == other.ColumnIndex;
        }

        public int CompareTo(HexGridColumn other)
        {
            if (other == null) return 1;
            return ColumnIndex.CompareTo(other.ColumnIndex);
        }

        public override int GetHashCode()
        {
            return ColumnIndex;
        }
    }
}