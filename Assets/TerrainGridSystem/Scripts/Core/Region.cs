using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {

    public class Region {

        public Polygon polygon;

        /// <summary>
        /// Points coordinates with applied grid offset and scale
        /// </summary>
        public List<Vector2> points;

        /// <summary>
        /// Scaled rect (rect with grid offset and scale applied)
        /// </summary>
        public Rect rect2D;
        public float rect2DArea;

        /// <summary>
        /// Cells in this region.
        /// </summary>
        public List<Cell> cells;

        /// <summary>
        /// Original grid segments. Segments coordinates are not scaled.
        /// </summary>
        public List<Segment> segments;

        /// <summary>
        /// Determines if this region should be rendered flat to the terrain
        /// </summary>
        public bool isFlat;

        public IAdmin entity;

        public Renderer renderer;
        public GameObject surfaceGameObject { get { return renderer != null ? renderer.gameObject : null; } }
        public Material cachedMat;

        /// <summary>
        /// Used internally to keep territory regions list order during updates
        /// </summary>
        public int sortIndex = -1;

        /// <summary>
        /// Used internally to ensure smaller territory surfaces are rendered before others
        /// </summary>
        public int renderingOrder;

        public Material customMaterial { get; set; }

        public Vector2 customTextureScale, customTextureOffset;
        public float customTextureRotation;
        public bool customRotateInLocalSpace;
        public bool customIsCanvasTexture;

        public delegate bool ContainsFunction (float x, float y);
        public ContainsFunction Contains;

        /// <summary>
        /// If the gameobject contains one or more children surfaces with name splitSurface due to having +65000 vertices
        /// </summary>
		public List<Renderer> childrenSurfaces;

        /// <summary>
        /// Reference to a custom border drawn using TerritoryDrawInteriorBorder
        /// </summary>
        public GameObject interiorBorderGameObject;

        /// <summary>
        /// Reference to a custom border drawn using CellDrawBorder or TerritoryDrawFrontier
        /// </summary>
        public GameObject customBorderGameObject;


        enum CentroidCalculatedType {
            None,
            Default,
            BetterCentroid
        }

        Vector2 _centroid;
        CentroidCalculatedType _centroidCalculated = CentroidCalculatedType.None;

        public Vector2 centroid {
            get {
                if (_centroidCalculated != CentroidCalculatedType.Default) {
                    ComputeCentroid();
                }
                return _centroid;
            }
        }

        public Vector2 betterCentroid {
            get {
                if (_centroidCalculated != CentroidCalculatedType.BetterCentroid) {
                    ComputeBetterCentroid();
                }
                return _centroid;
            }
        }

        public Region (IAdmin entity, bool isBox) {
            this.entity = entity;
            if (isBox) {
                segments = new List<Segment>(4);
                Contains = PointInBox;
            }
            else {
                segments = new List<Segment>(6);
                Contains = PointInPolygon;
            }
        }


        Region (IAdmin entity) {
            this.entity = entity;
            segments = new List<Segment>(4);
        }


        public void Clear () {
            polygon = null;
            ClearPointData();
            rect2D.width = rect2D.height = 0;
            rect2DArea = 0;
            DestroySurface();
            childrenSurfaces = null;
            cells.Clear();
        }

        public void ClearPointData () {
            if (points != null) {
                points.Clear();
            }
            if (segments != null) {
                segments.Clear();
            }
        }

        public void DestroySurface (bool keepCustomMaterial = false) {
            if (renderer != null) {
                Object.DestroyImmediate(renderer.gameObject);
                renderer = null;
                if (!keepCustomMaterial) {
                    ClearCustomMaterial();
                }
            }
        }

        void ClearCustomMaterial () {
            customMaterial = null;
            customTextureOffset = Misc.Vector2zero;
            customTextureRotation = 0;
            customTextureScale = Misc.Vector2zero;
            customRotateInLocalSpace = false;
            customIsCanvasTexture = false;
        }

        /// <summary>
        /// Returns an exact copy of this region
        /// </summary>
        /// <returns></returns>
        public Region Clone () {
            Region c = CloneWithoutPointData();
            c.points = new List<Vector2>(points);
            c.polygon = polygon.Clone();
            c.segments = new List<Segment>(segments);
            c.renderingOrder = renderingOrder;
            c.sortIndex = sortIndex;
            return c;
        }

        /// <summary>
        /// Returns a copy of this region without point/polygon data
        /// </summary>
        /// <returns></returns>
        public Region CloneWithoutPointData () {
            Region c = new Region(entity);
            c.Contains = this.Contains;
            c.customMaterial = this.customMaterial;
            c.customTextureScale = this.customTextureScale;
            c.customTextureOffset = this.customTextureOffset;
            c.customTextureRotation = this.customTextureRotation;
            c.rect2D = rect2D;
            c.rect2DArea = rect2DArea;
            c.renderingOrder = renderingOrder;
            c.sortIndex = sortIndex;
            return c;
        }


        public void SetPoints (List<Vector2> points) {
            this.points = points;
            UpdateBounds();
        }

        public void Enlarge (float amount) {
            Vector2 center = rect2D.center;
            int pointCount = points.Count;
            for (int k = 0; k < pointCount; k++) {
                Vector2 p = points[k];
                float DX = center.x - p.x;
                float DY = center.y - p.y;
                p.x -= DX * amount;
                p.y -= DY * amount;
                points[k] = p;
            }
        }

        public bool Intersects (Region other) {

            if (points == null || other == null || other.points == null)
                return false;

            Rect otherRect = other.rect2D;

            if (otherRect.xMin > rect2D.xMax)
                return false;
            if (otherRect.xMax < rect2D.xMin)
                return false;
            if (otherRect.yMin > rect2D.yMax)
                return false;
            if (otherRect.yMax < rect2D.yMin)
                return false;

            int pointCount = points.Count;
            int otherPointCount = other.points.Count;

            for (int k = 0; k < otherPointCount; k++) {
                int j = pointCount - 1;
                bool inside = false;
                Vector2 otherPoint = other.points[k];
                for (int i = 0; i < pointCount; j = i++) {
                    Vector2 pi = points[i];
                    Vector2 pj = points[j];
                    if (((pi.y <= otherPoint.y && otherPoint.y < pj.y) || (pj.y <= otherPoint.y && otherPoint.y < pi.y)) &&
                        (otherPoint.x < (pj.x - pi.x) * (otherPoint.y - pi.y) / (pj.y - pi.y) + pi.x))
                        inside = !inside;
                }
                if (inside)
                    return true;
            }

            for (int k = 0; k < pointCount; k++) {
                int j = otherPointCount - 1;
                bool inside = false;
                Vector2 p = points[k];
                for (int i = 0; i < otherPointCount; j = i++) {
                    Vector2 opi = other.points[i];
                    Vector2 opj = other.points[j];
                    if (((opi.y <= p.y && p.y < opj.y) || (opj.y <= p.y && p.y < opi.y)) &&
                        (p.x < (opj.x - opi.x) * (p.y - opi.y) / (opj.y - opi.y) + opi.x))
                        inside = !inside;
                }
                if (inside)
                    return true;
            }

            return false;
        }

        bool PointInBox (float x, float y) {
            return x >= rect2D.xMin && x <= rect2D.xMax && y >= rect2D.yMin && y <= rect2D.yMax;
        }

        bool PointInPolygon (float x, float y) {

            if (x > rect2D.xMax || x < rect2D.xMin || y > rect2D.yMax || y < rect2D.yMin)
                return false;

            if (points == null)
                return false;

            int numPoints = points.Count;
            bool inside = false;
            Vector2 pj = points[numPoints - 1];
            Vector2 pi;
            for (int i = 0; i < numPoints; pj = pi, i++) {
                pi = points[i];
                if (((pi.y <= y && y < pj.y) || (pj.y <= y && y < pi.y)) &&
                    (x < (pj.x - pi.x) * (y - pi.y) / (pj.y - pi.y) + pi.x))
                    inside = !inside;
            }
            return inside;
        }

        public bool ContainsRegion (Region otherRegion) {
            if (!rect2D.Overlaps(otherRegion.rect2D))
                return false;

            Vector2 otherRegionCenter = otherRegion.rect2D.center;
            if (!Contains(otherRegionCenter.x, otherRegionCenter.y))
                return false;

            int cellsCount = otherRegion.cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                Vector2 cellCenter = otherRegion.cells[k].scaledCenter;
                if (!Contains(cellCenter.x, cellCenter.y))
                    return false;
            }
            return true;
        }


        public void UpdateBounds () {
            float minx, miny, maxx, maxy;
            minx = miny = float.MaxValue;
            maxx = maxy = float.MinValue;
            int pointsCount = points.Count;
            for (int p = 0; p < pointsCount; p++) {
                Vector2 point = points[p];
                if (point.x < minx)
                    minx = point.x;
                if (point.x > maxx)
                    maxx = point.x;
                if (point.y < miny)
                    miny = point.y;
                if (point.y > maxy)
                    maxy = point.y;
            }
            float rectWidth = maxx - minx;
            float rectHeight = maxy - miny;
            rect2D = new Rect(minx, miny, rectWidth, rectHeight);
            rect2DArea = rectWidth * rectHeight;
            if (_centroidCalculated == CentroidCalculatedType.Default) {
                ComputeCentroid();
            }
            else if (_centroidCalculated == CentroidCalculatedType.BetterCentroid) {
                ComputeBetterCentroid();
            }
            if (points.Count == 4) {
                Contains = PointInBox;
            }
            else {
                Contains = PointInPolygon;
            }
        }

        /// <summary>
        /// Computes the center of the polygon so it falls inside it
        /// </summary>
        void ComputeCentroid () {

            Vector2 c = Misc.Vector2zero;
            float area = 0f;

            int pointCount = points.Count;
            for (int i = 0; i < pointCount; ++i) {
                Vector2 p1 = points[i];
                Vector2 p2 = i + 1 < pointCount ? points[i + 1] : points[0];

                float d = p1.x * p2.y - p1.y * p2.x;
                float triangleArea = 0.5f * d;
                area += triangleArea;

                c.x += triangleArea * (p1.x + p2.x) / 3f;
                c.y += triangleArea * (p1.y + p2.y) / 3f;
            }

            if (area != 0) {
                c.x /= area;
                c.y /= area;
            }
            _centroid = c;
            _centroidCalculated = CentroidCalculatedType.Default;
        }


        /// <summary>
        /// Computes the center of the polygon so it falls inside it
        /// </summary>
        void ComputeBetterCentroid () {

            Vector2 center = entity.GetScaledCenter();

            if (cells != null) {
                int cellCount = cells.Count;
                float maxRadius = -1;
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    float radius = GetCellToEdgeMinDistance(cell);
                    if (radius > maxRadius) {
                        maxRadius = radius;
                        center = cell.scaledCenter;
                    }
                }
            }

            _centroid = center;
            _centroidCalculated = CentroidCalculatedType.BetterCentroid;
        }

        readonly static Vector2[] dirs = { Vector2.right, Vector2.down, Vector2.left, Vector2.up };

        float GetCellToEdgeMinDistance (Cell cell) {
            float minRadius = float.MaxValue;
            for (int d = 0; d < 4; d++) {
                float offsetX = dirs[d].x * cell.region.rect2D.width;
                float offsetY = dirs[d].y * cell.region.rect2D.height;
                Vector2 center = cell.scaledCenter;
                Vector2 startPos = center;
                while (Contains(center.x, center.y)) {
                    center.x += offsetX;
                    center.y += offsetY;
                }
                float dx = Mathf.Abs(center.x - startPos.x);
                float dy = Mathf.Abs(center.y - startPos.y);
                float radius = dx + dy;
                if (radius < minRadius) {
                    minRadius = radius;
                }
            }
            return minRadius;
        }

    }
}

