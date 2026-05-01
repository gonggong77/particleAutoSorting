using System.Collections.Generic;
using UnityEngine;

namespace ParticleAutoSorting.Editor.Data
{
    public class MaterialGroupInfo
    {
        public Material SharedMaterial;
        public List<RendererInfo> Members = new List<RendererInfo>();
        public float RepresentativeOrder;
        public int AssignedOrderInLayer;
        public Color GroupColor;
    }
}
