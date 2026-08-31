using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class SkillIcon : MaskableGraphic
{
    public PlayerSkill Skill { get; set; }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (Skill)
        {
            case PlayerSkill.PowerRounds:
            case PlayerSkill.PiercingRounds:
                Bullet(mesh, -0.24f);
                Bullet(mesh, 0.06f);
                if (Skill == PlayerSkill.PiercingRounds)
                {
                    Quad(mesh, -0.45f, -0.08f, 0.45f, 0.02f);
                    Triangle(mesh, new Vector2(0.45f, -0.03f), new Vector2(0.26f, 0.14f), new Vector2(0.26f, -0.2f));
                }
                break;
            case PlayerSkill.HeavyStrike:
            case PlayerSkill.WideSwing:
                Quad(mesh, -0.055f, -0.43f, 0.055f, -0.1f);
                Quad(mesh, -0.2f, -0.14f, 0.2f, -0.07f);
                Quad(mesh, -0.09f, -0.06f, 0.09f, 0.32f);
                Triangle(mesh, new Vector2(-0.09f, 0.32f), new Vector2(0f, 0.47f), new Vector2(0.09f, 0.32f));
                if (Skill == PlayerSkill.WideSwing)
                    for (int i = 0; i < 18; i++)
                    {
                        float a = Mathf.Lerp(15f, 165f, i / 18f) * Mathf.Deg2Rad;
                        float b = Mathf.Lerp(15f, 165f, (i + 1f) / 18f) * Mathf.Deg2Rad;
                        Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                        Triangle(mesh, p * 0.45f, q * 0.45f, p * 0.4f);
                        Triangle(mesh, q * 0.45f, q * 0.4f, p * 0.4f);
                    }
                break;
            case PlayerSkill.AmmoRecovery:
            case PlayerSkill.Supply:
                Quad(mesh, -0.36f, -0.35f, 0.36f, -0.27f);
                Quad(mesh, -0.36f, -0.27f, -0.28f, 0.12f);
                Quad(mesh, 0.28f, -0.27f, 0.36f, 0.12f);
                Quad(mesh, -0.05f, -0.1f, 0.05f, 0.42f);
                Triangle(mesh, new Vector2(0f, -0.17f), new Vector2(-0.18f, 0.03f), new Vector2(0.18f, 0.03f));
                break;
            default:
                Quad(mesh, -0.12f, -0.4f, 0.12f, 0.4f);
                Quad(mesh, -0.4f, -0.12f, 0.4f, 0.12f);
                break;
        }
    }

    private Vector3 Point(Vector2 p) => rectTransform.rect.center + Vector2.Scale(p, rectTransform.rect.size);
    private void Bullet(VertexHelper mesh, float x)
    {
        Quad(mesh, x, -0.34f, x + 0.18f, 0.25f);
        Quad(mesh, x - 0.025f, -0.42f, x + 0.205f, -0.35f);
        Triangle(mesh, new Vector2(x, 0.25f), new Vector2(x + 0.09f, 0.43f), new Vector2(x + 0.18f, 0.25f));
    }
    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(Point(a), color, Vector2.zero);
        mesh.AddVert(Point(b), color, Vector2.zero);
        mesh.AddVert(Point(c), color, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2);
    }

    private void Quad(VertexHelper mesh, float x, float y, float right, float top)
    {
        Triangle(mesh, new Vector2(x, y), new Vector2(x, top), new Vector2(right, top));
        Triangle(mesh, new Vector2(x, y), new Vector2(right, top), new Vector2(right, y));
    }
}
