// ============================================================================
// Controller2D.cs
// Handles 2D character movement using raycasts instead of Rigidbody physics
// Responsible for horizontal/vertical collisions, slopes, and special surfaces (ice)
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller2D : RayCastController
{
    float maxSlopeAngle = 70;
    
    public CollisionInfo collisions;

    [HideInInspector]
    public Vector2 playerInput;

    float iceTimer = 100f;

    public override void Start()
    {
        base.Start();
        collisions.onIce = false;
    }
    
    public void Move(Vector2 velocity, Vector2 input, bool standingOnPlatform = false)
    {
        UpdateRaycastOrigins();
        collisions.Reset();

        iceTimer += Time.deltaTime;
        collisions.velocityOld = velocity;
        playerInput = input;

        // Handle slope descent before horizontal movement
        if (velocity.y < 0)
            DescendSlope(ref velocity);

        if (velocity.x != 0)
            HandleHorizontalCollisions(ref velocity);

        if (velocity.y != 0)
            HandleVerticalCollisions(ref velocity);

        // Apply final movement
        transform.Translate(velocity);

        if (standingOnPlatform)
        {
            collisions.below = true;
        }
    }

    void HandleHorizontalCollisions(ref Vector2 velocity)
    {
        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + skinWidth;

        for (int i = 0; i < horizontalRayCount; i++)
        {
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.red);

            if (!hit) continue;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            // Handle climbing slopes only on the lowest ray
            if (i == 0 && slopeAngle <= maxSlopeAngle) 
            {
                if(collisions.descendingSlope)
                {
                    collisions.descendingSlope = false;
                    velocity = collisions.velocityOld;
                }

                float distanceToSlopeStart = 0;
                if (slopeAngle != collisions.slopeAngleOld)
                {
                    distanceToSlopeStart = hit.distance - skinWidth;
                    velocity.x -= distanceToSlopeStart * directionX;
                }

                ClimbSlope(ref velocity, slopeAngle);
                velocity.x += distanceToSlopeStart * directionX;
            }

            // Standard horizontal collision
            if (!collisions.climbingSlope || slopeAngle > maxSlopeAngle)
            {
                velocity.x = (hit.distance - skinWidth) * directionX;
                rayLength = hit.distance;

                if(collisions.climbingSlope)
                {
                    velocity.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
                }

                collisions.left = directionX == -1;
                collisions.right = directionX == 1;
            }
        }
    }

    void HandleVerticalCollisions(ref Vector2 velocity)
    {
        float directionY = Mathf.Sign(velocity.y);
        float rayLength = Mathf.Abs(velocity.y) + skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);

            if (!hit) continue;

            velocity.y = (hit.distance - skinWidth) * directionY;
            rayLength = hit.distance;

            if(collisions.climbingSlope)
            {
                velocity.x = velocity.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(velocity.x);
            }

            collisions.below = directionY == -1;
            collisions.above = directionY == 1;

            HandleIce(hit, directionY);
        }

        // Extra upward probe to prevent ceiling clipping when moving downward
        if (directionY != 1)
            CheckCeiling(ref velocity, rayLength);

        // Re-check slope angle while climbing
        AdjustSlopeAfterVertical(ref velocity);
    }

    void HandleIce(RaycastHit2D hit, float directionY)
    {
        if (collisions.below && hit.collider.CompareTag("BlueLine"))
        {
            collisions.onIce = true;
            iceTimer = 0f;
        }
        else if (iceTimer > 0.2f)
        {
            collisions.onIce = false;
        }
    }

    void CheckCeiling(ref Vector2 velocity, float rayLength)
    {
        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = raycastOrigins.topLeft + Vector2.right * (verticalRaySpacing * i + velocity.x);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * rayLength, Color.blue);

            if (!hit || velocity.y <= 0) continue;

            velocity.y = hit.distance - skinWidth;
            collisions.above = true;
        }
    }

    void AdjustSlopeAfterVertical(ref Vector2 velocity)
    {
        if (!collisions.climbingSlope) return;

        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + skinWidth;

        Vector2 rayOrigin = (directionX == -1 ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up * velocity.y;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

        if (!hit) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle != collisions.slopeAngle)
        {
            velocity.x = (hit.distance - skinWidth) * directionX;
            collisions.slopeAngle = slopeAngle;
        }
    }

    void ClimbSlope(ref Vector2 velocity, float slopeAngle)
    {
        float moveDistance = Mathf.Abs(velocity.x);
        float climbVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if(velocity.y <= climbVelocityY)
        {
            velocity.y = climbVelocityY;
            velocity.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);

            collisions.below = true;
            collisions.climbingSlope = true;
            collisions.slopeAngle = slopeAngle;
        }
        
    }

    void DescendSlope(ref Vector2 velocity)
    {
        RaycastHit2D maxSlopeHitLeft = Physics2D.Raycast(raycastOrigins.bottomLeft, Vector2.down, Mathf.Abs(velocity.y) + skinWidth, collisionMask);
        RaycastHit2D maxSlopeHitRight = Physics2D.Raycast(raycastOrigins.bottomRight, Vector2.down, Mathf.Abs(velocity.y) + skinWidth, collisionMask);
        SlideDownMaxSlope(maxSlopeHitLeft, ref velocity);
        SlideDownMaxSlope(maxSlopeHitRight, ref velocity);

        if (collisions.slidingDownMaxSlope) return;


        float directionX = Mathf.Sign(velocity.x);
        Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, -Vector2.up, Mathf.Infinity, collisionMask);

        if (!hit) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle != 0 && slopeAngle < maxSlopeAngle)
        {
            if (Mathf.Sign(hit.normal.x) == directionX)
            {
                float moveDistance = Mathf.Abs(velocity.x);
                if (hit.distance - skinWidth <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * moveDistance)
                {
                    float descendVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
                    velocity.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);
                    velocity.y -= descendVelocityY;

                    collisions.slopeAngle = slopeAngle;
                    collisions.descendingSlope = true;
                    collisions.below = true;
                }
            }
        }
    }

    void SlideDownMaxSlope(RaycastHit2D hit, ref Vector2 velocity)
    {
        if (!hit) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if(slopeAngle > maxSlopeAngle)
        {
            velocity.x = hit.normal.x * (Mathf.Abs(velocity.y) - hit.distance) / Mathf.Tan(slopeAngle * Mathf.Deg2Rad);

            collisions.slopeAngle = slopeAngle;
            collisions.slidingDownMaxSlope = true;
        }
    }

    public struct CollisionInfo
    {
        public bool above, below, left, right;

        public bool climbingSlope, descendingSlope, slidingDownMaxSlope;
        public float slopeAngle, slopeAngleOld;
        public Vector2 velocityOld;
        public bool onIce;

        public void Reset()
        {
            above = below = left = right = false;
            climbingSlope = descendingSlope = slidingDownMaxSlope = false;

            slopeAngleOld = slopeAngle;
            slopeAngle = 0;
            //onIce = false;
        }
    }
    
}
