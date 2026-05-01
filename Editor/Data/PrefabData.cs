using System.Collections.Generic;
using UnityEngine;

namespace ParticleAutoSorting.Editor.Data
{
    public class PrefabData
    {
        public GameObject Prefab;
        public List<RendererInfo> Renderers = new List<RendererInfo>();
        public List<string> Warnings = new List<string>();
        public int BatchBefore;
        public int BatchAfter;
        public bool IsExpanded;
        public bool HasAboveBelow;
        public bool HasOverflow;
        public bool IsSelectedForApply = true;

        public bool HasSiblingReorder;
        public bool NeedsPreviewRefresh;

        public Dictionary<Material, bool> InstancingOverrides = new Dictionary<Material, bool>();
    }
}
