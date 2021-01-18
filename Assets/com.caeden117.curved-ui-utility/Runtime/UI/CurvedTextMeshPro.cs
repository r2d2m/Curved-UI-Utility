﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CurvedUIUtility
{
    public class CurvedTextMeshPro : TextMeshProUGUI
    {
        private CurvedUIHelper curvedHelper = new CurvedUIHelper();
        private CurvedUIController controller;

        private List<Vector3> cachedVertices = new List<Vector3>();
        private List<Vector3> modifiedVertices = new List<Vector3>();

        protected override void OnEnable()
        {
            base.OnEnable();
            curvedHelper.Reset();
            curvedHelper.GetCurvedUIController(canvas);
        }

        protected override void GenerateTextMesh()
        {
            base.GenerateTextMesh();

            m_mesh.GetVertices(cachedVertices);

            UpdateCurvature();
        }

        protected override void DrawUnderlineMesh(Vector3 start, Vector3 end, ref int index, float startScale, float endScale, float maxScale, float sdfScale, Color32 underlineColor)
        {
            if (m_cached_Underline_Character == null)
            {
                if (!TMP_Settings.warningsDisabled)
                    Debug.LogWarning("Unable to add underline since the Font Asset doesn't contain the underline character.", this);

                return;
            }

            int horizontalElements = CurvedUIHelper.GetNumberOfElementsForWidth(Mathf.Abs(start.x - end.x));

            int verticesCount = index + (horizontalElements * 4);
            // Check to make sure our current mesh buffer allocations can hold these new Quads.
            if (verticesCount > m_textInfo.meshInfo[0].vertices.Length)
            {
                // Resize Mesh Buffers
                m_textInfo.meshInfo[0].ResizeMeshInfo(verticesCount / 4);
            }

            // Adjust the position of the underline based on the lowest character. This matters for subscript character.
            start.y = Mathf.Min(start.y, end.y);
            end.y = Mathf.Min(start.y, end.y);

            float startPadding = m_padding * startScale / maxScale;
            float endPadding = m_padding * endScale / maxScale;

            float underlineThickness = m_fontAsset.faceInfo.underlineThickness;

            // Alpha is the lower of the vertex color or tag color alpha used.
            underlineColor.a = m_fontColor32.a < underlineColor.a ? (byte)(m_fontColor32.a) : (byte)(underlineColor.a);

            Vector3[] vertices = m_textInfo.meshInfo[0].vertices;
            Vector2[] uvs0 = m_textInfo.meshInfo[0].uvs0;
            Vector2[] uvs2 = m_textInfo.meshInfo[0].uvs2;
            Color32[] colors32 = m_textInfo.meshInfo[0].colors32;

            Vector2 uvBL = new Vector2((m_cached_Underline_Character.glyph.glyphRect.x - startPadding) / m_fontAsset.atlasWidth, (m_cached_Underline_Character.glyph.glyphRect.y - m_padding) / m_fontAsset.atlasHeight);  // bottom left
            Vector2 uvTL = new Vector2(uvBL.x, (m_cached_Underline_Character.glyph.glyphRect.y + m_cached_Underline_Character.glyph.glyphRect.height + m_padding) / m_fontAsset.atlasHeight);  // top left
            Vector2 uvBR = new Vector2((m_cached_Underline_Character.glyph.glyphRect.x + endPadding + m_cached_Underline_Character.glyph.glyphRect.width) / m_fontAsset.atlasWidth, uvTL.y); // End Part - Bottom Right
            Vector2 uvTR = new Vector2(uvBR.x, uvBL.y); // End Part - Top Right

            var xScale = Mathf.Abs(sdfScale);
            var width = end.x - start.x;

            for (int i = 0; i < horizontalElements; i++)
            {
                var face = i * 4;

                var faceWidth = width / horizontalElements * i;
                var nextFaceWidth = width / horizontalElements * (i + 1);

                var bl = index + face + 0;
                var tl = index + face + 1;
                var tr = index + face + 2;
                var br = index + face + 3;

                vertices[bl] = start + new Vector3(faceWidth, 0 - (underlineThickness + m_padding) * maxScale, 0); // BL
                vertices[tl] = start + new Vector3(faceWidth, m_padding * maxScale, 0); // TL
                vertices[tr] = vertices[tl] + new Vector3(nextFaceWidth - faceWidth, 0, 0); // TR
                vertices[br] = vertices[bl] + new Vector3(nextFaceWidth - faceWidth, 0, 0); // BR

                if (i == 0)
                {
                    uvs0[bl] = uvBL;
                    uvs0[tl] = uvTL;
                    uvs0[tr] = new Vector2((m_cached_Underline_Character.glyph.glyphRect.x - startPadding + (float)m_cached_Underline_Character.glyph.glyphRect.width / 2) / m_fontAsset.atlasWidth, uvTL.y);
                    uvs0[br] = new Vector2(uvs0[tr].x, uvBL.y);
                }
                else if (i == horizontalElements - 1)
                {
                    uvs0[bl] = new Vector2((m_cached_Underline_Character.glyph.glyphRect.x + endPadding + (float)m_cached_Underline_Character.glyph.glyphRect.width / 2) / m_fontAsset.atlasWidth, uvTL.y); // Mid Top Right
                    uvs0[tl] = new Vector2(uvs0[bl].x, uvBL.y); // Mid Bottom right
                    uvs0[tr] = uvTR;
                    uvs0[br] = uvBR;
                }
                else
                {
                    uvs0[bl] = Vector2.Lerp(uvBL, uvBR, faceWidth / width);
                    uvs0[tl] = Vector2.Lerp(uvTL, uvTR, faceWidth / width);
                    uvs0[tr] = Vector2.Lerp(uvTL, uvTR, nextFaceWidth / width);
                    uvs0[br] = Vector2.Lerp(uvBL, uvBR, nextFaceWidth / width);
                }

                uvs2[bl] = PackUV(faceWidth / width, 0, xScale);
                uvs2[tl] = PackUV(faceWidth / width, 1, xScale);
                uvs2[tr] = PackUV(nextFaceWidth / width, 1, xScale);
                uvs2[br] = PackUV(nextFaceWidth / width, 0, xScale);

                colors32[bl] = underlineColor;
                colors32[tl] = underlineColor;
                colors32[tr] = underlineColor;
                colors32[br] = underlineColor;
            }

            index = verticesCount;
        }

        private void LateUpdate()
        {
            if (!m_layoutAlreadyDirty)
            {
                UpdateCurvature();
            }
        }

        private void UpdateCurvature()
        {
            curvedHelper.PokeScreenSize();

            modifiedVertices.Clear();

            foreach (var v in cachedVertices)
            {
                modifiedVertices.Add(curvedHelper.GetCurvedPosition(rectTransform, v));
            }

            m_mesh.SetVertices(modifiedVertices);

            canvasRenderer.SetMesh(m_mesh);
        }
    }
}