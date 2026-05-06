using System.Collections.Generic;
using ParticleAutoSorting.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace ParticleAutoSorting.Editor
{
    public partial class ParticleAutoSortingWindow
    {
        static readonly Color ColorOverflow = new Color(0.70f, 0.10f, 0.10f);
        static readonly Color ColorInterleave = new Color(0.95f, 0.55f, 0.10f);
        static readonly Color ColorInstancingOff = new Color(0.90f, 0.25f, 0.25f);
        static readonly Color ColorNoAboveBelow = new Color(0.50f, 0.50f, 0.50f);
        static readonly Color ColorAutoAbove = new Color(0.30f, 0.55f, 0.90f);
        static readonly Color ColorInactive = new Color(0.90f, 0.80f, 0.20f);
        static readonly Color ColorPreviewDirty = new Color(0.95f, 0.55f, 0.10f);

        void DrawPrefabList()
        {
            EditorGUILayout.LabelField($"등록된 프리팹 ({prefabs.Count})", EditorStyles.boldLabel);
            using (var scope = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(prefabListHeight)))
            {
                scroll = scope.scrollPosition;
                if (prefabs.Count == 0)
                {
                    EditorGUILayout.HelpBox("등록된 프리팹이 없습니다. 위 영역에 드래그하세요.", MessageType.Info);
                    return;
                }

                int removeIndex = -1;
                for (int i = 0; i < prefabs.Count; i++)
                {
                    if (DrawEntry(prefabs[i]))
                        removeIndex = i;
                }

                if (removeIndex >= 0)
                {
                    prefabs.RemoveAt(removeIndex);
                    statusMessage = StatusRemoved;
                    Repaint();
                }
            }
        }

        bool DrawEntry(PrefabData data)
        {
            bool remove = false;
            var borderColor = PickBorderColor(data);

            var prevColor = GUI.color;
            if (!data.IsSelectedForApply)
                GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.55f);

            var boxRect = EditorGUILayout.BeginVertical(entryBoxStyle);
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    data.IsSelectedForApply = EditorGUILayout.Toggle(data.IsSelectedForApply, GUILayout.Width(18));

                    var arrow = data.IsExpanded ? "▼" : "▶";
                    if (GUILayout.Button(arrow, EditorStyles.label, GUILayout.Width(18)))
                        data.IsExpanded = !data.IsExpanded;

                    var name = data.Prefab != null ? data.Prefab.name : "(null)";
                    GUILayout.Label(name, EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    GUILayout.Label($"배치 {data.BatchBefore} → {data.BatchAfter}", EditorStyles.miniLabel);

                    if (GUILayout.Button("×", GUILayout.Width(24)))
                        remove = true;
                }

                DrawWarningPills(data);

                if (data.IsExpanded)
                {
                    EditorGUILayout.Space(2);
                    DrawRendererTable(data);
                }
            }
            EditorGUILayout.EndVertical();

            if (borderColor.HasValue && Event.current.type == EventType.Repaint)
                DrawBorder(boxRect, borderColor.Value);

            GUI.color = prevColor;
            return remove;
        }

        void DrawWarningPills(PrefabData data)
        {
            var pills = CollectPills(data);
            bool showDirty = editMode && data.NeedsPreviewRefresh;
            if (pills.Count == 0 && !showDirty) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(36);
                foreach (var p in pills)
                    DrawPill(p.label, p.color, null);
                if (showDirty)
                    DrawPill("↻ 미리보기 필요", ColorPreviewDirty, "행 순서가 변경되었습니다. [미리보기] 를 눌러 OiL/Fudge/배치수를 갱신하세요.");
                GUILayout.FlexibleSpace();
            }
        }

        static List<(string label, Color color)> CollectPills(PrefabData data)
        {
            var list = new List<(string, Color)>();

            if (data.HasOverflow)
                list.Add(("OiL 범위 초과", ColorOverflow));

            bool anyInterleave = false;
            bool anyInstancingOff = false;
            foreach (var r in data.Renderers)
            {
                if (r.HasInterleaveWarning) anyInterleave = true;
                if (r.IsInstancingDisabled) anyInstancingOff = true;
            }

            if (anyInterleave)
                list.Add(("인터리브", ColorInterleave));

            if (anyInstancingOff)
                list.Add(("Instancing OFF", ColorInstancingOff));

            if (data.IsDefaultedToAbove)
                list.Add(("Above 자동 적용", ColorAutoAbove));
            else if (!data.HasAboveBelow)
                list.Add(("Above/Below 없음", ColorNoAboveBelow));

            if (data.Warnings != null && data.Warnings.Contains(WarnInactive))
                list.Add(("비활성 오브젝트", ColorInactive));

            return list;
        }

        void DrawPill(string text, Color color, string tooltip)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            var content = new GUIContent(text, tooltip);
            var size = pillStyle.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(size.x + 8, 16, GUILayout.Width(size.x + 8));
            GUI.Box(rect, content, pillStyle);
            GUI.backgroundColor = prevBg;
        }

        static void DrawBorder(Rect rect, Color color)
        {
            const float t = 2f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
        }

        static Color? PickBorderColor(PrefabData data)
        {
            if (data.HasOverflow) return ColorOverflow;

            bool anyInterleave = false;
            bool anyInstancingOff = false;
            foreach (var r in data.Renderers)
            {
                if (r.HasInterleaveWarning) anyInterleave = true;
                if (r.IsInstancingDisabled) anyInstancingOff = true;
            }

            if (anyInstancingOff) return ColorInstancingOff;
            if (anyInterleave) return ColorInterleave;
            if (data.Warnings != null && data.Warnings.Contains(WarnInactive))
                return ColorInactive;
            if (!data.HasAboveBelow && !data.IsDefaultedToAbove) return ColorNoAboveBelow;

            return null;
        }
    }
}
