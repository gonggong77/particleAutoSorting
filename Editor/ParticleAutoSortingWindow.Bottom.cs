using System.IO;
using ParticleAutoSorting.Editor.Analysis;
using ParticleAutoSorting.Editor.Apply;
using ParticleAutoSorting.Editor.Optimization;
using ParticleAutoSorting.Editor.Report;
using UnityEditor;
using UnityEngine;

namespace ParticleAutoSorting.Editor
{
    public partial class ParticleAutoSortingWindow
    {
        void DrawBottomButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int selected = SelectedCount();
                int total = prefabs.Count;

                bool allSelected = total > 0 && selected == total;
                string toggleLabel = allSelected ? "전체 해제" : "전체 선택";
                using (new EditorGUI.DisabledScope(total == 0))
                {
                    if (GUILayout.Button(toggleLabel, GUILayout.Width(90)))
                    {
                        SetAllSelected(!allSelected);
                        Repaint();
                    }
                }

                GUILayout.Label($"{selected} / {total}개 선택", GUILayout.Width(120));

                var (before, after) = SelectedBatchTotal();
                GUILayout.Label($"전체 배치 수: {before} → {after}", EditorStyles.label);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(total == 0))
                {
                    GUIContent recomputeContent = editMode
                        ? new GUIContent("미리보기", "현재 변경된 순서/오버라이드 기준으로 OiL · Fudge · 배치수를 다시 계산합니다. 프리팹은 수정되지 않습니다.")
                        : new GUIContent("재계산", "OiL · Fudge · 배치수를 다시 계산합니다.");
                    if (GUILayout.Button(recomputeContent, GUILayout.Width(80)))
                    {
                        RecomputeAll();
                    }
                }

                if (GUILayout.Button("Undo", GUILayout.Width(70)))
                {
                    EditorApplication.delayCall += () =>
                    {
                        Undo.PerformUndo();
                        statusMessage = "Undo 실행";
                        Repaint();
                    };
                }

                using (new EditorGUI.DisabledScope(selected == 0))
                {
                    var reorderContent = new GUIContent(
                        "하이어라키만 재정렬",
                        "기존 OiL/Fudge 값은 그대로 두고, sibling 순서만 (OiL DESC, Fudge ASC) 로 재배치합니다.");
                    if (GUILayout.Button(reorderContent, GUILayout.Width(140)))
                    {
                        RunHierarchyReorder();
                    }
                }

                using (new EditorGUI.DisabledScope(selected == 0))
                {
                    if (GUILayout.Button("적용", applyButtonStyle, GUILayout.Width(90)))
                    {
                        RunApply();
                    }
                }
            }
        }

        int SelectedCount()
        {
            int n = 0;
            foreach (var p in prefabs)
                if (p != null && p.IsSelectedForApply) n++;
            return n;
        }

        void SetAllSelected(bool value)
        {
            foreach (var p in prefabs)
                if (p != null) p.IsSelectedForApply = value;
        }

        (int before, int after) SelectedBatchTotal()
        {
            int before = 0;
            int after = 0;
            foreach (var p in prefabs)
            {
                if (p == null || !p.IsSelectedForApply) continue;
                before += p.BatchBefore;
                after += p.BatchAfter;
            }
            return (before, after);
        }

        void RunApply()
        {
            bool ok = PrefabApplier.Apply(prefabs, charSortingOrder, fudgeStep);
            if (ok)
            {
                foreach (var p in prefabs)
                {
                    if (p == null || !p.IsSelectedForApply) continue;
                    BatchCounter.CountPrefab(p);
                }
                statusMessage = $"{StatusApplyDone}: {SelectedCount()}개";
            }
            else
            {
                statusMessage = StatusApplyCancelled;
            }
            Repaint();
        }

        void RunHierarchyReorder()
        {
            int selectedBefore = SelectedCount();
            bool ok = HierarchyReorderer.Apply(prefabs);
            if (ok)
            {
                // 디스크의 sibling 순서가 바뀌었으므로 영향받은 프리팹을 재분석해
                // 리스트/테이블이 새 순서를 반영하도록 갱신.
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var old = prefabs[i];
                    if (old == null || !old.IsSelectedForApply) continue;
                    if (old.Prefab == null) continue;

                    var fresh = PrefabAnalyzer.Analyze(old.Prefab);
                    fresh.IsExpanded = old.IsExpanded;
                    fresh.IsSelectedForApply = old.IsSelectedForApply;
                    fresh.InstancingOverrides = old.InstancingOverrides;

                    SortingOptimizer.Optimize(fresh, charSortingOrder, fudgeStep);
                    BatchCounter.CountPrefab(fresh);
                    prefabs[i] = fresh;
                }
                statusMessage = $"하이어라키 재정렬 완료: {selectedBefore}개";
            }
            else
            {
                statusMessage = "하이어라키 재정렬 취소됨";
            }
            Repaint();
        }

        void DrawCsvPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("보고서 내보내기 (CSV)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("경로", GUILayout.Width(40));
                    csvPath = EditorGUILayout.TextField(csvPath ?? string.Empty);

                    if (GUILayout.Button("찾아보기", GUILayout.Width(80)))
                    {
                        var picked = CsvReportExporter.ShowSavePanel();
                        if (!string.IsNullOrEmpty(picked))
                        {
                            csvPath = picked;
                            GUI.FocusControl(null);
                            Repaint();
                        }
                    }

                    using (new EditorGUI.DisabledScope(prefabs.Count == 0))
                    {
                        if (GUILayout.Button("내보내기", GUILayout.Width(90)))
                        {
                            RunCsvExport();
                        }
                    }
                }
            }
        }

        void RunCsvExport()
        {
            var path = csvPath;
            if (string.IsNullOrEmpty(path))
            {
                path = CsvReportExporter.ShowSavePanel();
                if (string.IsNullOrEmpty(path)) return;
                csvPath = path;
            }

            bool ok = CsvReportExporter.Export(prefabs, path);
            if (ok)
            {
                statusMessage = $"{StatusCsvDone}: {Path.GetFileName(path)}";
            }
            Repaint();
        }
    }
}
