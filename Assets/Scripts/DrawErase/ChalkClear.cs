// ===============================================================================
// ChalkClear.cs
// Erases all chalk within bounds, and deletes itself immediately
// ===============================================================================
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class ChalkClear : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(deleteSelf());
    }
    private IEnumerator deleteSelf()
    {
        yield return new WaitForSeconds(.1f);
        var guo = new GraphUpdateObject(GetComponent<Collider2D>().bounds);
        guo.updatePhysics = true;
        if (AstarPath.active != null) { AstarPath.active.UpdateGraphs(guo); }
        Destroy(gameObject);
    }
}
