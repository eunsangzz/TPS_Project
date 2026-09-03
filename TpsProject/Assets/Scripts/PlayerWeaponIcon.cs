using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class PlayerWeaponIcon : MaskableGraphic
{
    [SerializeField] private int slot;
    public int Slot
    {
        get => slot;
        set { slot = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (Slot == 1)
        {
            Quad(mesh, -0.06f, -0.42f, 0.06f, 0.43f);
            Quad(mesh, -0.15f, -0.18f, 0.15f, -0.1f);
            Quad(mesh, -0.09f, -0.05f, 0.09f, 0.43f);
        }
        else if (Slot == 2 || Slot == 3)
        {
            Quad(mesh, -0.25f, 0f, 0.27f, 0.17f);
            Quad(mesh, 0.22f, 0.05f, 0.48f, 0.12f);
            Quad(mesh, -0.46f, -0.08f, -0.23f, 0.13f);
            Quad(mesh, -0.16f, -0.3f, -0.08f, 0f);
            Quad(mesh, 0.01f, -0.29f, 0.14f, 0f);
            Quad(mesh, -0.1f, 0.17f, 0.08f, 0.25f);
        }
        else if (Slot == 4)
        {
            Quad(mesh, -0.46f, -0.12f, -0.22f, 0.08f);
            Quad(mesh, -0.25f, -0.02f, 0.18f, 0.1f);
            Quad(mesh, 0.16f, 0.025f, 0.49f, 0.075f);
            Quad(mesh, -0.15f, -0.26f, -0.07f, 0f);
            Quad(mesh, -0.14f, 0.17f, 0.17f, 0.29f);
            Quad(mesh, -0.05f, 0.1f, 0.03f, 0.18f);
        }
        else Quad(mesh, -0.13f, -0.025f, 0.13f, 0.025f);
    }

    private void Quad(VertexHelper mesh, float left, float bottom, float right, float top)
    {
        Rect rect = rectTransform.rect;
        int index = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left * rect.width, bottom * rect.height), color, Vector2.zero);
        mesh.AddVert(new Vector3(left * rect.width, top * rect.height), color, Vector2.zero);
        mesh.AddVert(new Vector3(right * rect.width, top * rect.height), color, Vector2.zero);
        mesh.AddVert(new Vector3(right * rect.width, bottom * rect.height), color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
