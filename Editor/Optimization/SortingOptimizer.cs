using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ParticleAutoSorting.Editor.Data;

namespace ParticleAutoSorting.Editor.Optimization
{
    public static class SortingOptimizer
    {
        private const int ShortMin = -32768;
        private const int ShortMax = 32767;
        public const float DefaultFudgeStep = 30f;

        public static void OptimizeAll(IEnumerable<PrefabData> prefabs, int charSortingOrder, float fudgeStep = DefaultFudgeStep)
        {
            if (prefabs == null) return;
            foreach (var p in prefabs)
                Optimize(p, charSortingOrder, fudgeStep);
        }

        public static List<MaterialGroupInfo> Optimize(PrefabData data, int charSortingOrder, float fudgeStep = DefaultFudgeStep)
        {
            var all = new List<MaterialGroupInfo>();
            if (data == null) return all;

            data.HasOverflow = false;
            foreach (var r in data.Renderers)
            {
                r.HasInterleaveWarning = false;
                if (r.InterleavedWith == null)
                    r.InterleavedWith = new List<string>();
                else
                    r.InterleavedWith.Clear();
            }

            var buckets = new Dictionary<string, List<RendererInfo>>();
            foreach (var r in data.Renderers)
            {
                var tag = string.IsNullOrEmpty(r.GroupTag) ? "Root" : r.GroupTag;
                if (!buckets.TryGetValue(tag, out var list))
                {
                    list = new List<RendererInfo>();
                    buckets[tag] = list;
                }
                list.Add(r);
            }

            foreach (var kv in buckets)
            {
                var tag = kv.Key;
                // Hierarchy 순서로 정렬된 연속 동일-Material 런 단위로 그룹을 만든다.
                // 같은 Material 이 Hierarchy 상에서 인접하지 않으면 별도 런 → 별도 OIL.
                // 이로써 OrderInLayer/Fudge 오브젝트 단위 단조성 불변식을 강제한다.
                var runs = BuildMaterialRuns(kv.Value);

                bool isBelow = tag == "Below";
                int n = runs.Count;
                for (int i = 0; i < n; i++)
                {
                    var g = runs[i];
                    int oil = isBelow
                        ? charSortingOrder - (n - i)
                        : charSortingOrder + (i + 1);

                    g.AssignedOrderInLayer = oil;
                    g.GroupColor = PickGroupColor(g.SharedMaterial);

                    if (oil < ShortMin || oil > ShortMax)
                        data.HasOverflow = true;

                    AssignFudgeAndWriteAI(g, oil, fudgeStep);
                }

                all.AddRange(runs);
            }

            DetectInterleave(data.Renderers);
            return all;
        }

        private static List<MaterialGroupInfo> BuildMaterialRuns(List<RendererInfo> list)
        {
            // Hierarchy 순서(위 → 아래)로 정렬 후, 연속된 동일-Material 구간을 하나의 런으로 묶는다.
            // 같은 Material 이라도 중간에 다른 Material 이 끼어들면 별도 런이 된다.
            // 이는 인접 오브젝트의 OrderInLayer 단조성(같거나 증가)을 보장하기 위함.
            var sorted = new List<RendererInfo>(list);
            sorted.Sort((a, b) => a.HierarchyOrder.CompareTo(b.HierarchyOrder));

            var runs = new List<MaterialGroupInfo>();
            MaterialGroupInfo current = null;
            foreach (var r in sorted)
            {
                if (r.SharedMaterial == null) continue;
                if (current == null || !ReferenceEquals(current.SharedMaterial, r.SharedMaterial))
                {
                    current = new MaterialGroupInfo
                    {
                        SharedMaterial = r.SharedMaterial,
                        RepresentativeOrder = r.HierarchyOrder
                    };
                    runs.Add(current);
                }
                current.Members.Add(r);
            }
            return runs;
        }

        private static void AssignFudgeAndWriteAI(MaterialGroupInfo g, int oil, float fudgeStep)
        {
            int size = g.Members.Count;
            g.Members.Sort((a, b) => a.HierarchyOrder.CompareTo(b.HierarchyOrder));

            float step = size <= 1 ? 0f : fudgeStep;
            for (int rank = 0; rank < size; rank++)
            {
                var m = g.Members[rank];
                m.OilAfterAI = oil;
                m.FudgeAfterAI = (size - 1 - rank) * step;
            }
        }

        private static void DetectInterleave(List<RendererInfo> renderers)
        {
            if (renderers == null || renderers.Count < 2) return;

            var sorted = new List<RendererInfo>(renderers);
            sorted.Sort(CompareAfterSortKey);

            var positions = new Dictionary<Material, List<int>>();
            for (int i = 0; i < sorted.Count; i++)
            {
                var r = sorted[i];
                if (r.SharedMaterial == null) continue;
                if (!positions.TryGetValue(r.SharedMaterial, out var ps))
                {
                    ps = new List<int>();
                    positions[r.SharedMaterial] = ps;
                }
                ps.Add(i);
            }

            var materials = positions.Keys.ToList();
            for (int a = 0; a < materials.Count; a++)
            {
                for (int b = a + 1; b < materials.Count; b++)
                {
                    var pa = positions[materials[a]];
                    var pb = positions[materials[b]];
                    int maxA = pa[pa.Count - 1];
                    int minA = pa[0];
                    int maxB = pb[pb.Count - 1];
                    int minB = pb[0];

                    if (maxA > minB && maxB > minA)
                    {
                        string nameA = materials[a] != null ? materials[a].name : "<null>";
                        string nameB = materials[b] != null ? materials[b].name : "<null>";
                        MarkInterleave(renderers, materials[a], nameB);
                        MarkInterleave(renderers, materials[b], nameA);
                    }
                }
            }
        }

        private static void MarkInterleave(List<RendererInfo> renderers, Material target, string otherName)
        {
            foreach (var r in renderers)
            {
                if (!ReferenceEquals(r.SharedMaterial, target)) continue;
                r.HasInterleaveWarning = true;
                if (!r.InterleavedWith.Contains(otherName))
                    r.InterleavedWith.Add(otherName);
            }
        }

        private static int CompareAfterSortKey(RendererInfo a, RendererInfo b)
        {
            int cmp = a.SortingLayerID.CompareTo(b.SortingLayerID);
            if (cmp != 0) return cmp;

            int aOil = a.OilOverride ?? a.OilAfterAI;
            int bOil = b.OilOverride ?? b.OilAfterAI;
            cmp = aOil.CompareTo(bOil);
            if (cmp != 0) return cmp;

            float aF = a.FudgeOverride ?? a.FudgeAfterAI;
            float bF = b.FudgeOverride ?? b.FudgeAfterAI;
            cmp = aF.CompareTo(bF);
            if (cmp != 0) return cmp;

            return a.HierarchyOrder.CompareTo(b.HierarchyOrder);
        }

        private static Color PickGroupColor(Material mat)
        {
            if (mat == null) return new Color(0.6f, 0.6f, 0.6f);
            int id = mat.GetInstanceID();
            float hue = (Mathf.Abs(id) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.9f);
        }
    }
}
