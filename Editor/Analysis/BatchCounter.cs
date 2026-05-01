using System;
using System.Collections.Generic;
using UnityEngine;
using ParticleAutoSorting.Editor.Data;

namespace ParticleAutoSorting.Editor.Analysis
{
    public static class BatchCounter
    {
        public static void CountPrefab(PrefabData data)
        {
            if (data == null) return;
            data.BatchBefore = CountAcrossGroups(data.Renderers, useBeforeValues: true);
            data.BatchAfter = CountAcrossGroups(data.Renderers, useBeforeValues: false);
        }

        public static (int before, int after) CountTotal(IEnumerable<PrefabData> prefabs)
        {
            int before = 0;
            int after = 0;
            if (prefabs == null) return (0, 0);
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                before += p.BatchBefore;
                after += p.BatchAfter;
            }
            return (before, after);
        }

        public static (int[] beforeCum, int[] afterCum) CumulativeBatchesPerRenderer(PrefabData data)
        {
            if (data == null || data.Renderers == null || data.Renderers.Count == 0)
                return (new int[0], new int[0]);

            int[] beforeIdx = AssignBatchIndices(data.Renderers, useAfter: false);
            int[] afterIdx = AssignBatchIndices(data.Renderers, useAfter: true);
            return (PrefixDistinctCount(beforeIdx), PrefixDistinctCount(afterIdx));
        }

        private static int[] AssignBatchIndices(List<RendererInfo> renderers, bool useAfter)
        {
            int n = renderers.Count;
            var result = new int[n];

            // Above/Below 는 개념적으로 별도 렌더 패스이므로 반드시 그룹 단위로
            // 배치 인덱스 공간을 분리해야 한다. 전역 정렬로 합치면 Above/Below
            // 사이에 동일 (Layer, OiL, Fudge, Material) 렌더러가 우연히 인접해
            // 잘못 merge 되어 프리팹 총 배치 수 자체가 틀어진다.
            var groupNames = new List<string>();
            var groupToIndices = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                var tag = renderers[i].GroupTag ?? "Root";
                if (!groupToIndices.TryGetValue(tag, out var bucket))
                {
                    bucket = new List<int>();
                    groupToIndices[tag] = bucket;
                    groupNames.Add(tag);
                }
                bucket.Add(i);
            }

            int offset = 0;
            foreach (var tag in groupNames)
            {
                var bucket = groupToIndices[tag];
                var order = bucket.ToArray();
                System.Array.Sort(order, (a, b) => CompareSortKey(renderers[a], renderers[b], useBefore: !useAfter));

                int batchIndex = 0;
                bool hasPrev = false;
                BatchKey prev = default;

                for (int s = 0; s < order.Length; s++)
                {
                    var r = renderers[order[s]];
                    var key = BuildBatchKey(r);
                    if (!hasPrev || !key.Equals(prev))
                    {
                        batchIndex++;
                        prev = key;
                        hasPrev = true;
                    }
                    result[order[s]] = offset + batchIndex;
                }
                offset += batchIndex;
            }

            return result;
        }

        private static int[] PrefixDistinctCount(int[] batchIndices)
        {
            int n = batchIndices.Length;
            var cum = new int[n];
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                seen.Add(batchIndices[i]);
                cum[i] = seen.Count;
            }
            return cum;
        }

        private static int CountAcrossGroups(List<RendererInfo> renderers, bool useBeforeValues)
        {
            if (renderers == null || renderers.Count == 0) return 0;

            var buckets = new Dictionary<string, List<RendererInfo>>();
            foreach (var r in renderers)
            {
                var tag = r.GroupTag ?? "Root";
                if (!buckets.TryGetValue(tag, out var list))
                {
                    list = new List<RendererInfo>();
                    buckets[tag] = list;
                }
                list.Add(r);
            }

            int total = 0;
            foreach (var kv in buckets)
                total += CountSingleGroup(kv.Value, useBeforeValues);
            return total;
        }

        private static int CountSingleGroup(List<RendererInfo> list, bool useBeforeValues)
        {
            if (list == null || list.Count == 0) return 0;

            var sorted = new List<RendererInfo>(list);
            sorted.Sort((a, b) => CompareSortKey(a, b, useBeforeValues));

            int batchCount = 0;
            bool hasPrev = false;
            BatchKey prev = default;

            foreach (var r in sorted)
            {
                var key = BuildBatchKey(r);
                if (!hasPrev || !key.Equals(prev))
                {
                    batchCount++;
                    prev = key;
                    hasPrev = true;
                }
            }
            return batchCount;
        }

        private static int CompareSortKey(RendererInfo a, RendererInfo b, bool useBefore)
        {
            int cmp = a.SortingLayerID.CompareTo(b.SortingLayerID);
            if (cmp != 0) return cmp;

            int aOil = ResolveOil(a, useBefore);
            int bOil = ResolveOil(b, useBefore);
            cmp = aOil.CompareTo(bOil);
            if (cmp != 0) return cmp;

            float aFudge = ResolveFudge(a, useBefore);
            float bFudge = ResolveFudge(b, useBefore);
            cmp = aFudge.CompareTo(bFudge);
            if (cmp != 0) return cmp;

            return a.HierarchyOrder.CompareTo(b.HierarchyOrder);
        }

        private static int ResolveOil(RendererInfo r, bool useBefore)
        {
            if (useBefore) return r.OilBefore;
            return r.OilOverride ?? r.OilAfterAI;
        }

        private static float ResolveFudge(RendererInfo r, bool useBefore)
        {
            if (useBefore) return r.FudgeBefore;
            return r.FudgeOverride ?? r.FudgeAfterAI;
        }

        private static BatchKey BuildBatchKey(RendererInfo r) => new BatchKey(
            r.SortingLayerID,
            r.SharedMaterial,
            r.RenderMode,
            r.Mesh);

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly int SortingLayerID;
            public readonly Material Material;
            public readonly ParticleSystemRenderMode? RenderMode;
            public readonly Mesh Mesh;

            public BatchKey(int layer, Material mat, ParticleSystemRenderMode? mode, Mesh mesh)
            {
                SortingLayerID = layer;
                Material = mat;
                RenderMode = mode;
                Mesh = mesh;
            }

            public bool Equals(BatchKey other)
            {
                return SortingLayerID == other.SortingLayerID
                    && ReferenceEquals(Material, other.Material)
                    && Nullable.Equals(RenderMode, other.RenderMode)
                    && ReferenceEquals(Mesh, other.Mesh);
            }

            public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = SortingLayerID;
                    h = h * 397 ^ (Material != null ? Material.GetInstanceID() : 0);
                    h = h * 397 ^ (RenderMode.HasValue ? (int)RenderMode.Value : -1);
                    h = h * 397 ^ (Mesh != null ? Mesh.GetInstanceID() : 0);
                    return h;
                }
            }
        }
    }
}
