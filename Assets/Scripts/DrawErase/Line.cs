// ===============================================================================
// Line.cs
// Operations on a single line segment
// ===============================================================================
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using Unity.VisualScripting;
using UnityEngine;

public class Line : MonoBehaviour
{
    [Header("Rendering / Collision")]
    [SerializeField] public LineRenderer _renderer;
    [SerializeField] private PolygonCollider2D _collider;

    [Header("References")]
    public ChalkManager _chalkManager;
    public GameObject dynamicLineParent;

    [Header("State")]
    public bool touchedLava = false;
    public bool inLava = false;
    public bool bluePrevCheck = true;

    private bool isErased = false;
    private float heatLevel = 0f;

    // Local-space points used to build the line
    private readonly List<Vector2> _points = new();

    private void Update()
    {
        if(inLava)
        {
            HeatUp();
        }
    }

    // Adds a new point to the line in world space.
    // Returns false if the point is too close to the previous one.
    public bool SetPosition(Vector2 worldPos)
    {
        Vector2 localPos = WorldToLocal(worldPos);

        if (!CanAppend(localPos))
            return false;

        _points.Add(localPos);

        _renderer.positionCount++;
        _renderer.SetPosition(_renderer.positionCount - 1, localPos);

        // Not enough points yet to form geometry
        if (_renderer.positionCount <= 1)
            return false;

        UpdateColliderAndNavMesh();
        return true;
    }

    public bool CanAppend(Vector2 localPos)
    {
        if (_renderer.positionCount == 0)
            return true;

        Vector2 last = _renderer.GetPosition(_renderer.positionCount - 1);
        return Vector2.Distance(last, localPos) > DrawManager.RESOLUTION;
    }

    public bool CanAppendWorldSpace(Vector2 worldPos)
    {
        if (_renderer.positionCount == 0)
            return true;

        return CanAppend(WorldToLocal(worldPos));
    }

    private Vector2 WorldToLocal(Vector2 worldPos)
    {
        return worldPos - (Vector2)transform.position;
    }

    // Rebuilds the polygon collider from the LineRenderer mesh and updates the A* navigation graph.
    private void UpdateColliderAndNavMesh()
    {
        Mesh mesh = new();
        _renderer.BakeMesh(mesh, true);

        List<Vector2> verts = new();

        foreach (Vector3 v in mesh.vertices)
        {
            Vector2 v2 = v;
            if (!verts.Contains(v2))
                verts.Add(v2);
        }

        verts = ConvexHull.compute(verts);
        _collider.points = verts.ToArray();

        if (AstarPath.active != null)
        {
            var guo = new GraphUpdateObject(_collider.bounds)
            {
                updatePhysics = true
            };
            AstarPath.active.UpdateGraphs(guo);
        }
    }


    public void destroy()
    {
        Destroy(gameObject);

        if (CompareTag("BlueLine"))
        {
            Transform parent = transform.parent;
            if (parent != null && parent.childCount == 1)
            {
                Destroy(parent.gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isErased)
            return;

        // Eraser or despawn
        if (collision.CompareTag("Eraser") || collision.CompareTag("BallDespawn"))
        {
            HandleErase();
            return;
        }

        // Lava contact
        if (collision.CompareTag("Lava") && !touchedLava)
        {
            inLava = true;
            HeatUp();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Lava"))
        {
            touchedLava = false;
            inLava = false;
        }
    }

    private void HandleErase()
    {
        if (CompareTag("BlueLine"))
        {
            SplitFromParent(applyVelocity: true);
        }

        _chalkManager.ReplenishChalk(0.1f);
        transform.parent = null;
        Destroy(gameObject);
        isErased = true;
    }

    // Adds "heat" to the line until it can change color
    private void HeatUp()
    {
        if (heatLevel < 0.5f)
        {
            IncreaseHeat();
            return;
        }

        DrawManager drawManager = _chalkManager.GetComponent<DrawManager>();

        // White lines convert immediately
        if (CompareTag("White"))
        {
            drawManager.createLine(1, this);
            _chalkManager.ReplenishChalk(0.1f);
            Destroy(gameObject);
            return;
        }

        // Blue lines melt together
        if (CompareTag("BlueLine"))
        {
            Transform parent = transform.parent;

            if (parent != null && parent.TryGetComponent(out Rigidbody2D rb) &&
                rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // Melt entire dynamic chain
                List<Transform> children = new();
                foreach (Transform child in parent)
                    children.Add(child);

                foreach (Transform child in children)
                {
                    drawManager.createLine(2, child.GetComponent<Line>());
                    child.GetComponent<Line>().EraseDynamic();
                }
            }
            else
            {
                drawManager.createLine(2, this);
                EraseDynamic();
            }
        }
    }

    private void IncreaseHeat()
    {
        Rigidbody2D rb = null;

        if (CompareTag("BlueLine") && transform.parent != null)
            rb = transform.parent.GetComponent<Rigidbody2D>();

        // Dynamic blue lines heat up faster
        heatLevel += Time.deltaTime * ((rb != null && rb.bodyType == RigidbodyType2D.Dynamic) ? 1.5f : 1f);
    }

    private void EraseDynamic()
    {
        SplitFromParent(applyVelocity: true);

        _chalkManager.ReplenishChalk(0.1f);
        transform.parent = null;
        Destroy(gameObject);
        isErased = true;
    }

    // Blue lines can be split if erased down the middle
    private void SplitFromParent(bool applyVelocity)
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        Rigidbody2D parentRb = parent.GetComponent<Rigidbody2D>();
        Vector2 velocity = parentRb.velocity;

        int oldChildren = 0;
        int newChildren = 0;

        GameObject newParent = Instantiate(
            dynamicLineParent,
            transform.position,
            Quaternion.identity,
            parent.parent
        );

        DrawManager drawManager = _chalkManager.GetComponent<DrawManager>();
        drawManager.UpdateDynamicParent(newParent);

        bool afterCurrent = false;
        List<Transform> children = new();

        foreach (Transform child in parent)
            children.Add(child);

        foreach (Transform child in children)
        {
            if (afterCurrent)
            {
                child.parent = newParent.transform;
                newChildren++;
            }
            else
            {
                if (child == transform)
                    afterCurrent = true;

                oldChildren++;
            }
        }

        if (newChildren == 0)
        {
            Destroy(newParent);
        }
        else
        {
            Rigidbody2D rb = newParent.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;

            if (applyVelocity)
                rb.velocity = velocity;
        }

        if (oldChildren == 1)
            Destroy(parent.gameObject);
        else
            parentRb.bodyType = RigidbodyType2D.Dynamic;
    }
}
