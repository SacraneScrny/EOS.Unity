#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    internal sealed class EosGraphView
    {
        public Vector2 Pan;
        public float Zoom = 1f;
        public string Selected;
        public Action<string> OnSelect;

        public readonly Dictionary<string, Vector2> Positions = new();

        const float BaseW = 168f;
        const float BaseH = 56f;
        const float MinZoom = 0.35f;
        const float MaxZoom = 2.0f;

        public struct Node
        {
            public string Id;
            public string Title;
            public string Subtitle;
            public Color Color;
            public float Width;
            public float Height;
        }

        public struct Edge
        {
            public string From;
            public string To;
            public Color Color;
        }

        string _drag;
        Vector2 _dragGrab;
        bool _panning;

        GUIStyle _titleStyle;
        GUIStyle _subStyle;

        public bool HasPosition(string id) => Positions.ContainsKey(id);
        public void SetPosition(string id, Vector2 worldPos) => Positions[id] = worldPos;

        Vector2 WorldToScreen(Vector2 world) => (world + Pan) * Zoom;
        Vector2 ScreenToWorld(Vector2 screen) => screen / Zoom - Pan;

        public void FrameAll()
        {
            Pan = new Vector2(40f, 40f);
            Zoom = 1f;
        }

        public void Draw(Rect area, IList<Node> nodes, IList<Edge> edges)
        {
            EnsureStyles();
            var e = Event.current;

            EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.18f, 1f));

            GUI.BeginClip(area);
            var local = new Rect(0, 0, area.width, area.height);

            DrawGrid(local);

            var rectById = new Dictionary<string, Rect>(nodes.Count);
            foreach (var n in nodes)
            {
                if (!Positions.TryGetValue(n.Id, out var pos)) continue;
                float w = (n.Width > 0 ? n.Width : BaseW) * Zoom;
                float h = (n.Height > 0 ? n.Height : BaseH) * Zoom;
                var p = WorldToScreen(pos);
                rectById[n.Id] = new Rect(p.x, p.y, w, h);
            }

            DrawEdges(edges, rectById);
            DrawNodes(nodes, rectById);

            HandleInput(e, local, rectById);

            GUI.EndClip();
        }

        void DrawEdges(IList<Edge> edges, Dictionary<string, Rect> rectById)
        {
            Handles.BeginGUI();
            foreach (var edge in edges)
            {
                if (!rectById.TryGetValue(edge.From, out var a)) continue;
                if (!rectById.TryGetValue(edge.To, out var b)) continue;

                var start = new Vector3(a.xMax, a.center.y);
                var end = new Vector3(b.xMin, b.center.y);
                float t = Mathf.Max(30f, Mathf.Abs(end.x - start.x) * 0.5f);
                var st = start + new Vector3(t, 0);
                var et = end - new Vector3(t, 0);
                Handles.DrawBezier(start, end, st, et, edge.Color, null, 2.5f * Zoom);
            }
            Handles.EndGUI();
        }

        void DrawNodes(IList<Node> nodes, Dictionary<string, Rect> rectById)
        {
            _titleStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(12 * Zoom));
            _subStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(10 * Zoom));

            foreach (var n in nodes)
            {
                if (!rectById.TryGetValue(n.Id, out var r)) continue;

                bool selected = n.Id == Selected;
                var body = n.Color;
                EditorGUI.DrawRect(r, body);

                var border = selected ? new Color(1f, 0.85f, 0.2f) : new Color(0, 0, 0, 0.6f);
                DrawBorder(r, border, selected ? 2f : 1f);

                var pad = 6f * Zoom;
                var titleRect = new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height * 0.5f);
                var subRect = new Rect(r.x + pad, r.y + r.height * 0.5f, r.width - 2 * pad, r.height * 0.5f - pad);
                GUI.Label(titleRect, n.Title, _titleStyle);
                if (!string.IsNullOrEmpty(n.Subtitle))
                    GUI.Label(subRect, n.Subtitle, _subStyle);
            }
        }

        void HandleInput(Event e, Rect local, Dictionary<string, Rect> rectById)
        {
            if (!local.Contains(e.mousePosition) && e.type != EventType.MouseUp && e.type != EventType.MouseDrag)
                return;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                {
                    var worldUnder = ScreenToWorld(e.mousePosition);
                    float newZoom = Mathf.Clamp(Zoom * (1f - e.delta.y * 0.03f), MinZoom, MaxZoom);
                    Zoom = newZoom;
                    Pan = e.mousePosition / Zoom - worldUnder;
                    e.Use();
                    break;
                }
                case EventType.MouseDown when e.button == 0:
                {
                    _drag = HitTest(e.mousePosition, rectById);
                    if (_drag != null)
                    {
                        Selected = _drag;
                        OnSelect?.Invoke(_drag);
                        _dragGrab = ScreenToWorld(e.mousePosition) - Positions[_drag];
                    }
                    else
                    {
                        Selected = null;
                        OnSelect?.Invoke(null);
                        _panning = true;
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 0:
                {
                    if (_drag != null)
                        Positions[_drag] = ScreenToWorld(e.mousePosition) - _dragGrab;
                    else if (_panning)
                        Pan += e.delta / Zoom;
                    e.Use();
                    break;
                }
                case EventType.MouseUp when e.button == 0:
                {
                    _drag = null;
                    _panning = false;
                    break;
                }
            }
        }

        string HitTest(Vector2 screen, Dictionary<string, Rect> rectById)
        {
            string hit = null;
            foreach (var kv in rectById)
                if (kv.Value.Contains(screen)) hit = kv.Key;
            return hit;
        }

        void DrawGrid(Rect area)
        {
            float spacing = 24f * Zoom;
            if (spacing < 8f) return;
            var line = new Color(1f, 1f, 1f, 0.04f);
            Vector2 origin = WorldToScreen(Vector2.zero);
            float ox = Mathf.Repeat(origin.x, spacing);
            float oy = Mathf.Repeat(origin.y, spacing);
            Handles.BeginGUI();
            Handles.color = line;
            for (float x = ox; x < area.width; x += spacing)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, area.height));
            for (float y = oy; y < area.height; y += spacing)
                Handles.DrawLine(new Vector3(0, y), new Vector3(area.width, y));
            Handles.EndGUI();
        }

        static void DrawBorder(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.white },
            };
            _subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            };
        }
    }
}
#endif
