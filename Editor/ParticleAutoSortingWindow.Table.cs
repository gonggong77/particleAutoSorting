using System.Collections.Generic;
using System.Globalization;
using ParticleAutoSorting.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace ParticleAutoSorting.Editor
{
    public partial class ParticleAutoSortingWindow
    {
        const float ColObject = 180f;
        const float ColMaterial = 170f;
        const float ColOil = 60f;
        const float ColFudge = 68f;
        const float ColWarn = 22f;
        const float ColHandle = 18f;

        static readonly Color DragGuideColor = new Color(0.20f, 0.55f, 1f, 0.95f);
        static readonly Color ParentDividerColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        void DrawRendererTable(PrefabData data)
        {
            DrawInstancingSection(data);

            DrawInterleavePairList(data);

            if (data.HasAboveBelow)
            {
                DrawSection(data, "Above");
                DrawSection(data, "Below");
            }
            else if (data.IsDefaultedToAbove)
            {
                EditorGUILayout.HelpBox(
                    "Above/Below 구조가 설정되지 않아 모든 렌더러를 Above로 자동 처리했습니다.\n" +
                    "프리팹 계층에 'Above' 및 'Below' 이름의 GameObject를 추가하면 더 정밀하게 제어할 수 있습니다.",
                    MessageType.Info);
                DrawSection(data, "Above");
            }
            else
            {
                DrawSection(data, "Root");
            }
        }

        static List<Material> CollectUniqueMaterials(PrefabData data)
        {
            var result = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (var r in data.Renderers)
            {
                var mat = r.SharedMaterial;
                if (mat == null) continue;
                if (seen.Add(mat)) result.Add(mat);
            }
            return result;
        }

        void DrawInstancingSection(PrefabData data)
        {
            if (!editMode) return;

            var mats = CollectUniqueMaterials(data);
            if (mats.Count == 0) return;

            using (new EditorGUILayout.VerticalScope(entryBoxStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("GPU Instancing (수정 모드)", sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("모두 ON", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                        SetPrefabInstancing(data, true);
                    if (GUILayout.Button("모두 OFF", EditorStyles.miniButtonRight, GUILayout.Width(70)))
                        SetPrefabInstancing(data, false);
                }

                EditorGUILayout.HelpBox(
                    "⚠️ 이 기능은 Material의 GPU Instancing 토글만 제어합니다. " +
                    "셰이더가 Instancing을 지원하지 않으면 토글을 켜도 효과가 없습니다.",
                    MessageType.None);

                foreach (var mat in mats)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var color = MaterialColor(mat);
                        DrawPill(mat.name, color, mat.name);

                        GUILayout.Label(mat.name, GUILayout.Width(ColMaterial));

                        bool current = mat.enableInstancing;
                        bool pending = data.InstancingOverrides.TryGetValue(mat, out var overrideValue)
                            ? overrideValue
                            : current;
                        bool dirty = pending != current;

                        var label = "현재: " + (current ? "ON" : "OFF");
                        var labelStyle = dirty ? overrideLabelStyle : EditorStyles.label;
                        GUILayout.Label(label, labelStyle, GUILayout.Width(80));

                        bool edited = EditorGUILayout.Toggle(pending, GUILayout.Width(20));
                        if (edited != pending)
                        {
                            if (edited == current)
                                data.InstancingOverrides.Remove(mat);
                            else
                                data.InstancingOverrides[mat] = edited;
                        }

                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        void SetPrefabInstancing(PrefabData data, bool target)
        {
            if (data == null) return;
            var mats = CollectUniqueMaterials(data);
            foreach (var mat in mats)
            {
                if (mat == null) continue;
                bool current = mat.enableInstancing;
                if (target == current)
                    data.InstancingOverrides.Remove(mat);
                else
                    data.InstancingOverrides[mat] = target;
            }
            Repaint();
        }

        void SetAllInstancing(bool target)
        {
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                SetPrefabInstancing(p, target);
            }
        }

        void DrawInterleavePairList(PrefabData data)
        {
            var pairs = CollectInterleavePairs(data);
            if (pairs.Count == 0) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("인터리브된 Material 쌍", EditorStyles.miniBoldLabel);
                foreach (var p in pairs)
                    EditorGUILayout.LabelField($"• {p.a}  ↔  {p.b}", EditorStyles.miniLabel);
            }
        }

        static List<(string a, string b)> CollectInterleavePairs(PrefabData data)
        {
            var result = new List<(string, string)>();
            var seen = new HashSet<string>();

            foreach (var r in data.Renderers)
            {
                if (!r.HasInterleaveWarning) continue;
                if (r.SharedMaterial == null) continue;
                string myName = r.SharedMaterial.name ?? "<null>";

                foreach (var otherName in r.InterleavedWith)
                {
                    string a = string.CompareOrdinal(myName, otherName) <= 0 ? myName : otherName;
                    string b = string.CompareOrdinal(myName, otherName) <= 0 ? otherName : myName;
                    string key = a + "||" + b;
                    if (seen.Add(key))
                        result.Add((a, b));
                }
            }

            return result;
        }

        void DrawSection(PrefabData data, string groupTag)
        {
            var rows = FilterByGroup(data, groupTag);
            if (rows.Count == 0) return;

            EditorGUILayout.LabelField(groupTag, sectionHeaderStyle);
            DrawTableHeader();

            var seenTransforms = new HashSet<Transform>();
            var orderedTransforms = new List<Transform>();
            var rowRectsByTransform = new Dictionary<Transform, Rect>();
            Transform prevParent = null;
            bool firstParent = true;

            foreach (var r in rows)
            {
                Transform t = r.Renderer != null ? r.Renderer.transform : null;
                bool isFirst = t != null && seenTransforms.Add(t);

                if (isFirst && t != null)
                {
                    var curParent = t.parent;
                    if (!firstParent && curParent != prevParent)
                        DrawParentDivider(curParent);
                    prevParent = curParent;
                    firstParent = false;
                    orderedTransforms.Add(t);
                }

                var rowRect = DrawTableRow(data, r, isFirst);

                if (t != null)
                {
                    if (rowRectsByTransform.TryGetValue(t, out var existing))
                    {
                        float xMin = Mathf.Min(existing.xMin, rowRect.xMin);
                        float yMin = Mathf.Min(existing.yMin, rowRect.yMin);
                        float xMax = Mathf.Max(existing.xMax, rowRect.xMax);
                        float yMax = Mathf.Max(existing.yMax, rowRect.yMax);
                        rowRectsByTransform[t] = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                    }
                    else
                    {
                        rowRectsByTransform[t] = rowRect;
                    }
                }
            }

            if (editMode && _drag != null && _drag.data == data)
                HandleSiblingDrag(data, orderedTransforms, rowRectsByTransform);
        }

        void DrawParentDivider(Transform parent)
        {
            var line = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(line, ParentDividerColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (editMode) GUILayout.Space(ColHandle);
                GUILayout.Space(ColWarn);
                GUILayout.Label(
                    new GUIContent("▼ " + (parent != null ? parent.name : "<root>"),
                                   "부모 Transform 경계 — 위/아래 행은 부모가 달라 서로 드래그로 이동할 수 없습니다."),
                    EditorStyles.miniLabel);
            }
        }

        static List<RendererInfo> FilterByGroup(PrefabData data, string groupTag)
        {
            var list = new List<RendererInfo>();
            foreach (var r in data.Renderers)
            {
                var tag = string.IsNullOrEmpty(r.GroupTag) ? "Root" : r.GroupTag;
                if (tag == groupTag) list.Add(r);
            }
            list.Sort((x, y) => x.HierarchyOrder.CompareTo(y.HierarchyOrder));
            return list;
        }

        void DrawTableHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (editMode) GUILayout.Space(ColHandle);
                GUILayout.Space(ColWarn);
                GUILayout.Label("Object", tableHeaderStyle, GUILayout.Width(ColObject));
                GUILayout.Label("Material", tableHeaderStyle, GUILayout.Width(ColMaterial));
                GUILayout.Label("OiL B", tableHeaderStyle, GUILayout.Width(ColOil));
                GUILayout.Label("OiL A", tableHeaderStyle, GUILayout.Width(ColOil));
                GUILayout.Label("Fudge B", tableHeaderStyle, GUILayout.Width(ColFudge));
                GUILayout.Label("Fudge A", tableHeaderStyle, GUILayout.Width(ColFudge));
                GUILayout.FlexibleSpace();
            }
        }

        Rect DrawTableRow(PrefabData data, RendererInfo r, bool isFirstRowOfTransform)
        {
            var rowRect = EditorGUILayout.BeginHorizontal();
            {
                if (editMode)
                {
                    if (isFirstRowOfTransform && r.Renderer != null)
                        DrawDragHandle(data, r.Renderer.transform);
                    else
                        GUILayout.Space(ColHandle);
                }

                if (r.HasInterleaveWarning)
                {
                    var tip = r.InterleavedWith.Count > 0
                        ? "인터리브 대상: " + string.Join(", ", r.InterleavedWith)
                        : "인터리브 감지됨";
                    GUILayout.Label(new GUIContent("⚠", tip), GUILayout.Width(ColWarn));
                }
                else
                {
                    GUILayout.Space(ColWarn);
                }

                GUILayout.Label(new GUIContent(r.ObjectName, KindLabel(r.Kind)), GUILayout.Width(ColObject));

                DrawMaterialCell(r);

                GUILayout.Label(r.OilBefore.ToString(CultureInfo.InvariantCulture), GUILayout.Width(ColOil));
                DrawOilAfterCell(r);

                GUILayout.Label(r.FudgeBefore.ToString("0.###", CultureInfo.InvariantCulture), GUILayout.Width(ColFudge));
                DrawFudgeAfterCell(r);

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
            return rowRect;
        }

        void DrawDragHandle(PrefabData data, Transform t)
        {
            var content = new GUIContent("≡", "드래그하여 같은 부모 안에서 sibling 순서 변경");
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniLabel,
                GUILayout.Width(ColHandle));
            GUI.Label(rect, content, EditorStyles.miniLabel);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                int id = GUIUtility.GetControlID(FocusType.Passive);
                _drag = new DragState
                {
                    data = data,
                    sourceParent = t.parent,
                    draggingTransform = t,
                    hoverInsertIndex = -1,
                    controlId = id,
                };
                GUIUtility.hotControl = id;
                evt.Use();
            }
        }

        void HandleSiblingDrag(PrefabData data, List<Transform> ordered, Dictionary<Transform, Rect> rects)
        {
            if (_drag == null || _drag.draggingTransform == null) return;

            var siblings = new List<Transform>();
            foreach (var t in ordered)
            {
                if (t != null && t.parent == _drag.sourceParent)
                    siblings.Add(t);
            }
            if (siblings.Count == 0) return;

            var evt = Event.current;
            float mouseY = evt.mousePosition.y;

            int insertIdx = siblings.Count;
            for (int i = 0; i < siblings.Count; i++)
            {
                if (!rects.TryGetValue(siblings[i], out var rr)) continue;
                float midY = rr.yMin + rr.height * 0.5f;
                if (mouseY < midY) { insertIdx = i; break; }
            }
            _drag.hoverInsertIndex = insertIdx;

            float xMin = 0f, xMax = 0f, lineY = 0f;
            bool haveLine = false;
            if (insertIdx <= 0 && rects.TryGetValue(siblings[0], out var firstRect))
            {
                xMin = firstRect.xMin; xMax = firstRect.xMax;
                lineY = firstRect.yMin; haveLine = true;
            }
            else if (insertIdx >= siblings.Count && rects.TryGetValue(siblings[siblings.Count - 1], out var lastRect))
            {
                xMin = lastRect.xMin; xMax = lastRect.xMax;
                lineY = lastRect.yMax; haveLine = true;
            }
            else if (rects.TryGetValue(siblings[insertIdx - 1], out var prevRect))
            {
                xMin = prevRect.xMin; xMax = prevRect.xMax;
                lineY = prevRect.yMax; haveLine = true;
            }

            if (haveLine)
                _drag.hoverInsertRect = new Rect(xMin, lineY - 1f, xMax - xMin, 2f);

            if (evt.type == EventType.Repaint && haveLine)
                EditorGUI.DrawRect(_drag.hoverInsertRect, DragGuideColor);

            if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == _drag.controlId)
            {
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == _drag.controlId)
            {
                if (_drag.hoverInsertIndex >= 0)
                    ApplyReorder(data, _drag.draggingTransform, _drag.hoverInsertIndex);
                if (GUIUtility.hotControl == _drag.controlId) GUIUtility.hotControl = 0;
                _drag = null;
                Repaint();
                evt.Use();
            }
        }

        static void ApplyReorder(PrefabData data, Transform target, int insertIndex)
        {
            if (data == null || target == null) return;

            var siblings = new List<Transform>();
            var baseByT = new Dictionary<Transform, float>();
            foreach (var r in data.Renderers)
            {
                if (r.Renderer == null) continue;
                var t = r.Renderer.transform;
                if (t.parent != target.parent) continue;
                if (!baseByT.ContainsKey(t))
                {
                    baseByT[t] = Mathf.Floor(r.HierarchyOrder);
                    siblings.Add(t);
                }
            }
            siblings.Sort((a, b) => baseByT[a].CompareTo(baseByT[b]));

            int oldIdx = siblings.IndexOf(target);
            if (oldIdx < 0) return;

            int newIdx = Mathf.Clamp(insertIndex, 0, siblings.Count);
            if (newIdx > oldIdx) newIdx--;
            if (newIdx == oldIdx) return;

            var slots = new List<float>();
            foreach (var t in siblings) slots.Add(baseByT[t]);

            var moved = siblings[oldIdx];
            siblings.RemoveAt(oldIdx);
            siblings.Insert(newIdx, moved);

            var newBase = new Dictionary<Transform, float>();
            for (int i = 0; i < siblings.Count; i++) newBase[siblings[i]] = slots[i];

            foreach (var r in data.Renderers)
            {
                if (r.Renderer == null) continue;
                var t = r.Renderer.transform;
                if (!newBase.TryGetValue(t, out var b)) continue;
                float frac = r.HierarchyOrder - Mathf.Floor(r.HierarchyOrder);
                r.HierarchyOrder = b + frac;
            }

            data.HasSiblingReorder = true;
            data.NeedsPreviewRefresh = true;
        }

        static string KindLabel(RendererKind kind)
        {
            switch (kind)
            {
                case RendererKind.Particle: return "ParticleSystemRenderer";
                case RendererKind.Trail_Component: return "TrailRenderer";
                case RendererKind.Trail_Module: return "ParticleSystem Trails Module";
                default: return kind.ToString();
            }
        }

        void DrawMaterialCell(RendererInfo r)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(ColMaterial)))
            {
                string matName = r.SharedMaterial != null ? r.SharedMaterial.name : "<null>";
                var color = MaterialColor(r.SharedMaterial);
                DrawPill(matName, color, matName);

                if (r.IsInstancingDisabled)
                {
                    GUILayout.Label(
                        new GUIContent("ⓘ", "GPU Instancing 비활성화"),
                        GUILayout.Width(16));
                }

                GUILayout.FlexibleSpace();
            }
        }

        void DrawOilAfterCell(RendererInfo r)
        {
            int displayed = r.OilOverride ?? r.OilAfterAI;
            bool overridden = r.OilOverride.HasValue && r.OilOverride.Value != r.OilAfterAI;

            if (editMode)
            {
                int edited = EditorGUILayout.IntField(displayed, GUILayout.Width(ColOil));
                if (edited != displayed)
                {
                    r.OilOverride = edited == r.OilAfterAI ? (int?)null : edited;
                }
            }
            else
            {
                var style = overridden ? overrideLabelStyle : EditorStyles.label;
                GUILayout.Label(displayed.ToString(CultureInfo.InvariantCulture), style, GUILayout.Width(ColOil));
            }
        }

        void DrawFudgeAfterCell(RendererInfo r)
        {
            float displayed = r.FudgeOverride ?? r.FudgeAfterAI;
            bool overridden = r.FudgeOverride.HasValue
                && !Mathf.Approximately(r.FudgeOverride.Value, r.FudgeAfterAI);

            if (editMode)
            {
                float edited = EditorGUILayout.FloatField(displayed, GUILayout.Width(ColFudge));
                if (!Mathf.Approximately(edited, displayed))
                {
                    r.FudgeOverride = Mathf.Approximately(edited, r.FudgeAfterAI)
                        ? (float?)null
                        : edited;
                }
            }
            else
            {
                var style = overridden ? overrideLabelStyle : EditorStyles.label;
                GUILayout.Label(displayed.ToString("0.###", CultureInfo.InvariantCulture), style, GUILayout.Width(ColFudge));
            }
        }

        static Color MaterialColor(Material mat)
        {
            if (mat == null) return new Color(0.55f, 0.55f, 0.55f);
            int id = Mathf.Abs(mat.GetInstanceID());
            float hue = (id % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.85f);
        }
    }
}
