#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using EOS.Core;
using EOS.Diagnostics;
using EOS.Entities;

namespace EOS.Unity.Editor
{
    /// <summary>
    /// Live, read-only visualizer for the EOS world: entities + components, the system
    /// pipeline graph, and the group / archetype structure. Editor-only, IMGUI, polls the
    /// public core API. The only mutation it performs is enabling/disabling system groups.
    /// </summary>
    public sealed class EosInspectorWindow : EditorWindow
    {
        enum Tab { Live, Systems, Groups }
        enum GroupTab { SystemGroups, DataArchetypes, QueryArchetypes }
        enum PhaseFilter { All, Update, FixedUpdate, LateUpdate }

        Tab _tab = Tab.Live;
        GroupTab _groupTab = GroupTab.SystemGroups;
        PhaseFilter _phase = PhaseFilter.All;

        int _worldIndex;
        string _search = "";

        Vector2 _entityScroll, _detailScroll, _sideScroll, _groupScroll;
        EosEntity _selectedEntity = EosEntity.Null;
        bool _hasSelection;

        readonly EosGraphView _graph = new();
        string _selectedSystemId;
        readonly HashSet<string> _expanded = new();
        readonly List<string> _tagBuffer = new();

        [MenuItem("Sackrany/EOS/World Inspector")]
        static void Open() => GetWindow<EosInspectorWindow>("EOS World");

        void OnEnable()
        {
            _graph.OnSelect = id => _selectedSystemId = id;
            _graph.FrameAll();
        }

        // Repaint a few times a second while playing so live data stays fresh.
        void OnInspectorUpdate()
        {
            if (EosIntrospection.IsLive) Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();

            if (!EosIntrospection.IsLive && _tab == Tab.Live)
            {
                EditorGUILayout.HelpBox(
                    "Universe is not booted. Enter Play mode (or call EosLoop.Boot) to inspect live entities.\n" +
                    "Systems and Groups tabs work in edit mode by reflecting the code.",
                    MessageType.Info);
                return;
            }

            var world = CurrentWorld();

            switch (_tab)
            {
                case Tab.Live: DrawLive(world); break;
                case Tab.Systems: DrawSystems(world); break;
                case Tab.Groups: DrawGroups(world); break;
            }
        }

        // ---- Toolbar -----------------------------------------------------------------

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Live", "Systems", "Groups & Archetypes" },
                    EditorStyles.toolbarButton, GUILayout.Width(330));

                GUILayout.Space(8);

                var worlds = EosIntrospection.Worlds();
                if (worlds.Count > 0)
                {
                    var labels = new string[worlds.Count];
                    for (int i = 0; i < worlds.Count; i++) labels[i] = EosIntrospection.WorldLabel(worlds[i]);
                    _worldIndex = Mathf.Clamp(_worldIndex, 0, worlds.Count - 1);
                    _worldIndex = EditorGUILayout.Popup(_worldIndex, labels, EditorStyles.toolbarPopup, GUILayout.Width(160));

                    var w = worlds[_worldIndex];
                    GUILayout.Label($"frame {w.Frame}  ver {w.Version}  {(w.IsEnabled ? "● enabled" : "○ disabled")}",
                        EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label("no live world", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Copy dump", EditorStyles.toolbarButton) && EosIntrospection.IsLive)
                    EditorGUIUtility.systemCopyBuffer = WorldDebug.DumpUniverse();
            }
        }

        IReadOnlyWorld CurrentWorld()
        {
            var worlds = EosIntrospection.Worlds();
            if (worlds.Count == 0) return null;
            _worldIndex = Mathf.Clamp(_worldIndex, 0, worlds.Count - 1);
            return worlds[_worldIndex];
        }

        // ---- Live tab ----------------------------------------------------------------

        void DrawLive(IReadOnlyWorld world)
        {
            if (world == null) { EditorGUILayout.HelpBox("No world selected.", MessageType.Info); return; }

            using (new EditorGUILayout.HorizontalScope())
            {
                // Left: entity list.
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(270)))
                {
                    _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                    var entities = EosIntrospection.Entities(world);
                    EditorGUILayout.LabelField($"Entities: {entities.Count}", EditorStyles.miniBoldLabel);

                    _entityScroll = EditorGUILayout.BeginScrollView(_entityScroll);
                    foreach (var entity in entities)
                    {
                        if (!MatchesSearch(world, entity)) continue;
                        bool isSel = _hasSelection && _selectedEntity.Id == entity.Id && _selectedEntity.Version == entity.Version;
                        var label = $"#{entity.Id}  {entity.Name}{(entity.IsActive ? "" : "  (inactive)")}";
                        var bg = GUI.backgroundColor;
                        if (isSel) GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
                        if (GUILayout.Button(label, EditorStyles.miniButton))
                        {
                            _selectedEntity = entity;
                            _hasSelection = true;
                        }
                        GUI.backgroundColor = bg;
                    }
                    EditorGUILayout.EndScrollView();
                }

                // Right: entity detail + storages.
                using (new EditorGUILayout.VerticalScope())
                {
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    if (_hasSelection && _selectedEntity.IsValid)
                        DrawEntityDetail(world, _selectedEntity);
                    else
                        EditorGUILayout.HelpBox("Select an entity.", MessageType.None);

                    EditorGUILayout.Space();
                    DrawStorages(world);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        bool MatchesSearch(IReadOnlyWorld world, EosEntity entity)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            var q = _search.Trim();
            if (entity.Id.ToString() == q) return true;
            if (!string.IsNullOrEmpty(entity.Name) && entity.Name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (var c in EosIntrospection.Components(world, entity))
                if (c.Name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void DrawEntityDetail(IReadOnlyWorld world, EosEntity entity)
        {
            EditorGUILayout.LabelField($"Entity #{entity.Id}  '{entity.Name}'  v{entity.Version}", EditorStyles.boldLabel);
            var key = EosIntrospection.StableKey(world, entity);
            if (!string.IsNullOrEmpty(key)) EditorGUILayout.LabelField("Stable key", key);
            EditorGUILayout.LabelField("Active", entity.IsActive.ToString());

            var tags = EosIntrospection.Tags(world, entity, _tagBuffer);
            EditorGUILayout.LabelField("Tags", tags.Count == 0 ? "(none)" : string.Join(", ", tags));

            EditorGUILayout.Space();
            var components = EosIntrospection.Components(world, entity);
            EditorGUILayout.LabelField($"Components ({components.Count})", EditorStyles.boldLabel);

            foreach (var c in components)
            {
                string fid = $"{entity.Id}:{c.Name}";
                bool exp = _expanded.Contains(fid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool now = EditorGUILayout.Foldout(exp, c.Name + (c.Ready ? "" : "  (not ready)"), true);
                    if (now != exp) { if (now) _expanded.Add(fid); else _expanded.Remove(fid); }
                }
                if (!exp) continue;

                EditorGUI.indentLevel++;
                var values = EosIntrospection.Values(c.Instance);
                if (values.Count == 0) EditorGUILayout.LabelField("(no public fields)", EditorStyles.miniLabel);
                foreach (var v in values)
                    EditorGUILayout.LabelField(v.Name, v.Value);
                EditorGUI.indentLevel--;
            }
        }

        void DrawStorages(IReadOnlyWorld world)
        {
            var storages = EosIntrospection.Storages(world);
            EditorGUILayout.LabelField($"Component storages ({storages.Count})", EditorStyles.boldLabel);
            foreach (var s in storages)
            {
                if (s.Count == 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(s.Name, GUILayout.Width(220));
                    EditorGUILayout.LabelField($"× {s.Count}", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"add v{s.MaxAddVersion}  mark v{s.MaxMarkVersion}", EditorStyles.miniLabel);
                }
            }
        }

        // ---- Systems tab -------------------------------------------------------------

        void DrawSystems(IReadOnlyWorld world)
        {
            var systems = EosVizModel.Systems(world);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _phase = (PhaseFilter)EditorGUILayout.EnumPopup(_phase, EditorStyles.toolbarPopup, GUILayout.Width(120));
                if (GUILayout.Button("Re-layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    LayoutSystems(systems, force: true);
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    _graph.FrameAll();
                GUILayout.Label($"{systems.Count} systems  ·  scroll to zoom, drag to pan", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            LayoutSystems(systems, force: false);

            var nodes = new List<EosGraphView.Node>();
            var edges = new List<EosGraphView.Edge>();
            var byId = new Dictionary<string, EosVizModel.SystemInfo>();

            foreach (var s in systems)
            {
                if (!PhaseShown(s.Phase)) continue;
                byId[s.Id] = s;
                nodes.Add(new EosGraphView.Node
                {
                    Id = s.Id,
                    Title = s.Name,
                    Subtitle = SystemBadge(s),
                    Color = SystemColor(s),
                });
            }

            foreach (var s in systems)
            {
                if (!PhaseShown(s.Phase)) continue;
                foreach (var target in s.Before)
                {
                    var tid = target.FullName ?? target.Name;
                    if (byId.ContainsKey(tid)) edges.Add(new EosGraphView.Edge { From = s.Id, To = tid, Color = EdgeColor });
                }
                foreach (var target in s.After)
                {
                    var tid = target.FullName ?? target.Name;
                    if (byId.ContainsKey(tid)) edges.Add(new EosGraphView.Edge { From = tid, To = s.Id, Color = EdgeColor });
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var graphRect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                _graph.Draw(graphRect, nodes, edges);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
                {
                    _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);
                    DrawSystemDetail(byId);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawSystemDetail(Dictionary<string, EosVizModel.SystemInfo> byId)
        {
            if (_selectedSystemId == null || !byId.TryGetValue(_selectedSystemId, out var s))
            {
                EditorGUILayout.HelpBox("Select a system node.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(s.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Phase", s.Phase.ToString());
            EditorGUILayout.LabelField("Enabled", s.Enabled.ToString());
            EditorGUILayout.LabelField("Kind", s.IsEvent ? "event" : (s.Reactive ? "reactive query" : "query"));
            if (s.Group != null) EditorGUILayout.LabelField("Group", s.Group.Name);
            if (s.Order != 0) EditorGUILayout.LabelField("Order", s.Order.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(s.QuerySignature(), EditorStyles.wordWrappedMiniLabel, GUILayout.Height(34));
            if (s.Optional.Count > 0)
                EditorGUILayout.LabelField("Optional", string.Join(", ", s.Optional.ConvertAll(EosIntrospection.NiceName)));

            if (s.Before.Count > 0)
                EditorGUILayout.LabelField("Before", string.Join(", ", s.Before.ConvertAll(t => t.Name)));
            if (s.After.Count > 0)
                EditorGUILayout.LabelField("After", string.Join(", ", s.After.ConvertAll(t => t.Name)));
        }

        void LayoutSystems(List<EosVizModel.SystemInfo> systems, bool force)
        {
            // Stack each phase in its own vertical band; within a band, x = update layer.
            const float colW = 210f, rowH = 84f, bandGap = 60f;
            var phaseOrder = new[] { UpdateType.Update, UpdateType.FixedUpdate, UpdateType.LateUpdate };
            float bandY = 20f;

            foreach (var phase in phaseOrder)
            {
                if (_phase != PhaseFilter.All && (int)_phase - 1 != (int)phase) continue;

                var inPhase = systems.FindAll(s => s.Phase == phase);
                if (inPhase.Count == 0) continue;

                var rowOfLayer = new Dictionary<int, int>();
                int maxRow = 0;
                foreach (var s in inPhase)
                {
                    if (!force && _graph.HasPosition(s.Id)) continue;
                    rowOfLayer.TryGetValue(s.Layer, out int row);
                    rowOfLayer[s.Layer] = row + 1;
                    _graph.SetPosition(s.Id, new Vector2(40f + s.Layer * colW, bandY + row * rowH));
                    if (row > maxRow) maxRow = row;
                }
                bandY += (maxRow + 1) * rowH + bandGap;
            }
        }

        bool PhaseShown(UpdateType phase)
            => _phase == PhaseFilter.All || (int)_phase - 1 == (int)phase;

        static readonly Color EdgeColor = new(0.6f, 0.7f, 0.9f, 0.9f);

        static string SystemBadge(EosVizModel.SystemInfo s)
        {
            var kind = s.IsEvent ? "event" : (s.Reactive ? "reactive" : "");
            var grp = s.Group != null ? "[" + s.Group.Name + "]" : "";
            var q = s.Include.Count > 0 ? s.QuerySignature() : "";
            var head = string.IsNullOrEmpty(kind) ? grp : (grp.Length > 0 ? kind + " " + grp : kind);
            return string.IsNullOrEmpty(head) ? q : head + "\n" + q;
        }

        static Color SystemColor(EosVizModel.SystemInfo s)
        {
            if (!s.Enabled) return new Color(0.30f, 0.30f, 0.32f);
            if (s.IsEvent) return new Color(0.38f, 0.28f, 0.46f);
            return s.Phase switch
            {
                UpdateType.FixedUpdate => new Color(0.20f, 0.36f, 0.42f),
                UpdateType.LateUpdate => new Color(0.40f, 0.34f, 0.20f),
                _ => new Color(0.22f, 0.32f, 0.46f),
            };
        }

        // ---- Groups & Archetypes tab -------------------------------------------------

        void DrawGroups(IReadOnlyWorld world)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _groupTab = (GroupTab)GUILayout.Toolbar((int)_groupTab,
                    new[] { "System Groups", "Data Archetypes", "Query Archetypes" },
                    EditorStyles.toolbarButton, GUILayout.Width(360));
                GUILayout.FlexibleSpace();
            }

            _groupScroll = EditorGUILayout.BeginScrollView(_groupScroll);
            switch (_groupTab)
            {
                case GroupTab.SystemGroups: DrawSystemGroups(world); break;
                case GroupTab.DataArchetypes: DrawDataArchetypes(world); break;
                case GroupTab.QueryArchetypes: DrawQueryArchetypes(world); break;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawSystemGroups(IReadOnlyWorld world)
        {
            var systems = EosVizModel.Systems(world);
            var root = EosVizModel.GroupTree(world, systems);
            bool live = world != null && EosIntrospection.IsLive;
            if (!live)
                EditorGUILayout.HelpBox("Enable/disable toggles act on the live world; enter Play mode to use them.", MessageType.None);
            DrawGroupNode(world, root, live, 0);
        }

        void DrawGroupNode(IReadOnlyWorld world, EosVizModel.GroupNode node, bool live, int depth)
        {
            // Root is synthetic: render its children/systems flat.
            if (node.Type != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(depth * 14);
                    bool en = node.Enabled;
                    using (new EditorGUI.DisabledScope(!live))
                    {
                        bool now = EditorGUILayout.ToggleLeft(node.Name, en, EditorStyles.boldLabel, GUILayout.Width(240));
                        if (live && now != en && world != null)
                        {
                            world.SystemGroups.SetEnabled(node.Type, now);
                            node.Enabled = now;
                        }
                    }
                    GUILayout.Label($"{CountSystems(node)} systems", EditorStyles.miniLabel);
                }
            }

            int childDepth = node.Type != null ? depth + 1 : depth;
            foreach (var child in node.Children)
                DrawGroupNode(world, child, live, childDepth);

            foreach (var s in node.Systems)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(childDepth * 14 + 18);
                    var tag = s.IsEvent ? " (event)" : (s.Reactive ? " (reactive)" : "");
                    GUILayout.Label($"• {s.Name}  [{s.Phase}]{tag}", s.Enabled ? EditorStyles.label : EditorStyles.miniLabel);
                }
            }
        }

        static int CountSystems(EosVizModel.GroupNode node)
        {
            int n = node.Systems.Count;
            foreach (var c in node.Children) n += CountSystems(c);
            return n;
        }

        void DrawDataArchetypes(IReadOnlyWorld world)
        {
            if (world == null || !EosIntrospection.IsLive)
            {
                EditorGUILayout.HelpBox("Data archetypes are computed from live entities. Enter Play mode.", MessageType.Info);
                return;
            }

            var archetypes = EosVizModel.DataArchetypes(world);
            var systems = EosVizModel.Systems(world);
            EditorGUILayout.LabelField($"{archetypes.Count} distinct component-sets", EditorStyles.boldLabel);

            int idx = 0;
            foreach (var arch in archetypes)
            {
                string fid = "arch:" + idx++;
                bool exp = _expanded.Contains(fid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool now = EditorGUILayout.Foldout(exp, $"× {arch.Count}   {arch.Label}", true);
                    if (now != exp) { if (now) _expanded.Add(fid); else _expanded.Remove(fid); }
                }
                if (!exp) continue;

                EditorGUI.indentLevel++;
                var matching = systems.FindAll(s => s.Include.Count > 0 && EosVizModel.Matches(s, arch));
                EditorGUILayout.LabelField("Matched by",
                    matching.Count == 0 ? "(no concrete-include system)" : string.Join(", ", matching.ConvertAll(s => s.Name)),
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("Sample entities",
                    string.Join(", ", arch.Sample.ConvertAll(e => "#" + e.Id)), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }
        }

        void DrawQueryArchetypes(IReadOnlyWorld world)
        {
            var systems = EosVizModel.Systems(world);

            // Group systems by identical query signature.
            var bySig = new Dictionary<string, List<EosVizModel.SystemInfo>>();
            foreach (var s in systems)
            {
                var sig = s.QuerySignature();
                if (!bySig.TryGetValue(sig, out var bucket)) { bucket = new(); bySig[sig] = bucket; }
                bucket.Add(s);
            }

            EditorGUILayout.LabelField($"{bySig.Count} distinct query signatures", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Signature = mandatory includes, !excludes, #tags. Interface params and tag matching are approximate.", MessageType.None);

            foreach (var kv in bySig)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.SelectableLabel(kv.Key, EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUI.indentLevel++;
                foreach (var s in kv.Value)
                {
                    var tag = s.IsEvent ? " (event)" : (s.Reactive ? " (reactive)" : "");
                    EditorGUILayout.LabelField($"• {s.Name}  [{s.Phase}]{tag}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
