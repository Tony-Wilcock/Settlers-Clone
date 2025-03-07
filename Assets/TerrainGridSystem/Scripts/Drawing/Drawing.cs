﻿using UnityEngine;
using System.Collections.Generic;

namespace TGS
{
    public static class Drawing {

        static readonly Rect dummyRect = new Rect();

        public static Renderer CreateSurface(string name, List<Vector3> surfPoints, int[] indices, Material material, int sortingOrder, DisposalManager disposalManager, Vector2 tileScale = default, Vector2 tileOffset = default) {
            return CreateSurface(name, surfPoints, indices, material, dummyRect, Misc.Vector2one, Misc.Vector2zero, 0, false, sortingOrder, disposalManager, tileScale, tileOffset);
        }

        /// <summary>
        /// Rotates one point around another
        /// </summary>
        /// <param name="pointToRotate">The point to rotate.</param>
        /// <param name="centerPoint">The centre point of rotation.</param>
        /// <param name="angleInDegrees">The rotation angle in degrees.</param>
        /// <returns>Rotated point</returns>
        static Vector2 RotatePoint(Vector2 pointToRotate, Vector2 centerPoint, float angleInDegrees) {
            float angleInRadians = angleInDegrees * Mathf.Deg2Rad;
            float cosTheta = Mathf.Cos(angleInRadians);
            float sinTheta = Mathf.Sin(angleInRadians);
            return new Vector2(cosTheta * (pointToRotate.x - centerPoint.x) - sinTheta * (pointToRotate.y - centerPoint.y) + centerPoint.x,
                sinTheta * (pointToRotate.x - centerPoint.x) + cosTheta * (pointToRotate.y - centerPoint.y) + centerPoint.y);
        }

        public static Renderer CreateSurface(string name, List<Vector3> surfPoints, int[] indices, Material material, Rect rect, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateTextureInLocalSpace, int sortingOrder, DisposalManager disposalManager, Vector2 tileScale = default, Vector2 tileOffset = default) {

            GameObject hexa = new GameObject(name, typeof(MeshRenderer), typeof(MeshFilter));
            disposalManager.MarkForDisposal(hexa);
            if (!Application.isPlaying) {
                hexa.hideFlags |= HideFlags.HideInHierarchy;
            }

            Mesh mesh = new Mesh();
            mesh.name = "Surface";
            disposalManager.MarkForDisposal(mesh);
            // uv mapping
            int len = surfPoints.Count;
            Vector2[] uv = new Vector2[len];
            Vector2 tileCenter = rect.center;
            if (tileScale == default) tileScale = Misc.Vector2one;
            bool useTileScale = tileScale != Misc.Vector2one;
            for (int k = 0; k < len; k++) {
                Vector3 coor = surfPoints[k];
                Vector2 normCoor;
                if (rotateTextureInLocalSpace) {
                    normCoor = new Vector2((coor.x - rect.xMin) / rect.width, (coor.y - rect.yMin) / rect.height);
                    if (textureRotation != 0) {
                        normCoor = RotatePoint(normCoor, Misc.Vector2half, textureRotation);
                    }
                    normCoor.x = 0.5f + (normCoor.x - 0.5f) / textureScale.x;
                    normCoor.y = 0.5f + (normCoor.y - 0.5f) / textureScale.y;
                    normCoor -= textureOffset;
                } else {
                    coor.x /= textureScale.x;
                    coor.y /= textureScale.y;
                    if (textureRotation != 0) {
                        coor = RotatePoint(coor, Vector2.zero, textureRotation);
                    }
                    coor.x -= textureOffset.x;
                    coor.y -= textureOffset.y;
                    normCoor = new Vector2((coor.x - rect.xMin) / rect.width, (coor.y - rect.yMin) / rect.height);
                }
                uv[k] = normCoor;
                if (useTileScale) {
                    coor.x = tileCenter.x + (coor.x - tileCenter.x) * tileScale.x + tileOffset.x;
                    coor.y = tileCenter.y + (coor.y - tileCenter.y) * tileScale.y + tileOffset.y;
                    surfPoints[k] = coor;
                }
            }

            mesh.SetVertices(surfPoints);
            mesh.SetTriangles(indices, 0, true);
            mesh.uv = uv;
            mesh.RecalculateNormals();

            MeshFilter meshFilter = hexa.GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            Renderer rr = hexa.GetComponent<Renderer>();
            rr.sortingOrder = sortingOrder;
            rr.sharedMaterial = material;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;
            return rr;

        }
    }


}



