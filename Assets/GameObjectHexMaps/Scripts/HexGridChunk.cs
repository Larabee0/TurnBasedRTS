using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace GameObjectHexagons
{
	public class HexGridChunk : MonoBehaviour
	{
		static Color weights1 = new Color(1f, 0f, 0f);
		static Color weights2 = new Color(0f, 1f, 0f);
		static Color weights3 = new Color(0f, 0f, 1f);

		HexCell[] cells;

		public HexMesh terrian;
		public HexMesh rivers;
		public HexMesh roads;
		public HexMesh water;
		public HexMesh waterShore;
		public HexMesh estuaries;
		public HexFeatureManager features;
		Canvas gridCanvas;

		private void Awake()
		{
			gridCanvas = GetComponentInChildren<Canvas>();

			cells = new HexCell[HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ];
			ShowUI(false);
		}

		public void AddCell(int index, HexCell cell)
		{
			cells[index] = cell;
			cell.chunk = this;
			cell.transform.SetParent(transform, false);
			cell.uiRect.SetParent(gridCanvas.transform, false);
		}

		public void Refresh()
		{
			enabled = true;
		}

		public void ShowUI(bool visible)
		{
			gridCanvas.gameObject.SetActive(visible);
		}

		private void LateUpdate()
		{
			Triangulate();
			enabled = false;
		}

		public void Triangulate()
		{
			terrian.Clear();
			rivers.Clear();
			roads.Clear();
			water.Clear();
			waterShore.Clear();
			estuaries.Clear();
			features.Clear();
			for (int i = 0; i < cells.Length; i++)
			{
				Triangulate(cells[i]);
			}
			terrian.Apply();
			rivers.Apply();
			roads.Apply();
			water.Apply();
			waterShore.Apply();
			estuaries.Apply();
			features.Apply();
		}

		private void Triangulate(HexCell cell)
		{
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				Triangulate(d, cell);
			}
            if (!cell.IsUnderwater)
            {
				if (!cell.HasRiver && !cell.HasRoads)
				{
					features.AddFeature(cell, cell.Position);
				}
				if (cell.IsSpeical)
				{
					features.AddSpecialFeature(cell, cell.Position);
				}
			}
			
		}

		private void Triangulate(HexDirection direction, HexCell cell)
		{
			Vector3 centre = cell.Position;
			EdgeVertices e = new EdgeVertices(centre + HexMetrics.GetFirstSolidCorner(direction), centre + HexMetrics.GetSecondSolidCorner(direction));

			if (cell.HasRiver)
			{
				if (cell.HasRiverThroughEdge(direction))
				{
					e.v3.y = cell.StreamBedY;
					if (cell.HasRiverBeginOrEnd)
					{
						TriangulateWithRiverBeginOrEnd(cell, centre, e);
					}
					else
					{
						TriangulateWithRiver(direction, cell, centre, e);
					}
				}
				else
				{
					TriangulateAdjacentToRiver(direction, cell, centre, e);
				}
			}
			else
			{
				TriangulateWithoutRiver(direction, cell, centre, e);
				if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
				{
					features.AddFeature(cell, (centre + e.v1 + e.v5) * (1f / 3f));
				}
			}

			if (direction <= HexDirection.SE)
			{
				TriangulateConnection(direction, cell, e);
			}
            if (cell.IsUnderwater)
            {
				TriangulateWater(direction, cell, centre);
			}
		}

		private void TriangulateConnection(HexDirection direction, HexCell cell, EdgeVertices e1)
		{
			HexCell neighbour = cell.GetNeighbour(direction);
			if (neighbour == null)
			{
				return;
			}

			Vector3 bridge = HexMetrics.GetBridge(direction);
			bridge.y = neighbour.Position.y - cell.Position.y;
			EdgeVertices e2 = new EdgeVertices(e1.v1 + bridge, e1.v5 + bridge);
			bool hasRiver = cell.HasRiverThroughEdge(direction);
			bool hasRoad = cell.HasRoadThroughEdge(direction);
			if (hasRiver)
			{
				e2.v3.y = neighbour.StreamBedY;
				float3 indices = new float3(cell.Index);
				indices.y = neighbour.Index;
				if (!cell.IsUnderwater)
				{
                    if (!neighbour.IsUnderwater)
                    {
						TriangulateRiverQuad(e1.v2, e1.v4, e2.v2, e2.v4, cell.RiverSurfaceY, neighbour.RiverSurfaceY, 0.8f, cell.HasIncomingRiver && cell.IncomingRiver == direction, indices);
					}
					else if (cell.Elevation > neighbour.WaterLevel)
                    {
						TriangulateWaterfallInWater(e1.v2, e1.v4, e2.v2, e2.v4, cell.RiverSurfaceY, neighbour.RiverSurfaceY, neighbour.WaterSurfaceY, indices);
					}
				}
				else if (!neighbour.IsUnderwater && neighbour.Elevation > cell.WaterLevel)
				{
					TriangulateWaterfallInWater(e2.v4, e2.v2, e1.v4, e1.v2, neighbour.RiverSurfaceY, cell.RiverSurfaceY, cell.WaterSurfaceY, indices);
				}
			}

			if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
			{
				TriangulateEdgeTerraces(e1, cell, e2, neighbour, hasRoad);
			}
			else
			{
				TriangulateEdgeStrip(e1, weights1, cell.Index, e2, weights2, neighbour.Index, hasRoad);
			}

			features.AddWall(e1, cell, e2, neighbour, hasRiver, hasRoad);

			HexCell nextNeighbour = cell.GetNeighbour(direction.Next());
			if (direction <= HexDirection.E && nextNeighbour != null)
			{
				Vector3 v5 = e1.v5 + HexMetrics.GetBridge(direction.Next());
				v5.y = nextNeighbour.Position.y;

				if (cell.Elevation <= neighbour.Elevation)
				{
					if (cell.Elevation <= nextNeighbour.Elevation)
					{
						TriangulateCorner(e1.v5, cell, e2.v5, neighbour, v5, nextNeighbour);
					}
					else
					{
						TriangulateCorner(v5, nextNeighbour, e1.v5, cell, e2.v5, neighbour);
					}
				}
				else if (neighbour.Elevation <= nextNeighbour.Elevation)
				{
					TriangulateCorner(e2.v5, neighbour, v5, nextNeighbour, e1.v5, cell);
				}
				else
				{
					TriangulateCorner(v5, nextNeighbour, e1.v5, cell, e2.v5, neighbour);
				}
			}
		}

		private void TriangulateEdgeStrip(EdgeVertices e1, Color w1, float index1, EdgeVertices e2, Color w2, float index2, bool hasRoad = false)
		{
			terrian.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
			terrian.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
			terrian.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
			terrian.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);

			float3 indices = new float3(index1);
			indices.y = index2;
			terrian.AddQuadCellData(indices, w1, w2);
			terrian.AddQuadCellData(indices, w1, w2);
			terrian.AddQuadCellData(indices, w1, w2);
			terrian.AddQuadCellData(indices, w1, w2);

			if (hasRoad)
			{
				TriangulateRoadSegment(e1.v2, e1.v3, e1.v4, e2.v2, e2.v3, e2.v4, w1, w2, indices);
			}
		}

		private void TriangulateEdgeFan(Vector3 centre, EdgeVertices edge, float index)
		{
			float3 indices = new float3(index);

			terrian.AddTriangle(centre, edge.v1, edge.v2);
			terrian.AddTriangle(centre, edge.v2, edge.v3);
			terrian.AddTriangle(centre, edge.v3, edge.v4);
			terrian.AddTriangle(centre, edge.v4, edge.v5);

			terrian.AddTriangleCellData(indices, weights1);
			terrian.AddTriangleCellData(indices, weights1);
			terrian.AddTriangleCellData(indices, weights1);
			terrian.AddTriangleCellData(indices, weights1);
		}

		private void TriangulateEdgeTerraces(EdgeVertices begin, HexCell beginCell, EdgeVertices end, HexCell endCell, bool hasRoad)
		{
			EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
			Color w2 = HexMetrics.TerraceLerp(weights1, weights2, 1);
			float i1 = beginCell.Index;
			float i2 = endCell.Index;

			TriangulateEdgeStrip(begin, weights1, i1, e2, w2, i2, hasRoad);

			for (int i = 2; i < HexMetrics.terraceSteps; i++)
			{
				EdgeVertices e1 = e2;
				Color w1 = w2;
				e2 = EdgeVertices.TerraceLerp(begin, end, i);
				w2 = HexMetrics.TerraceLerp(weights1, weights2, i);
				TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
			}

			TriangulateEdgeStrip(e2, w2, i1, end, weights2, i2, hasRoad);
		}

		private void TriangulateCorner(Vector3 bottom, HexCell bottomCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
		{
			HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
			HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

			if (leftEdgeType == HexEdgeType.Slope)
			{
				if (rightEdgeType == HexEdgeType.Slope)
				{
					TriangulateCornerTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
				}
				else if (rightEdgeType == HexEdgeType.Flat)
				{
					TriangulateCornerTerraces(left, leftCell, right, rightCell, bottom, bottomCell);
				}
				else
				{
					TriangulateCornerTerracesCliff(bottom, bottomCell, left, leftCell, right, rightCell);
				}
			}
			else if (rightEdgeType == HexEdgeType.Slope)
			{
				if (leftEdgeType == HexEdgeType.Flat)
				{
					TriangulateCornerTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
				}
				else
				{
					TriangulateCornerCliffTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
				}
			}
			else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
			{
				if (leftCell.Elevation < rightCell.Elevation)
				{
					TriangulateCornerCliffTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
				}
				else
				{
					TriangulateCornerTerracesCliff(left, leftCell, right, rightCell, bottom, bottomCell);
				}
			}
			else
			{
				terrian.AddTriangle(bottom, left, right);
				float3 indices = new float3(bottomCell.Index, leftCell.Index, rightCell.Index);
				terrian.AddTriangleCellData(indices,weights1, weights2, weights3);
				//terrian.AddTriangleTerrianTypes(indices);
			}
			features.AddWall(bottom, bottomCell, left, leftCell, right, rightCell);
		}

		private void TriangulateCornerTerraces(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
		{
			Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
			Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
			Color w3 = HexMetrics.TerraceLerp(weights1, weights2, 1);
			Color w4 = HexMetrics.TerraceLerp(weights1, weights3, 1);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);

			terrian.AddTriangle(begin, v3, v4);
			terrian.AddTriangleCellData(indices, weights1, w3, w4);

			for (int i = 2; i < HexMetrics.terraceSteps; i++)
			{
				Vector3 v1 = v3;
				Vector3 v2 = v4;
				Color w1 = w3;
				Color w2 = w4;
				v3 = HexMetrics.TerraceLerp(begin, left, i);
				v4 = HexMetrics.TerraceLerp(begin, right, i);
				w3 = HexMetrics.TerraceLerp(weights1, weights2, i);
				w4 = HexMetrics.TerraceLerp(weights1, weights3, i);
				terrian.AddQuad(v1, v2, v3, v4);
				terrian.AddQuadCellData(indices, w1, w2, w3, w4);
			}

			terrian.AddQuad(v3, v4, left, right);
			terrian.AddQuadCellData(indices, w3, w4, weights2, weights3);
		}

		private void TriangulateCornerTerracesCliff(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
		{
			float b = 1f / (rightCell.Elevation - beginCell.Elevation);
			if (b < 0)
			{
				b = -b;
			}
			Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(right), b);
			Color boundaryWeight = Color.Lerp(weights1, weights3, b);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);
			TriangulateBoundaryTriangle(begin, weights1, left, weights2, boundary, boundaryWeight, indices);

			if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
			{
				TriangulateBoundaryTriangle(left, weights2, right, weights3, boundary, boundaryWeight, indices);
			}
			else
			{
				terrian.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
				terrian.AddTriangleCellData(indices,weights2, weights3, boundaryWeight);
			}
		}

		private void TriangulateCornerCliffTerraces(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
		{
			float b = 1f / (leftCell.Elevation - beginCell.Elevation);
			if (b < 0)
			{
				b = -b;
			}
			Vector3 boundary = Vector3.Lerp(HexMetrics.Perturb(begin), HexMetrics.Perturb(left), b);
			Color boundaryWeights = Color.Lerp(weights1, weights2, b);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);

			TriangulateBoundaryTriangle(right, weights3, begin, weights1, boundary, boundaryWeights, indices);

			if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
			{
				TriangulateBoundaryTriangle(left, weights2, right, weights3, boundary, boundaryWeights, indices);
			}
			else
			{
				terrian.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
				terrian.AddTriangleCellData(indices,weights2, weights3, boundaryWeights);
				//terrian.AddTriangleTerrianTypes(indices);
			}
		}

		private void TriangulateBoundaryTriangle(Vector3 begin, Color beginWeights, Vector3 left, Color leftWeights, Vector3 boundary, Color boundaryWeights, Vector3 indices)
		{
			Vector3 v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));
			Color w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, 1);

			terrian.AddTriangleUnperturbed(HexMetrics.Perturb(begin), v2, boundary);
			terrian.AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);

			for (int i = 2; i < HexMetrics.terraceSteps; i++)
			{
				Vector3 v1 = v2;
				Color w1 = w2;
				v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
				w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, i);
				terrian.AddTriangleUnperturbed(v1, v2, boundary);
				terrian.AddTriangleCellData(indices,w1, w2, boundaryWeights);
			}

			terrian.AddTriangleUnperturbed(v2, HexMetrics.Perturb(left), boundary);
			terrian.AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
		}

		private void TriangulateWithRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
		{
			Vector3 centreL;
			Vector3 centreR;

			if (cell.HasRiverThroughEdge(direction.Opposite()))
			{
				centreL = centre + HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
				centreR = centre + HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
			}
			else if (cell.HasRiverThroughEdge(direction.Next()))
			{
				centreL = centre;
				centreR = Vector3.Lerp(centre, e.v5, 2f / 3f);
			}
			else if (cell.HasRiverThroughEdge(direction.Previous()))
			{
				centreL = Vector3.Lerp(centre, e.v1, 2f / 3f);
				centreR = centre;
			}
			else if (cell.HasRiverThroughEdge(direction.Next2()))
			{
				centreL = centre;
				centreR = centre + HexMetrics.GetSolidEdgeMiddle(direction.Next()) * (0.5f * HexMetrics.innerToOuter);
			}
			else
			{
				centreL = centre + HexMetrics.GetSolidEdgeMiddle(direction.Previous()) * (0.5f * HexMetrics.innerToOuter);
				centreR = centre;
			}
			centre = Vector3.Lerp(centreL, centreR, 0.5f);

			EdgeVertices m = new EdgeVertices(Vector3.Lerp(centreL, e.v1, 0.5f), Vector3.Lerp(centreR, e.v5, 0.5f), 1f / 6f);
			m.v3.y = centre.y = e.v3.y;

			TriangulateEdgeStrip(m, weights1, cell.Index, e, weights1, cell.Index);
			float3 indices = new float3(cell.Index);
			terrian.AddTriangle(centreL, m.v1, m.v2);
			terrian.AddQuad(centreL, centre, m.v2, m.v3);
			terrian.AddQuad(centre, centreR, m.v3, m.v4);
			terrian.AddTriangle(centreR, m.v4, m.v5);

			terrian.AddTriangleCellData(indices, weights1);
			terrian.AddQuadCellData(indices,weights1);
			terrian.AddQuadCellData(indices, weights1);
			terrian.AddTriangleCellData(indices, weights1);

			if (!cell.IsUnderwater)
			{
				bool reversed = cell.IncomingRiver == direction;
				TriangulateRiverQuad(centreL, centreR, m.v2, m.v4, cell.RiverSurfaceY, 0.4f, reversed, indices);
				TriangulateRiverQuad(m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed, indices);
			}
		}

		private void TriangulateWithRiverBeginOrEnd(HexCell cell, Vector3 centre, EdgeVertices e)
		{
			EdgeVertices m = new EdgeVertices(Vector3.Lerp(centre, e.v1, 0.5f), Vector3.Lerp(centre, e.v5, 0.5f));
			m.v3.y = e.v3.y;

			TriangulateEdgeStrip(m, weights1, cell.Index, e, weights1, cell.Index);
			TriangulateEdgeFan(centre, m, cell.Index);

            if (!cell.IsUnderwater)
            {
				bool reversed = cell.HasIncomingRiver;
				float3 indices = new float3(cell.Index);
				TriangulateRiverQuad(m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed, indices);
				centre.y = m.v2.y = m.v4.y = cell.RiverSurfaceY;
				rivers.AddTriangle(centre, m.v2, m.v4);
				if (reversed)
				{
					rivers.AddTriangleUV(new Vector2(0.5f, 0.4f), new Vector2(1f, 0.2f), new Vector2(0f, 0.2f));
				}
				else
				{
					rivers.AddTriangleUV(new Vector2(0.5f, 0.4f), new Vector2(0f, 0.6f), new Vector2(1f, 0.6f));
				}
				rivers.AddTriangleCellData(indices, weights1);
			}
			
		}

		private void TriangulateWithoutRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
		{
			TriangulateEdgeFan(centre, e, cell.Index);

			if (cell.HasRoads)
			{
				Vector2 interpolators = GetRoadInterpolators(direction, cell);
				TriangulateRoad(centre, Vector3.Lerp(centre, e.v1, interpolators.x), Vector3.Lerp(centre, e.v5, interpolators.y), e, cell.HasRoadThroughEdge(direction), cell.Index);
			}
		}

		private void TriangulateAdjacentToRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
		{
			if (cell.HasRoads)
			{
				TriangulateRoadAdjacentToRiver(direction, cell, centre, e);
			}

			if (cell.HasRiverThroughEdge(direction.Next()))
			{
				if (cell.HasRiverThroughEdge(direction.Previous()))
				{
					centre += HexMetrics.GetSolidEdgeMiddle(direction) * (HexMetrics.innerToOuter * 0.5f);
				}
				else if (cell.HasRiverThroughEdge(direction.Previous2()))
				{
					centre += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
				}
			}
			else if (cell.HasRiverThroughEdge(direction.Previous()) && cell.HasRiverThroughEdge(direction.Next2()))
			{
				centre += HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
			}

			EdgeVertices m = new EdgeVertices(Vector3.Lerp(centre, e.v1, 0.5f), Vector3.Lerp(centre, e.v5, 0.5f));

			TriangulateEdgeStrip(m, weights1, cell.Index, e, weights1, cell.Index);
			TriangulateEdgeFan(centre, m, cell.Index);
			if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
            {
				features.AddFeature(cell,(centre + e.v1 + e.v5) * (1f / 3f));
            }
		}

		private void TriangulateRiverQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y, float v, bool reversed,Vector3 indices)
		{
			TriangulateRiverQuad(v1, v2, v3, v4, y, y, v, reversed, indices);
		}

		private void TriangulateRiverQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y1, float y2, float v, bool reversed, Vector3 indices)
		{
			v1.y = v2.y = y1;
			v3.y = v4.y = y2;
			rivers.AddQuad(v1, v2, v3, v4);
			if (reversed)
			{
				rivers.AddQuadUV(1f, 0f, 0.8f - v, 0.6f - v);
			}
			else
			{
				rivers.AddQuadUV(0f, 1f, v, v + 0.2f);
			}
			rivers.AddQuadCellData(indices, weights1, weights2);
		}

		private void TriangulateRoad(Vector3 centre, Vector3 mL, Vector3 mR, EdgeVertices e, bool hasRoadThroughCellEdge, float index)
		{
			if (hasRoadThroughCellEdge)
			{
				float3 indices = new float3(index);
				Vector3 mC = Vector3.Lerp(mL, mR, 0.5f);
				TriangulateRoadSegment(mL, mC, mR, e.v2, e.v3, e.v4, weights1, weights1, indices);
				roads.AddTriangle(centre, mL, mC);
				roads.AddTriangle(centre, mC, mR);
				roads.AddTriangleUV(new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f));
				roads.AddTriangleUV(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
				roads.AddTriangleCellData(indices, weights1);
				roads.AddTriangleCellData(indices, weights1);
			}
			else
			{
				TriangulateRoadEdge(centre, mL, mR,index);
			}
		}

		private void TriangulateRoadSegment(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, Vector3 v5, Vector3 v6, Color w1, Color w2, Vector3 indices)
		{
			roads.AddQuad(v1, v2, v4, v5);
			roads.AddQuad(v2, v3, v5, v6);
			roads.AddQuadUV(0f, 1f, 0f, 0f);
			roads.AddQuadUV(1f, 0f, 0f, 0f);
			roads.AddQuadCellData(indices, w1, w2);
			roads.AddQuadCellData(indices, w1, w2);
		}

		private void TriangulateRoadEdge(Vector3 centre, Vector3 mL, Vector3 mR,float index)
		{
			roads.AddTriangle(centre, mL, mR);
			roads.AddTriangleUV(new Vector2(1f, 0), Vector2.zero, Vector2.zero);
			float3 indices = new float3(index);
			roads.AddTriangleCellData(indices, weights1);
		}

		private void TriangulateRoadAdjacentToRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
		{
			bool hasRoadThroughEdge = cell.HasRoadThroughEdge(direction);
			bool previousHasRiver = cell.HasRiverThroughEdge(direction.Previous());
			bool nextHasRiver = cell.HasRiverThroughEdge(direction.Next());
			Vector2 interpolators = GetRoadInterpolators(direction, cell);
			Vector3 roadCentre = centre;

			if (cell.HasRiverBeginOrEnd)
			{
				roadCentre += HexMetrics.GetSolidEdgeMiddle(cell.RiverBeginOrEndDirection.Opposite()) * (1f / 3f);
			}
			else if (cell.IncomingRiver == cell.OutgoingRiver.Opposite())
			{
				Vector3 corner;
				if (previousHasRiver)
				{
					if (!hasRoadThroughEdge && !cell.HasRoadThroughEdge(direction.Next()))
					{
						return;
					}
					corner = HexMetrics.GetSecondSolidCorner(direction);
				}
				else
				{
					if (!hasRoadThroughEdge && !cell.HasRoadThroughEdge(direction.Previous()))
					{
						return;
					}
					corner = HexMetrics.GetFirstSolidCorner(direction);
				}
				roadCentre += corner * 0.5f;

				if (cell.IncomingRiver == direction.Next() && (cell.HasRoadThroughEdge(direction.Next2()) || cell.HasRoadThroughEdge(direction.Opposite())))
				{
					features.AddBridge(roadCentre, centre - corner * 0.5f);
				}
				
				centre += corner * 0.25f;
			}
			else if (cell.IncomingRiver == cell.OutgoingRiver.Previous())
			{
				roadCentre -= HexMetrics.GetSecondCorner(cell.IncomingRiver) * 0.2f;
			}
			else if (cell.IncomingRiver == cell.OutgoingRiver.Next())
			{
				roadCentre -= HexMetrics.GetFirstCorner(cell.IncomingRiver) * 0.2f;
			}
			else if (previousHasRiver && nextHasRiver)
			{
				if (!hasRoadThroughEdge)
				{
					return;
				}
				Vector3 offset = HexMetrics.GetSolidEdgeMiddle(direction) * HexMetrics.innerToOuter;
				roadCentre += offset * 0.7f;
				centre += offset * 0.5f;
			}
			else
			{
				HexDirection middle;
				if (previousHasRiver)
				{
					middle = direction.Next();
				}
				else if (nextHasRiver)
				{
					middle = direction.Previous();
				}
				else
				{
					middle = direction;
				}
				if (!cell.HasRoadThroughEdge(middle) && !cell.HasRoadThroughEdge(middle.Previous()) && !cell.HasRoadThroughEdge(middle.Next()))
				{
					return;
				}
				Vector3 offset = HexMetrics.GetSolidEdgeMiddle(middle);
				roadCentre += offset * 0.25f;
				if (direction == middle && cell.HasRoadThroughEdge(direction.Opposite()))
                {
					features.AddBridge(roadCentre, centre - offset * (HexMetrics.innerToOuter * 0.7f));
				}
				
			}

			Vector3 mL = Vector3.Lerp(roadCentre, e.v1, interpolators.x);
			Vector3 mR = Vector3.Lerp(roadCentre, e.v5, interpolators.y);
			TriangulateRoad(roadCentre, mL, mR, e, hasRoadThroughEdge, cell.Index);
			if (previousHasRiver)
			{
				TriangulateRoadEdge(roadCentre, centre, mL, cell.Index);
			}
			if (nextHasRiver)
			{
				TriangulateRoadEdge(roadCentre, mR, centre, cell.Index);
			}
		}

		private Vector2 GetRoadInterpolators(HexDirection direction, HexCell cell)
		{
			Vector2 interpolators;
			if (cell.HasRoadThroughEdge(direction))
			{
				interpolators.x = interpolators.y = 0.5f;
			}
			else
			{
				interpolators.x = cell.HasRoadThroughEdge(direction.Previous()) ? 0.5f : 0.25f;
				interpolators.y = cell.HasRoadThroughEdge(direction.Next()) ? 0.5f : 0.25f;
			}
			return interpolators;
		}

		private void TriangulateWater(HexDirection direction, HexCell cell,Vector3 centre)
        {
			centre.y = cell.WaterSurfaceY;
			HexCell neighbour = cell.GetNeighbour(direction);
			if (neighbour != null && !neighbour.IsUnderwater)
			{
				TriangulateWaterShore(direction, cell, neighbour, centre);
			}
			else
			{
				TriangulateOpenWater(direction, cell, neighbour, centre);
			}
		}

        private void TriangulateWaterShore(HexDirection direction, HexCell cell, HexCell neighbour, Vector3 centre)
        {
			EdgeVertices e1 = new EdgeVertices(centre + HexMetrics.GetFirstWaterCorner(direction), centre + HexMetrics.GetSecondWaterCorner(direction));
			float3 indices = new float3(cell.Index);
			indices.y = neighbour.Index;
			water.AddTriangle(centre, e1.v1, e1.v2);
			water.AddTriangle(centre, e1.v2, e1.v3);
			water.AddTriangle(centre, e1.v3, e1.v4);
			water.AddTriangle(centre, e1.v4, e1.v5);
			water.AddTriangleCellData(indices, weights1);
			water.AddTriangleCellData(indices, weights1);
			water.AddTriangleCellData(indices, weights1);
			water.AddTriangleCellData(indices, weights1);

			Vector3 centre2 = neighbour.Position;
            if (neighbour.ColumnIndex < cell.ColumnIndex - 1)
            {
				centre2.x += HexMetrics.wrapSize * HexMetrics.innerDiameter;
            }
			else if (neighbour.ColumnIndex > cell.ColumnIndex + 1)
            {
				centre2.x -= HexMetrics.wrapSize * HexMetrics.innerDiameter;
			}

			centre2.y = centre.y;
			EdgeVertices e2 = new EdgeVertices(centre2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()), centre2 + HexMetrics.GetFirstSolidCorner(direction.Opposite()));

            if (cell.HasRiverThroughEdge(direction))
            {
				TriangulateEstruary(e1, e2, cell.IncomingRiver == direction, indices);
            }
            else
			{
				waterShore.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
				waterShore.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
				waterShore.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
				waterShore.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);

				waterShore.AddQuadUV(0f, 0f, 0f, 1f);
				waterShore.AddQuadUV(0f, 0f, 0f, 1f);
				waterShore.AddQuadUV(0f, 0f, 0f, 1f);
				waterShore.AddQuadUV(0f, 0f, 0f, 1f);
				waterShore.AddQuadCellData(indices, weights1, weights2);
				waterShore.AddQuadCellData(indices, weights1, weights2);
				waterShore.AddQuadCellData(indices, weights1, weights2);
				waterShore.AddQuadCellData(indices, weights1, weights2);
			}

			HexCell nextNeighbor = cell.GetNeighbour(direction.Next());
			if (nextNeighbor != null)
			{
				Vector3 centre3 = nextNeighbor.Position;
                if (nextNeighbor.ColumnIndex < cell.ColumnIndex - 1)
                {
					centre3.x += HexMetrics.wrapSize * HexMetrics.innerDiameter;
				}
				else if (nextNeighbor.ColumnIndex > cell.ColumnIndex + 1)
				{
					centre3.x -= HexMetrics.wrapSize * HexMetrics.innerDiameter;
				}
				Vector3 v3 = centre3 + (nextNeighbor.IsUnderwater ? HexMetrics.GetFirstWaterCorner(direction.Previous()) : HexMetrics.GetFirstSolidCorner(direction.Previous()));
				v3.y = centre.y;
				waterShore.AddTriangle(e1.v5, e2.v5, v3);
				waterShore.AddTriangleUV(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, nextNeighbor.IsUnderwater ? 0f : 1f));
				waterShore.AddTriangleCellData(indices, weights1, weights2, weights3);
			}
		}

		private void TriangulateEstruary(EdgeVertices e1, EdgeVertices e2, bool incomingRiver,Vector3 indices)
		{
			waterShore.AddTriangle(e2.v1, e1.v2, e1.v1);
			waterShore.AddTriangle(e2.v5, e1.v5, e1.v4);
			
			waterShore.AddTriangleUV(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
			waterShore.AddTriangleUV(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
			
			waterShore.AddTriangleCellData(indices, weights2, weights1, weights1);
			waterShore.AddTriangleCellData(indices, weights2, weights1, weights1);


			estuaries.AddQuad(e2.v1, e1.v2, e2.v2, e1.v3);
			estuaries.AddTriangle(e1.v3, e2.v2, e2.v4);
			estuaries.AddQuad(e1.v3, e1.v4, e2.v4, e2.v5);

			estuaries.AddQuadUV(new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f));
			estuaries.AddTriangleUV(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f));
			estuaries.AddQuadUV(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

			estuaries.AddQuadCellData(indices, weights2, weights1, weights2, weights1);
			estuaries.AddTriangleCellData(indices, weights1, weights2, weights2);
			estuaries.AddQuadCellData(indices, weights1, weights2);

			if (incomingRiver)
			{
				estuaries.AddQuadUV2(new Vector2(1.5f, 1f), new Vector2(0.7f, 1.15f), new Vector2(1f, 0.8f), new Vector2(0.5f, 1.1f));
				estuaries.AddTriangleUV2(new Vector2(0.5f, 1.1f), new Vector2(1f, 0.8f), new Vector2(0f, 0.8f));
				estuaries.AddQuadUV2(new Vector2(0.5f, 1.1f), new Vector2(0.3f, 1.15f), new Vector2(0f, 0.8f), new Vector2(-0.5f, 1f));
			}
			else
			{
				estuaries.AddQuadUV2(new Vector2(-0.5f, -0.2f), new Vector2(0.3f, -0.35f), new Vector2(0f, 0f), new Vector2(0.5f, -0.3f));
				estuaries.AddTriangleUV2(new Vector2(0.5f, -0.3f), new Vector2(0f, 0f), new Vector2(1f, 0f));
				estuaries.AddQuadUV2(new Vector2(0.5f, -0.3f), new Vector2(0.7f, -0.35f), new Vector2(1f, 0f), new Vector2(1.5f, -0.2f));
			}
		}

        private void TriangulateOpenWater(HexDirection direction, HexCell cell, HexCell neighbour, Vector3 centre)
        {
			Vector3 c1 = centre + HexMetrics.GetFirstWaterCorner(direction);
			Vector3 c2 = centre + HexMetrics.GetSecondWaterCorner(direction);

			water.AddTriangle(centre, c1, c2);
			float3 indices = new float3(cell.Index);
			water.AddTriangleCellData(indices, weights1);
			if (direction <= HexDirection.SE && neighbour != null)
			{
				Vector3 bridge = HexMetrics.GetWaterBridge(direction);
				Vector3 e1 = c1 + bridge;
				Vector3 e2 = c2 + bridge;

				water.AddQuad(c1, c2, e1, e2);
				indices.y = neighbour.Index;
				water.AddQuadCellData(indices, weights1, weights2);

				if (direction <= HexDirection.E)
				{
					HexCell nextNeighbor = cell.GetNeighbour(direction.Next());
					if (nextNeighbor == null || !nextNeighbor.IsUnderwater)
					{
						return;
					}
					water.AddTriangle(c2, e2, c2 + HexMetrics.GetWaterBridge(direction.Next()));
					indices.z = nextNeighbor.Index;
					water.AddTriangleCellData(indices, weights1, weights2, weights3);
				}
			}
		}

		private void TriangulateWaterfallInWater(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float y1, float y2, float waterY, Vector3 indices)
        {
			v1.y = v2.y = y1;
			v3.y = v4.y = y2;
			v1 = HexMetrics.Perturb(v1);
			v2 = HexMetrics.Perturb(v2);
			v3 = HexMetrics.Perturb(v3);
			v4 = HexMetrics.Perturb(v4);
			float t = (waterY - y2) / (y1 - y2);
			v3 = Vector3.Lerp(v3, v1, t);
			v4 = Vector3.Lerp(v4, v2, t);
			rivers.AddQuadUnperturbed(v1, v2, v3, v4);
			rivers.AddQuadUV(0f, 1f, 0.8f, 1f);
			rivers.AddQuadCellData(indices, weights1, weights2);
		}

	}
}