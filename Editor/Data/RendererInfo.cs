using System.Collections.Generic;
using UnityEngine;

namespace ParticleAutoSorting.Editor.Data
{
    public enum RendererKind
    {
        Particle,
        Trail_Component,
        Trail_Module
    }

    public class RendererInfo
    {
        public string ObjectName;
        public Renderer Renderer;
        public Material SharedMaterial;
        public ParticleSystemRenderMode? RenderMode;
        public Mesh Mesh;
        public int SortingLayerID;
        public float HierarchyOrder;
        public string GroupTag;
        public RendererKind Kind;

        public int OilBefore;
        public float FudgeBefore;
        public int OilAfterAI;
        public float FudgeAfterAI;
        public int? OilOverride;
        public float? FudgeOverride;

        public bool HasInterleaveWarning;
        public List<string> InterleavedWith = new List<string>();
        public bool IsInstancingDisabled;
    }
}
