using System.Collections.Generic;
using System.Globalization;
using ParticleAutoSorting.Editor.Analysis;
using ParticleAutoSorting.Editor.Apply;
using ParticleAutoSorting.Editor.Data;
using ParticleAutoSorting.Editor.Optimization;
using ParticleAutoSorting.Editor.Report;
using UnityEditor;
using UnityEngine;

namespace ParticleAutoSorting.Editor
{
    public partial class ParticleAutoSortingWindow : EditorWindow
    {
        const string MenuPath = "Tools/particle auto sorting";
        const string WindowTitle = "Particle Auto Sorting";
        const string WarnInactive = "비활성 오브젝트 포함";

        const string StatusIdle = "대기 중";
        const string StatusAnalyzeDone = "분석 완료";
        const string StatusApplyDone = "적용 완료";
        const string StatusApplyCancelled = "적용 취소됨";
        const string StatusRecomputeDone = "재계산 완료";
        const string StatusCsvDone = "CSV 내보내기 완료";
        const string StatusRemoved = "프리팹 제거됨";

        [SerializeField] int charSortingOrder;
        [SerializeField] bool editMode;
        [SerializeField] string csvPath;
        [SerializeField] float prefabListHeight = 220f;
        [SerializeField] int fudgeStep = (int)SortingOptimizer.DefaultFudgeStep;
        [SerializeField] bool initialLayoutApplied;

        const int FudgeStepMin = 1;
        const int FudgeStepMax = 10000;

        const float PrefabListMinHeight = 80f;
        const float PrefabListBottomReserve = 200f;
        const float PrefabListSplitterHeight = 6f;
        const float WindowMinWidth = 980f;
        const float WindowMinHeight = 600f;
        const float WindowInitialWidth = 1240f;
        const float WindowInitialHeight = 880f;
        const float PrefabListInitialRatio = 0.7f;
        static readonly Color PrefabListSplitterColor = new Color(0f, 0f, 0f, 0.25f);
        static readonly Color PrefabListSplitterHoverColor = new Color(0.35f, 0.65f, 1f, 0.6f);

        bool _resizingPrefabList;
        float _resizeStartMouseY;
        float _resizeStartHeight;

        readonly List<PrefabData> prefabs = new List<PrefabData>();

        Vector2 scroll;
        string statusMessage = StatusIdle;

        GUIStyle pillStyle;
        GUIStyle entryBoxStyle;
        GUIStyle sectionHeaderStyle;
        GUIStyle tableHeaderStyle;
        GUIStyle overrideLabelStyle;
        GUIStyle applyButtonStyle;

        DragState _drag;

        class DragState
        {
            public PrefabData data;
            public Transform sourceParent;
            public Transform draggingTransform;
            public int hoverInsertIndex = -1;
            public Rect hoverInsertRect;
            public int controlId;
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<ParticleAutoSortingWindow>(WindowTitle);
            window.minSize = new Vector2(WindowMinWidth, WindowMinHeight);
            window.ApplyInitialLayoutIfNeeded();
            window.Show();
        }

        void OnEnable()
        {
            // 도메인 리로드 후 직렬화된 값이 0/음수일 수 있으므로 default 로 보정
            if (fudgeStep < FudgeStepMin) fudgeStep = (int)SortingOptimizer.DefaultFudgeStep;
            if (minSize.x < WindowMinWidth || minSize.y < WindowMinHeight)
                minSize = new Vector2(WindowMinWidth, WindowMinHeight);
        }

        void ApplyInitialLayoutIfNeeded()
        {
            if (initialLayoutApplied) return;

            var pos = position;
            pos.size = new Vector2(
                Mathf.Max(WindowInitialWidth, WindowMinWidth),
                Mathf.Max(WindowInitialHeight, WindowMinHeight));
            position = pos;

            var maxHeight = Mathf.Max(PrefabListMinHeight, pos.height - PrefabListBottomReserve);
            prefabListHeight = Mathf.Clamp(pos.height * PrefabListInitialRatio,
                PrefabListMinHeight, maxHeight);

            initialLayoutApplied = true;
        }

        void OnGUI()
        {
            EnsureStyles();

            DrawTopBar();
            EditorGUILayout.Space(4);
            DrawDropZone();
            EditorGUILayout.Space(4);
            DrawPrefabList();
            DrawPrefabListSplitter();
            EditorGUILayout.Space(4);
            DrawBottomButtons();
            EditorGUILayout.Space(4);
            DrawCsvPanel();
            GUILayout.FlexibleSpace();
            DrawStatusBar();

            HandleDragCleanup();
        }

        void HandleDragCleanup()
        {
            if (_drag == null) return;

            var evt = Event.current;
            if (evt.type == EventType.MouseUp || evt.type == EventType.MouseLeaveWindow)
            {
                if (_drag.controlId != 0 && GUIUtility.hotControl == _drag.controlId)
                    GUIUtility.hotControl = 0;
                _drag = null;
                Repaint();
            }
        }

        void EnsureStyles()
        {
            if (pillStyle == null)
            {
                pillStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 1, 1),
                    margin = new RectOffset(2, 2, 2, 2),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            if (entryBoxStyle == null)
            {
                entryBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(6, 6, 4, 4),
                    margin = new RectOffset(0, 0, 2, 2)
                };
            }

            if (sectionHeaderStyle == null)
            {
                sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    padding = new RectOffset(2, 2, 2, 2),
                    margin = new RectOffset(0, 0, 4, 2)
                };
            }

            if (tableHeaderStyle == null)
            {
                tableHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    padding = new RectOffset(2, 2, 1, 1)
                };
            }

            if (overrideLabelStyle == null)
            {
                overrideLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.35f, 0.65f, 1f) },
                    fontStyle = FontStyle.Bold
                };
            }

            if (applyButtonStyle == null)
            {
                applyButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }

        void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(WindowTitle, EditorStyles.boldLabel, GUILayout.Width(160));
                GUILayout.FlexibleSpace();

                GUILayout.Label("Char Sorting Order", GUILayout.Width(130));
                int newOrder = EditorGUILayout.IntField(charSortingOrder, GUILayout.Width(80));
                if (newOrder != charSortingOrder)
                {
                    charSortingOrder = newOrder;
                    RecomputeAll();
                }

                GUILayout.Space(12);

                var modeLabel = editMode ? "수정 모드: ON" : "수정 모드: OFF";
                var modeContent = new GUIContent(modeLabel, "ON: ≡ 핸들로 같은 부모 내 sibling 순서를 드래그로 변경할 수 있습니다. Fudge 간격도 조정할 수 있습니다.");
                bool newEdit = GUILayout.Toggle(editMode, modeContent, EditorStyles.toolbarButton, GUILayout.Width(120));
                if (newEdit != editMode)
                {
                    editMode = newEdit;
                    Repaint();
                }

                if (editMode)
                {
                    GUILayout.Space(8);
                    var stepContent = new GUIContent(
                        "Fudge 간격",
                        $"같은 런 내 인접 오브젝트 간 sortingFudge 차이 (default {SortingOptimizer.DefaultFudgeStep:0}). 1 이상의 정수.");
                    GUILayout.Label(stepContent, GUILayout.Width(70));
                    int newStep = EditorGUILayout.IntField(fudgeStep, GUILayout.Width(60));
                    newStep = Mathf.Clamp(newStep, FudgeStepMin, FudgeStepMax);
                    if (newStep != fudgeStep)
                    {
                        fudgeStep = newStep;
                        RecomputeAll();
                    }

                    GUILayout.Space(12);
                    var instLabel = new GUIContent(
                        "GPU Inst",
                        "모든 프리팹의 Material GPU Instancing 을 일괄 토글합니다.");
                    GUILayout.Label(instLabel, GUILayout.Width(60));
                    using (new EditorGUI.DisabledScope(prefabs.Count == 0))
                    {
                        if (GUILayout.Button("모두 ON", EditorStyles.toolbarButton, GUILayout.Width(70)))
                            SetAllInstancing(true);
                        if (GUILayout.Button("모두 OFF", EditorStyles.toolbarButton, GUILayout.Width(70)))
                            SetAllInstancing(false);
                    }
                }
            }
        }

        void DrawDropZone()
        {
            EditorGUILayout.LabelField("프리팹 등록", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(0, 72, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "프리팹을 이 영역에 드래그하세요 (복수 등록 지원)", EditorStyles.helpBox);
            HandleDragAndDrop(rect);
        }

        void HandleDragAndDrop(Rect dropRect)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = HasAnyPrefab(DragAndDrop.objectReferences)
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    RegisterPrefabs(DragAndDrop.objectReferences);
                    evt.Use();
                    break;
            }
        }

        static bool HasAnyPrefab(Object[] objects)
        {
            foreach (var obj in objects)
            {
                if (ExtractPrefab(obj) != null) return true;
            }
            return false;
        }

        static GameObject ExtractPrefab(Object obj)
        {
            if (obj is GameObject go)
            {
                var type = PrefabUtility.GetPrefabAssetType(go);
                if (type == PrefabAssetType.Regular || type == PrefabAssetType.Variant || type == PrefabAssetType.Model)
                    return go;
            }
            return null;
        }

        void RegisterPrefabs(Object[] objects)
        {
            int added = 0;
            int duplicates = 0;
            int rejected = 0;

            foreach (var obj in objects)
            {
                var prefab = ExtractPrefab(obj);
                if (prefab == null)
                {
                    rejected++;
                    continue;
                }

                if (IsAlreadyRegistered(prefab))
                {
                    duplicates++;
                    continue;
                }

                var data = PrefabAnalyzer.Analyze(prefab);
                SortingOptimizer.Optimize(data, charSortingOrder, fudgeStep);
                BatchCounter.CountPrefab(data);
                prefabs.Add(data);
                added++;
            }

            if (duplicates > 0)
                EditorUtility.DisplayDialog("중복 등록", $"이미 등록된 프리팹 {duplicates}개가 거부되었습니다.", "확인");

            if (added > 0)
                statusMessage = $"{StatusAnalyzeDone}: {added}개 등록";
            else if (rejected > 0 && duplicates == 0)
                statusMessage = "프리팹 자산이 아닙니다";

            Repaint();
        }

        bool IsAlreadyRegistered(GameObject prefab)
        {
            foreach (var data in prefabs)
            {
                if (data.Prefab == prefab) return true;
            }
            return false;
        }

        void RecomputeAll()
        {
            foreach (var data in prefabs)
            {
                SortingOptimizer.Optimize(data, charSortingOrder, fudgeStep);
                BatchCounter.CountPrefab(data);
                data.NeedsPreviewRefresh = false;
            }
            statusMessage = StatusRecomputeDone;
            Repaint();
        }

        void DrawPrefabListSplitter()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(PrefabListSplitterHeight));

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.Repaint:
                    var color = _resizingPrefabList || rect.Contains(evt.mousePosition)
                        ? PrefabListSplitterHoverColor
                        : PrefabListSplitterColor;
                    var line = new Rect(rect.x + 4, rect.y + rect.height * 0.5f - 1f, rect.width - 8, 2f);
                    EditorGUI.DrawRect(line, color);
                    break;

                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        _resizingPrefabList = true;
                        _resizeStartMouseY = evt.mousePosition.y;
                        _resizeStartHeight = prefabListHeight;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_resizingPrefabList)
                    {
                        var delta = evt.mousePosition.y - _resizeStartMouseY;
                        var maxHeight = Mathf.Max(PrefabListMinHeight,
                            position.height - PrefabListBottomReserve);
                        prefabListHeight = Mathf.Clamp(_resizeStartHeight + delta,
                            PrefabListMinHeight, maxHeight);
                        evt.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (_resizingPrefabList)
                    {
                        _resizingPrefabList = false;
                        evt.Use();
                    }
                    break;
            }
        }

        void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(statusMessage);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"등록된 프리팹: {prefabs.Count}");
            }
        }
    }
}
