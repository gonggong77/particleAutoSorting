using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ParticleAutoSorting.Editor.Data;

namespace ParticleAutoSorting.Editor.Analysis
{
    public static class PrefabAnalyzer
    {
        public static PrefabData Analyze(GameObject prefab)
        {
            var data = new PrefabData { Prefab = prefab };
            if (prefab == null)
            {
                data.Warnings.Add("프리팹이 null입니다.");
                return data;
            }

            var above = FindDescendant(prefab.transform, "Above");
            var below = FindDescendant(prefab.transform, "Below");

            if (above == null && below == null)
            {
                data.HasAboveBelow = false;
                data.Warnings.Add("Above/Below 구조 누락");
                CollectFromRoot(prefab.transform, data, "Root");
            }
            else
            {
                data.HasAboveBelow = true;
                if (above == null)
                    data.Warnings.Add("Above 루트 누락");
                else
                    CollectFromRoot(above, data, "Above");

                if (below == null)
                    data.Warnings.Add("Below 루트 누락");
                else
                    CollectFromRoot(below, data, "Below");
            }

            return data;
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

        private static void CollectFromRoot(Transform root, PrefabData data, string groupTag)
        {
            var traversal = new List<Transform>();
            CollectDepthFirst(root, traversal);

            int orderCounter = 0;
            bool anySubEmitterFailure = false;

            foreach (var t in traversal)
            {
                var go = t.gameObject;
                bool added = false;

                var psr = go.GetComponent<ParticleSystemRenderer>();
                if (psr != null && IsActiveInPrefab(go) && psr.enabled)
                {
                    var mat = psr.sharedMaterial;
                    if (mat != null)
                    {
                        data.Renderers.Add(new RendererInfo
                        {
                            ObjectName = go.name,
                            Renderer = psr,
                            SharedMaterial = mat,
                            RenderMode = psr.renderMode,
                            Mesh = psr.renderMode == ParticleSystemRenderMode.Mesh ? psr.mesh : null,
                            SortingLayerID = psr.sortingLayerID,
                            HierarchyOrder = orderCounter,
                            GroupTag = groupTag,
                            Kind = RendererKind.Particle,
                            OilBefore = psr.sortingOrder,
                            FudgeBefore = psr.sortingFudge,
                            OilAfterAI = psr.sortingOrder,
                            FudgeAfterAI = psr.sortingFudge,
                            IsInstancingDisabled = !mat.enableInstancing
                        });
                        added = true;
                    }

                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var trailsModule = ps.trails;
                        if (trailsModule.enabled)
                        {
                            var trailMat = psr.trailMaterial;
                            if (trailMat != null)
                            {
                                data.Renderers.Add(new RendererInfo
                                {
                                    ObjectName = go.name + " (Trail)",
                                    Renderer = psr,
                                    SharedMaterial = trailMat,
                                    RenderMode = null,
                                    Mesh = null,
                                    SortingLayerID = psr.sortingLayerID,
                                    HierarchyOrder = orderCounter + 0.5f,
                                    GroupTag = groupTag,
                                    Kind = RendererKind.Trail_Module,
                                    OilBefore = psr.sortingOrder,
                                    FudgeBefore = psr.sortingFudge,
                                    OilAfterAI = psr.sortingOrder,
                                    FudgeAfterAI = psr.sortingFudge,
                                    IsInstancingDisabled = !trailMat.enableInstancing
                                });
                                added = true;
                            }
                        }

                        if (CheckSubEmitterResolution(ps) == false)
                            anySubEmitterFailure = true;
                    }
                }

                var tr = go.GetComponent<TrailRenderer>();
                if (tr != null && IsActiveInPrefab(go) && tr.enabled)
                {
                    var mat = tr.sharedMaterial;
                    if (mat != null)
                    {
                        float hOrder = added ? orderCounter + 0.5f : orderCounter;
                        data.Renderers.Add(new RendererInfo
                        {
                            ObjectName = go.name,
                            Renderer = tr,
                            SharedMaterial = mat,
                            RenderMode = null,
                            Mesh = null,
                            SortingLayerID = tr.sortingLayerID,
                            HierarchyOrder = hOrder,
                            GroupTag = groupTag,
                            Kind = RendererKind.Trail_Component,
                            OilBefore = tr.sortingOrder,
                            FudgeBefore = GetSortingFudge(tr),
                            OilAfterAI = tr.sortingOrder,
                            FudgeAfterAI = GetSortingFudge(tr),
                            IsInstancingDisabled = !mat.enableInstancing
                        });
                        added = true;
                    }
                }

                if (added)
                    orderCounter++;
            }

            if (anySubEmitterFailure)
                data.Warnings.Add("Sub Emitter 참조 누락");
        }

        private static void CollectDepthFirst(Transform t, List<Transform> result)
        {
            result.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectDepthFirst(t.GetChild(i), result);
        }

        private static bool CheckSubEmitterResolution(ParticleSystem ps)
        {
            var subs = ps.subEmitters;
            if (!subs.enabled) return true;
            for (int i = 0; i < subs.subEmittersCount; i++)
            {
                var sub = subs.GetSubEmitterSystem(i);
                if (sub == null) return false;
            }
            return true;
        }

        private static bool IsActiveInPrefab(GameObject go)
        {
            // Prefab asset 은 씬에 없으므로 activeInHierarchy 가 항상 false.
            // 부모 체인을 따라 activeSelf 만으로 판정한다.
            var t = go.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf) return false;
                t = t.parent;
            }
            return true;
        }

        private static float GetSortingFudge(Renderer r)
        {
            if (r is ParticleSystemRenderer psr) return psr.sortingFudge;
            var so = new SerializedObject(r);
            var prop = so.FindProperty("m_SortingFudge");
            return prop != null ? prop.floatValue : 0f;
        }
    }
}
