// ===============================================================================
// DrawManager.cs
// Manages the draw/erase system, allowing chalk types with different properties
// Different systems for drawing static vs. dynamic lines
// ===============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class DrawManager : MonoBehaviour
{
    // CONSTANTS
    public const float RESOLUTION = 0.1f;
    public const float RESOLUTION_ICE = 0.5f;
    public const float amountChalkUsed = 0.1f;


    // SCENE REFERENCES
    private Camera _cam;

    [Header("Line Prefabs")]
    [SerializeField] private Line _whiteLinePrefab;
    [SerializeField] private Line _redLinePrefab;
    [SerializeField] private Line _blueLinePrefab;

    [Header("Parents")]
    [SerializeField] private GameObject _parent;
    public GameObject dynamicLineParent;

    [Header("Chalk Managers")]
    [SerializeField] private ChalkManager _whiteChalkManager;
    [SerializeField] private ChalkManager _redChalkManager;
    [SerializeField] private ChalkManager _blueChalkManager;

    [Header("Tools")]
    public GameObject eraser;
    public GameObject screenClear;


    // RUNTIME STATE
    private Vector2 prevMousePos;

    private ChalkManager _chalkManager;
    private Line _linePrefab;

    private GameObject eraserInstance;
    private GameObject screenClearInstance;

    private GameObject _currentDynamicParent;
    private Line _currentLine;

    private bool isDynamic = false;
    public float _resolution = RESOLUTION;


    // PERMISSIONS / UI STATE
    public bool canDrawWhite;
    public bool canDrawRed;
    public bool canDrawBlue;

    public bool hasWhite = false;
    public bool hasRed = false;
    public bool hasBlue = false;

    void Start()
    {
        _cam = Camera.main;
        prevMousePos = _cam.ScreenToWorldPoint(Input.mousePosition);

        // Default to white chalk manager
        _chalkManager = _whiteChalkManager;
    }


    void Update()
    {
        Vector2 mousePos = _cam.ScreenToWorldPoint(Input.mousePosition);
        bool canDraw = DrawZoneCheck(mousePos) && _linePrefab != null && (canDrawWhite || canDrawRed || canDrawBlue);

        HandleMouseInput(mousePos, canDraw);
        HandleColorHotkeys(mousePos);

        if (Input.GetKeyDown("e"))
        {
            chalkClear();
        }

        prevMousePos = mousePos;
    }

    private void HandleMouseInput(Vector2 mousePos, bool canDraw)
    {
        // Begin draw
        if (Input.GetMouseButtonDown(0) && canDraw)
        {
            StartLine(mousePos);
        }

        // Continue draw
        if (Input.GetMouseButton(0))
        {
            if (isDynamic)
                DrawDynamicLine(mousePos, canDraw);
            else
                DrawStaticLine(mousePos, canDraw);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            eraserInstance = Instantiate(eraser, mousePos, Quaternion.identity);
        }

        // Finish draw
        if (Input.GetMouseButtonUp(0))
        {
            FinishLine();
        }

        if (Input.GetMouseButtonUp(1))
        {
            Destroy(eraserInstance);
        }
    }


    private void StartLine(Vector2 mousePos)
    {
        if (!isDynamic)
        {
            _currentLine = Instantiate(_linePrefab, mousePos, Quaternion.identity, _parent.transform);
        }
        else
        {
            _currentDynamicParent = Instantiate(dynamicLineParent, mousePos, Quaternion.identity, _parent.transform);
            _currentLine = Instantiate(_linePrefab, mousePos, Quaternion.identity, _currentDynamicParent.transform);
        }

        _currentLine._chalkManager = _chalkManager;
        _currentLine.SetPosition(mousePos);
    }

    private void FinishLine()
    {
        if (_currentLine == null)
            return;

        if (isDynamic && _currentDynamicParent != null)
        {
            Rigidbody2D rb = _currentDynamicParent.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.bodyType = RigidbodyType2D.Dynamic;
        }

        _currentLine.destroy();
        _currentLine = null;
    }

    private void DrawStaticLine(Vector2 mousePos, bool canDraw)
    {
        Vector2 nextPos = Vector2.MoveTowards(prevMousePos, mousePos, RESOLUTION);

        if (_currentLine == null && !_chalkManager.isEmpty() && canDraw)
        {
            StartLine(mousePos);
        }

        while (_currentLine != null && _currentLine.CanAppendWorldSpace(mousePos) && _chalkManager.chalkAmount > 0)
        {
            if (_chalkManager.isEmpty())
            {
                _currentLine.destroy();
                break;
            }

            canDraw = DrawZoneCheck(nextPos);

            if (!canDraw)
            {
                nextPos = Vector2.MoveTowards(nextPos, mousePos, RESOLUTION);
                _currentLine.destroy();
                ReadyNextStaticLineSegment(nextPos);
            }
            else if (_currentLine.SetPosition(nextPos))
            {
                _chalkManager.ReduceChalk(amountChalkUsed);
                ReadyNextStaticLineSegment(nextPos);
                nextPos = Vector2.MoveTowards(nextPos, mousePos, RESOLUTION);
            }
            else
            {
                nextPos = Vector2.MoveTowards(nextPos, mousePos, RESOLUTION);
            }

        }
    }

    private void ReadyNextStaticLineSegment(Vector2 pos)
    {
        _currentLine = Instantiate(_linePrefab, pos, Quaternion.identity, _parent.transform);
        _currentLine._chalkManager = _chalkManager;
        _currentLine.SetPosition(pos);
    }

    private void DrawDynamicLine(Vector2 mousePos, bool canDraw)
    {
        Vector2 nextPos = Vector2.MoveTowards(prevMousePos, mousePos, _resolution);

        if (_currentLine == null && !_chalkManager.isEmpty() && canDraw)
        {
            _currentDynamicParent = Instantiate(dynamicLineParent, mousePos, Quaternion.identity, _parent.transform);
            _currentLine = Instantiate(_linePrefab, mousePos, Quaternion.identity, _currentDynamicParent.transform);
            _currentLine._chalkManager = _chalkManager;
            _currentLine.SetPosition(mousePos);
        }


        while (_currentLine != null && _currentLine.CanAppendWorldSpace(mousePos) && _chalkManager.chalkAmount > 0)
        {
            if (_chalkManager.isEmpty())
            {
                _currentLine.destroy();
                SetDynamicParentActive();
                break;
            }

            canDraw = DrawZoneCheck(nextPos);

            if (!canDraw)
            {
                nextPos = Vector2.MoveTowards(nextPos, mousePos, _resolution);
                _currentLine.destroy();
                SetDynamicParentActive();

                _currentDynamicParent = Instantiate(dynamicLineParent, mousePos, Quaternion.identity, _parent.transform);
                ReadyNextDynamicLineSegment(nextPos);
            }
            else if (_currentLine.SetPosition(nextPos))
            {
                _chalkManager.ReduceChalk(amountChalkUsed);
                _currentLine.bluePrevCheck = false;

                ReadyNextDynamicLineSegment(nextPos);
                nextPos = Vector2.MoveTowards(nextPos, mousePos, _resolution);
            }
            else
            {
                nextPos = Vector2.MoveTowards(nextPos, mousePos, _resolution);
            }

        }
    }

    private void ReadyNextDynamicLineSegment(Vector2 pos)
    {
        _currentLine = Instantiate(_linePrefab, pos, Quaternion.identity, _currentDynamicParent.transform);
        _currentLine._chalkManager = _chalkManager;
        _currentLine.SetPosition(pos);
    }

    private void SetDynamicParentActive()
    {
        Rigidbody2D rb = _currentDynamicParent.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Dynamic;
    }

    // Uses a screen raycast to detect if the line is in an anti-draw zone
    private bool DrawZoneCheck(Vector2 curPos)
    {
        Ray ray = _cam.ScreenPointToRay(_cam.WorldToScreenPoint(curPos));
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);


        foreach (RaycastHit2D hit in hits)
        {
            if (!hit)
                continue;

            string tag = hit.collider.tag;

            if (tag == "NoDraw" || tag == "Eraser" || tag == "Ground")
                return false;

            if (isDynamic && tag == "BlueLine")
            {
                if (!hit.collider.gameObject.GetComponent<Line>().bluePrevCheck)
                {
                    return false;
                }
            }
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(curPos, _whiteLinePrefab._renderer.startWidth - 0.03f);

        foreach (Collider2D collider2D in colliders)
        {
            if (!collider2D)
                continue;

            String _tag = collider2D.tag;

            if (tag == "Player")
                return false;

            if (!isDynamic && tag == "BlueLine")
                return false;

            if (isDynamic && (tag == "White" || tag == "Red"))
                return false;

            if (isDynamic &&  _tag == "BlueLine" && collider2D.transform.parent.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic)
                return false;
        }
        return true;
    }

    public void chalkClear()
    {
        screenClearInstance = Instantiate(screenClear, Vector3.zero, Quaternion.identity);
    }

    private void HandleColorHotkeys(Vector2 mousePos)
    {
        if (Input.GetKeyDown("1") && canDrawWhite)
            ChangeColorInternal(0, mousePos);

        else if (Input.GetKeyDown("3") && canDrawRed)
            ChangeColorInternal(1, mousePos);

        else if (Input.GetKeyDown("2") && canDrawBlue)
            ChangeColorInternal(2, mousePos);
    }

    public void ChangeColor(int index)
    {
        ChangeColorInternal(index, prevMousePos);
    }

    // Switch selected color of the manager
    private void ChangeColorInternal(int index, Vector2 mousePos)
    {
        if (_currentLine != null && Input.GetMouseButton(0))
        {
            _currentLine.destroy();
            if (isDynamic)
                SetDynamicParentActive();
        }

        switch (index)
        {
            case 0: // White
                canDrawWhite = true;
                canDrawRed = canDrawBlue = false;
                _linePrefab = _whiteLinePrefab;
                _chalkManager = _whiteChalkManager;
                isDynamic = false;
                _resolution = RESOLUTION;
                break;

            case 1: // Red
                canDrawRed = true;
                canDrawWhite = canDrawBlue = false;
                _linePrefab = _redLinePrefab;
                _chalkManager = _redChalkManager;
                isDynamic = false;
                _resolution = RESOLUTION;
                break;

            case 2: // Blue
                canDrawBlue = true;
                canDrawWhite = canDrawRed = false;
                _linePrefab = _blueLinePrefab;
                _chalkManager = _blueChalkManager;
                isDynamic = true;
                _resolution = RESOLUTION_ICE;
                break;
        }

        if (Input.GetMouseButton(0))
            StartLine(mousePos);
    }

    // Creates a new line of different color over the existing line (white chalk to red)
    public void createLine(int index, Line oldLine)
    {
        if (oldLine._renderer.positionCount <= 1)
            return;

        Line prefab = index == 1 ? _redLinePrefab : _whiteLinePrefab;
        ChalkManager manager = index == 1 ? _redChalkManager : _whiteChalkManager;

        Line newLine = Instantiate(prefab, oldLine.transform.position, Quaternion.identity, _parent.transform);
        newLine._chalkManager = manager;

        newLine.SetPosition(oldLine._renderer.GetPosition(0) + oldLine.transform.position);
        newLine.SetPosition(oldLine._renderer.GetPosition(1) + oldLine.transform.position);

        if (index == 2)
            newLine.touchedLava = true;
    }

    public void UpdateDynamicParent(GameObject newParent)
    {
        _currentDynamicParent = newParent;

        if(_currentLine != null)
        {
            _currentLine.gameObject.transform.parent = newParent.transform;
        }
    }
}
