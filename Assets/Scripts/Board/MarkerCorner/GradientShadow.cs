using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class GradientShadow : BaseMeshEffect
{
    public Color topColor = Color.black;
    public Color bottomColor = Color.clear;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        UIVertex vertex = new UIVertex();
        int count = vh.currentVertCount;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float ratio = vertex.position.y / vh.currentVertCount;
            vertex.color *= Color.Lerp(bottomColor, topColor, ratio);
            vh.SetUIVertex(vertex, i);
        }
    }
}
