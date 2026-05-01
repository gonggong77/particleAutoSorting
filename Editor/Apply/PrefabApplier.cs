using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ParticleAutoSorting.Editor.Analysis;
using ParticleAutoSorting.Editor.Data;
using ParticleAutoSorting.Editor.Optimization;

namespace ParticleAutoSorting.Editor.Apply
{
    public static class PrefabApplier
    {
        private const string UndoGroupName = "Particle Auto Sorting Apply";

        public static bool Apply(IEnumerable<PrefabData> prefabs, int charSortingOrder, float fudgeStep = SortingOptimizer.DefaultFudgeStep)
        {
            if (prefabs == null) return false;

            var targets = new List<PrefabData>();
            foreach (var p in prefabs)
            {
                if (p == null || p.Prefab == null) continue;
                if (!p.IsSelectedForApply) continue;
                targets.Add(p);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Particle Auto Sorting",
                    "적용할 프리팹이 선택되지 않았습니다.",
                    "확인");
                return false;
            }

            // 미리보기 미클릭 케이스 보장: 최신 순서/오버라이드 기준으로 재계산
            foreach (var p in targets)
            {
                SortingOptimizer.Optimize(p, charSortingOrder, fudgeStep);
                BatchCounter.CountPrefab(p);
                p.NeedsPreviewRefresh = false;
            }

            var overflowTargets = new List<PrefabData>();
            foreach (var p in targets)
            {
                if (p.HasOverflow) overflowTargets.Add(p);
            }

            if (overflowTargets.Count > 0)
            {
                var names = new List<string>();
                foreach (var p in overflowTargets)
                    names.Add(p.Prefab != null ? p.Prefab.name : "(null)");

                EditorUtility.DisplayDialog(
                    "OiL 오버플로 — 적용 차단",
                    "다음 프리팹에서 OiL이 short 범위(-32768~32767)를 초과합니다:\n\n" +
                    string.Join("\n", names.ToArray()) +
                    "\n\nChar Sorting Order 값을 조정하거나 해당 프리팹의 체크를 해제한 뒤 다시 시도하세요.",
                    "확인");
                return false;
            }

            var globalPending = new Dictionary<Material, bool>();
            var conflictMats = new HashSet<Material>();
            foreach (var p in targets)
            {
                foreach (var kv in p.InstancingOverrides)
                {
                    if (kv.Key == null) continue;
                    if (globalPending.TryGetValue(kv.Key, out var existing))
                    {
                        if (existing != kv.Value) conflictMats.Add(kv.Key);
                    }
                    else
                    {
                        globalPending[kv.Key] = kv.Value;
                    }
                }
            }

            if (conflictMats.Count > 0)
            {
                var conflictNames = new List<string>();
                foreach (var m in conflictMats) conflictNames.Add(m.name);

                EditorUtility.DisplayDialog(
                    "GPU Instancing 충돌 — 적용 차단",
                    "다음 Material 에 대해 여러 프리팹이 ON/OFF 로 충돌합니다:\n\n" +
                    string.Join("\n", conflictNames.ToArray()) +
                    "\n\n충돌을 해결한 뒤 다시 시도하세요.",
                    "확인");
                return false;
            }

            int matChangeCount = 0;
            foreach (var kv in globalPending)
            {
                if (kv.Key == null) continue;
                if (kv.Key.enableInstancing != kv.Value) matChangeCount++;
            }

            int interleaveCount = 0;
            int instancingOffCount = 0;
            int siblingReorderCount = 0;
            int sumBefore = 0;
            int sumAfter = 0;
            foreach (var p in targets)
            {
                bool hasInterleaveInThis = false;
                var instancingMats = new HashSet<Material>();
                foreach (var r in p.Renderers)
                {
                    if (r.HasInterleaveWarning) hasInterleaveInThis = true;
                    if (r.IsInstancingDisabled && r.SharedMaterial != null)
                        instancingMats.Add(r.SharedMaterial);
                }
                if (hasInterleaveInThis) interleaveCount++;
                instancingOffCount += instancingMats.Count;
                if (p.HasSiblingReorder) siblingReorderCount++;
                sumBefore += p.BatchBefore;
                sumAfter += p.BatchAfter;
            }

            var message =
                $"선택된 프리팹 {targets.Count}개에 OiL/Fudge를 적용합니다.\n\n" +
                $"· 인터리브 경고 프리팹: {interleaveCount}개\n" +
                $"· GPU Instancing 꺼진 Material: {instancingOffCount}개\n" +
                (matChangeCount > 0
                    ? $"· GPU Instancing 변경 Material: {matChangeCount}개 (이 Material을 사용하는 다른 프리팹/씬에도 영향)\n"
                    : string.Empty) +
                $"· 형제 순서 변경 프리팹: {siblingReorderCount}개\n" +
                $"· 전체 배치 수: {sumBefore} → {sumAfter}\n" +
                "\n계속 진행하시겠습니까?";

            bool ok = EditorUtility.DisplayDialog(
                "Particle Auto Sorting — 적용 확인",
                message,
                "적용",
                "취소");
            if (!ok) return false;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroupName);
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                foreach (var p in targets)
                {
                    ApplySingle(p);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            foreach (var p in targets)
            {
                foreach (var r in p.Renderers)
                {
                    if (r.SharedMaterial != null)
                        r.IsInstancingDisabled = !r.SharedMaterial.enableInstancing;
                }
                p.InstancingOverrides.Clear();
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        private static void ApplySingle(PrefabData data)
        {
            if (data == null || data.Prefab == null) return;

            ApplySiblingOrder(data);

            var writtenRenderers = new HashSet<Renderer>();
            foreach (var r in data.Renderers)
            {
                if (r.Renderer == null) continue;
                if (r.Kind == RendererKind.Trail_Module) continue;
                if (!writtenRenderers.Add(r.Renderer)) continue;

                int oil = r.OilOverride ?? r.OilAfterAI;
                float fudge = r.FudgeOverride ?? r.FudgeAfterAI;

                Undo.RegisterCompleteObjectUndo(r.Renderer, UndoGroupName);
                WriteRenderer(r.Renderer, oil, fudge);
                EditorUtility.SetDirty(r.Renderer);
            }

            foreach (var kv in data.InstancingOverrides)
            {
                var mat = kv.Key;
                if (mat == null) continue;
                if (mat.enableInstancing == kv.Value) continue;

                Undo.RegisterCompleteObjectUndo(mat, UndoGroupName);
                mat.enableInstancing = kv.Value;
                EditorUtility.SetDirty(mat);
            }

            PrefabUtility.SavePrefabAsset(data.Prefab);

            data.HasSiblingReorder = false;
            data.NeedsPreviewRefresh = false;
        }

        private static void ApplySiblingOrder(PrefabData data)
        {
            if (data == null || !data.HasSiblingReorder) return;

            // 1. 부모별로 자식 transform 그룹핑 (RendererInfo 기준 distinct, 순서는 HierarchyOrder 정수부)
            var byParent = new Dictionary<Transform, List<Transform>>();
            var baseByTransform = new Dictionary<Transform, float>();
            foreach (var r in data.Renderers)
            {
                if (r.Renderer == null) continue;
                var t = r.Renderer.transform;
                var parent = t.parent;
                if (parent == null) continue;
                if (baseByTransform.ContainsKey(t)) continue;
                baseByTransform[t] = Mathf.Floor(r.HierarchyOrder);
                if (!byParent.TryGetValue(parent, out var list))
                {
                    list = new List<Transform>();
                    byParent[parent] = list;
                }
                list.Add(t);
            }

            foreach (var kv in byParent)
            {
                var parent = kv.Key;
                var children = kv.Value;
                children.Sort((a, b) => baseByTransform[a].CompareTo(baseByTransform[b]));

                // 2. 같은 부모 자식 중 Renderer 가 있는 자식의 현재 sibling slot 위치만 수집 (Renderer 없는 자식 절대 위치 보존)
                var rendererChildSet = new HashSet<Transform>(children);
                var slots = new List<int>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (rendererChildSet.Contains(c))
                        slots.Add(i);
                }

                if (slots.Count != children.Count) continue;

                // 3. slot 위치에 새 순서대로 채워 넣기
                for (int i = 0; i < children.Count; i++)
                {
                    var target = children[i];
                    int targetSlot = slots[i];
                    if (target.GetSiblingIndex() == targetSlot) continue;
                    Undo.RegisterCompleteObjectUndo(parent, UndoGroupName);
                    target.SetSiblingIndex(targetSlot);
                    EditorUtility.SetDirty(parent);
                }
            }
        }

        private static void WriteRenderer(Renderer renderer, int oil, float fudge)
        {
            if (renderer is ParticleSystemRenderer psr)
            {
                psr.sortingOrder = oil;
                psr.sortingFudge = fudge;
                return;
            }

            if (renderer is TrailRenderer tr)
            {
                tr.sortingOrder = oil;
                var so = new SerializedObject(tr);
                var prop = so.FindProperty("m_SortingFudge");
                if (prop != null)
                {
                    prop.floatValue = fudge;
                    so.ApplyModifiedProperties();
                }
                return;
            }

            renderer.sortingOrder = oil;
        }
    }
}
