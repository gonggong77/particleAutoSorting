using System.Collections.Generic;
using ParticleAutoSorting.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace ParticleAutoSorting.Editor.Apply
{
    /// <summary>
    /// 기존 PSR 의 sortingOrder/sortingFudge 값은 그대로 두고,
    /// 모든 부모 노드의 자식 sibling 순서만 (OiL DESC, Fudge ASC) 로 재정렬한다.
    /// PSR 이 없는 자식의 슬롯 인덱스는 보존한다.
    /// </summary>
    public static class HierarchyReorderer
    {
        private const string UndoGroupName = "Particle Auto Sorting Hierarchy Reorder";

        public static bool Apply(IEnumerable<PrefabData> prefabs)
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
                    "재정렬할 프리팹이 선택되지 않았습니다.",
                    "확인");
                return false;
            }

            bool ok = EditorUtility.DisplayDialog(
                "하이어라키만 재정렬",
                $"선택된 프리팹 {targets.Count}개에 대해 기존 OiL/Fudge 값은 그대로 두고\n" +
                "Above/Below 내부의 sibling 순서만 (OiL DESC, Fudge ASC) 로 재배치합니다.\n\n" +
                "계속 진행하시겠습니까?",
                "재정렬",
                "취소");
            if (!ok) return false;

            int reorderedCount = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var p in targets)
                {
                    if (ApplyOne(p)) reorderedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static bool ApplyOne(PrefabData data)
        {
            var prefabAsset = data.Prefab;
            if (prefabAsset == null) return false;

            var path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) return false;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return false;

            try
            {
                var above = FindDescendant(root.transform, "Above");
                var below = FindDescendant(root.transform, "Below");

                if (above == null && below == null)
                {
                    // Above/Below 누락 prefab 은 root 전체 트리를 대상으로 한다.
                    ReorderSubtreeByExistingPSR(root.transform);
                }
                else
                {
                    if (above != null) ReorderSubtreeByExistingPSR(above);
                    if (below != null) ReorderSubtreeByExistingPSR(below);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 깊이 우선으로 모든 부모 노드를 방문하여 ReorderChildrenAtParent 적용.
        /// </summary>
        private static void ReorderSubtreeByExistingPSR(Transform root)
        {
            if (root == null) return;
            ReorderChildrenAtParent(root);
            // SetSiblingIndex 후에도 전체 자식 집합은 변하지 않으므로 i 인덱스로 안전하게 순회.
            for (int i = 0; i < root.childCount; i++)
            {
                ReorderSubtreeByExistingPSR(root.GetChild(i));
            }
        }

        /// <summary>
        /// 한 부모 노드의 직속 자식 중 PSR/TrailRenderer 를 가진 자식만 (OiL DESC, Fudge ASC) 로 재정렬.
        /// PSR-자식의 원래 슬롯 인덱스 리스트를 보존하며, 비-PSR 자식의 절대 위치는 건드리지 않는다.
        /// </summary>
        private static void ReorderChildrenAtParent(Transform parent)
        {
            if (parent == null || parent.childCount < 2) return;

            var psrChildren = new List<Transform>();
            var slots = new List<int>();

            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (HasSortingRenderer(c))
                {
                    psrChildren.Add(c);
                    slots.Add(i);
                }
            }

            if (psrChildren.Count < 2) return;

            // 정렬 키: top → bottom 으로 (OiL ASC, Fudge DESC).
            // 트리 하단 = 화면 앞. OiL 큰 쪽이 하단(나중에 그려짐), Fudge 작은 쪽이 하단(앞에 그려짐).
            psrChildren.Sort((a, b) =>
            {
                int oilA = GetSortingOrder(a);
                int oilB = GetSortingOrder(b);
                if (oilA != oilB) return oilA.CompareTo(oilB); // ASC
                float fA = GetSortingFudge(a);
                float fB = GetSortingFudge(b);
                return fB.CompareTo(fA); // DESC
            });

            for (int i = 0; i < psrChildren.Count; i++)
            {
                var target = psrChildren[i];
                int targetSlot = slots[i];
                if (target.GetSiblingIndex() != targetSlot)
                {
                    target.SetSiblingIndex(targetSlot);
                }
            }
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var nested = FindDescendant(c, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static bool HasSortingRenderer(Transform t)
        {
            var go = t.gameObject;
            if (go.GetComponent<ParticleSystemRenderer>() != null) return true;
            if (go.GetComponent<TrailRenderer>() != null) return true;
            return false;
        }

        private static int GetSortingOrder(Transform t)
        {
            var psr = t.GetComponent<ParticleSystemRenderer>();
            if (psr != null) return psr.sortingOrder;
            var tr = t.GetComponent<TrailRenderer>();
            if (tr != null) return tr.sortingOrder;
            return 0;
        }

        private static float GetSortingFudge(Transform t)
        {
            var psr = t.GetComponent<ParticleSystemRenderer>();
            if (psr != null) return psr.sortingFudge;
            var tr = t.GetComponent<TrailRenderer>();
            if (tr != null)
            {
                var so = new SerializedObject(tr);
                var prop = so.FindProperty("m_SortingFudge");
                return prop != null ? prop.floatValue : 0f;
            }
            return 0f;
        }
    }
}
