using System.Collections.Generic;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>Baut einfache Meshes aus Quadern und flachen Vierecken, mit einem Teilnetz je Material.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> subs = new List<List<int>>();

        public MeshBuilder(int submeshes = 1) { for (int i = 0; i < submeshes; i++) subs.Add(new List<int>()); }

        public int VertexCount => verts.Count;

        /// <summary>Flaches Viereck auf Höhe y zwischen zwei Weltecken (x0, z0) und (x1, z1).</summary>
        public void Flat(float x0, float z0, float x1, float z1, float y, int sub = 0)
        {
            int i = verts.Count;
            verts.Add(new Vector3(x0, y, z0)); verts.Add(new Vector3(x0, y, z1));
            verts.Add(new Vector3(x1, y, z1)); verts.Add(new Vector3(x1, y, z0));
            for (int k = 0; k < 4; k++) normals.Add(Vector3.up);
            uvs.Add(new Vector2(x0, z0)); uvs.Add(new Vector2(x0, z1)); uvs.Add(new Vector2(x1, z1)); uvs.Add(new Vector2(x1, z0));
            AddQuad(sub, i, z1 > z0 == x1 > x0);
        }

        void AddQuad(int sub, int i, bool flip)
        {
            var t = subs[sub];
            if (flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        /// <summary>Quader zwischen zwei Weltpunkten (min, max), alle sechs Seiten.</summary>
        public void Box(Vector3 min, Vector3 max, int sub = 0, bool bottom = false)
        {
            Face(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up, sub);
            if (bottom) Face(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, sub);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back, sub);
            Face(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.forward, sub);
            Face(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left, sub);
            Face(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right, sub);
        }

        /// <summary>Viereck aus vier Ecken im Uhrzeigersinn (von vorn gesehen).</summary>
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, int sub = 0)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            var t = subs[sub];
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public Mesh Build(Mesh mesh = null)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Erzeugt ein Objekt mit Mesh und Materialien (eins je Teilnetz).</summary>
        public static (GameObject go, MeshFilter mf, MeshRenderer mr) Object(string name, Transform parent, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            return (go, mf, mr);
        }
    }
}
