using UnityEngine;

public class TeleportEnemyAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private WaypointPatrol patrol;

    [Header("Attack Animation")]
    [SerializeField] private float attackScaleMultiplier = 1.35f;
    [SerializeField] private float attackScaleDuration = 0.3f;

    private bool isAttackScaling;
    private float attackScaleTimer;
    private Vector3 originalScale;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float waypointReachDistance = 0.1f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 1f;
    [SerializeField] private float scanInterval = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Teleport Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color teleportReadyColor = Color.red;
    [SerializeField] private Color teleportEmptyColor = Color.white;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 10;

    [Header("Search")]
    [SerializeField] private float searchDuration = 3f;

    [Header("Teleport")]
    [SerializeField] private float teleportDistance = 3f;
    [SerializeField] private float teleportCooldown = 5f;
    [SerializeField] private float minimumDistanceFromPlayer = 1.5f;
    [SerializeField] private LayerMask teleportObstacleLayer;

    [Header("Patrol Teleport")]
    [SerializeField] private bool allowPatrolTeleport = true;
    [SerializeField] private float patrolTeleportDistance = 3f;
    [SerializeField] private float patrolObstacleCheckRadius = 0.2f;

    private EnemyState currentState = EnemyState.Idle;

    private Rigidbody2D rb;

    private int currentWaypoint;

    private Vector2 lastKnownPlayerPosition;

    private float scanTimer;
    private float searchTimer;
    private float attackTimer;
    private float teleportTimer;

    private Vector2 pendingTeleportPosition;

    private bool teleportWasForPatrol;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (isAttackScaling)
        {
            UpdateAttackScale();
        }

        UpdateTeleportColor();

        scanTimer += Time.deltaTime;

        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }

        if (teleportTimer > 0f)
        {
            teleportTimer -= Time.deltaTime;
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;

            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;

            case EnemyState.Search:
                UpdateSearch();
                break;

            case EnemyState.Teleport:
                UpdateTeleport();
                break;
        }
    }

    // =========================================================
    // IDLE
    // =========================================================

    private void UpdateIdle()
    {
        if (ScanForPlayer())
        {
            lastKnownPlayerPosition = player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        if (patrol != null && patrol.Count > 0)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    // =========================================================
    // PATROL
    // =========================================================

    private void UpdatePatrol()
    {
        if (ScanForPlayer())
        {
            lastKnownPlayerPosition = player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        if (patrol == null || patrol.Count == 0)
        {
            return;
        }

        Transform waypoint =
            patrol.GetWaypoint(currentWaypoint);

        if (waypoint == null)
        {
            return;
        }

        Vector2 waypointPosition = waypoint.position;

        float distanceToWaypoint =
            Vector2.Distance(
                transform.position,
                waypointPosition
            );

        // Waypoint reached.
        if (distanceToWaypoint <= waypointReachDistance)
        {
            currentWaypoint++;

            if (currentWaypoint >= patrol.Count)
            {
                currentWaypoint = 0;
            }

            return;
        }

        // =====================================================
        // TELEPORT ONLY IF THERE IS AN OBSTACLE
        // =====================================================

        if (allowPatrolTeleport &&
            teleportTimer <= 0f)
        {
            bool pathBlocked =
                IsPathBlocked(
                    transform.position,
                    waypointPosition
                );

            if (pathBlocked)
            {
                Vector2 teleportPosition;

                if (FindPatrolTeleportPosition(
                    waypointPosition,
                    out teleportPosition))
                {
                    pendingTeleportPosition =
                        teleportPosition;

                    teleportWasForPatrol = true;

                    ChangeState(EnemyState.Teleport);

                    return;
                }
            }
        }
        MoveTowards(waypointPosition);
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void UpdateChase()
    {
        if (player == null)
        {
            return;
        }

        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;

            float distance =
                Vector2.Distance(
                    transform.position,
                    player.position
                );

            if (distance > detectionRange)
            {
                lastKnownPlayerPosition =
                    player.position;

                ChangeState(EnemyState.Search);

                return;
            }

            lastKnownPlayerPosition =
                player.position;
        }

        float currentDistance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (currentDistance <= attackRange)
        {
            ChangeState(EnemyState.Attack);

            return;
        }

        // Normal teleport toward player.
        if (teleportTimer <= 0f)
        {
            Vector2 teleportPosition;

            if (FindTeleportPosition(
                out teleportPosition))
            {
                pendingTeleportPosition =
                    teleportPosition;

                teleportWasForPatrol = false;

                ChangeState(EnemyState.Teleport);

                return;
            }
        }

        MoveTowards(player.position);
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void UpdateAttack()
    {
        if (player == null)
        {
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (distance > attackRange)
        {
            ChangeState(EnemyState.Chase);

            return;
        }

        FaceTowards(player.position);

        if (attackTimer <= 0f)
        {
            AttackPlayer();

            attackTimer = attackCooldown;
        }
    }

    private void AttackPlayer()
    {
        if (player == null)
        {
            return;
        }

        PlayerHealth health =
            player.GetComponentInParent<PlayerHealth>();

        if (health == null)
        {
            health =
                player.GetComponentInChildren<PlayerHealth>();
        }

        if (health != null)
        {
            StartAttackScale();

            health.TakeDamage(attackDamage);
        }
    }

    // =========================================================
    // ATTACK SCALE
    // =========================================================

    private void StartAttackScale()
    {
        if (isAttackScaling)
        {
            return;
        }

        isAttackScaling = true;
        attackScaleTimer = 0f;
    }

    private void UpdateAttackScale()
    {
        if (attackScaleDuration <= 0f)
        {
            transform.localScale = originalScale;
            isAttackScaling = false;

            return;
        }

        attackScaleTimer += Time.deltaTime;

        float progress =
            attackScaleTimer /
            attackScaleDuration;

        progress = Mathf.Clamp01(progress);

        float scaleProgress =
            Mathf.Sin(progress * Mathf.PI);

        float multiplier =
            Mathf.Lerp(
                1f,
                attackScaleMultiplier,
                scaleProgress
            );

        transform.localScale =
            originalScale * multiplier;

        if (progress >= 1f)
        {
            transform.localScale =
                originalScale;

            isAttackScaling = false;
        }
    }

    // =========================================================
    // SEARCH
    // =========================================================

    private void UpdateSearch()
    {
        searchTimer += Time.deltaTime;

        if (ScanForPlayer())
        {
            lastKnownPlayerPosition =
                player.position;

            searchTimer = 0f;

            ChangeState(EnemyState.Chase);

            return;
        }

        // Only teleport if the path to the
        // last known position is actually blocked.
        if (allowPatrolTeleport &&
            teleportTimer <= 0f &&
            IsPathBlocked(
                transform.position,
                lastKnownPlayerPosition))
        {
            Vector2 teleportPosition;

            if (FindPatrolTeleportPosition(
                lastKnownPlayerPosition,
                out teleportPosition))
            {
                pendingTeleportPosition =
                    teleportPosition;

                teleportWasForPatrol = true;

                ChangeState(EnemyState.Teleport);

                return;
            }
        }

        MoveTowards(lastKnownPlayerPosition);

        if (searchTimer >= searchDuration)
        {
            searchTimer = 0f;

            ChangeState(EnemyState.Patrol);
        }
    }

    // =========================================================
    // TELEPORT
    // =========================================================

    private void UpdateTeleport()
    {
        transform.position =
            pendingTeleportPosition;

        teleportTimer =
            teleportCooldown;

       

        // If teleport was caused by a patrol obstacle,
        // continue patrolling.
        if (teleportWasForPatrol)
        {
            teleportWasForPatrol = false;

            ChangeState(EnemyState.Patrol);

            return;
        }

        // Otherwise it was a combat teleport.
        ChangeState(EnemyState.Chase);
    }

    // =========================================================
    // TELEPORT COLOR
    // =========================================================

    private void UpdateTeleportColor()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float charge;

        if (teleportCooldown <= 0f)
        {
            charge = 1f;
        }
        else
        {
            charge =
                1f -
                (teleportTimer /
                 teleportCooldown);
        }

        charge = Mathf.Clamp01(charge);

        spriteRenderer.color =
            Color.Lerp(
                teleportEmptyColor,
                teleportReadyColor,
                charge
            );
    }

    // =========================================================
    // PLAYER DETECTION
    // =========================================================

    private bool ScanForPlayer()
    {
        if (player == null)
        {
            return false;
        }

        if (scanTimer < scanInterval)
        {
            return false;
        }

        scanTimer = 0f;

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        // 360-degree detection.
        // Walls do not block this scan.
        if (distance <= detectionRange)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // PLAYER TELEPORT POSITION
    // =========================================================

    private bool FindTeleportPosition(
        out Vector2 teleportPosition)
    {
        teleportPosition =
            transform.position;

        if (player == null)
        {
            return false;
        }

        Vector2 direction =
            (
                (Vector2)player.position -
                (Vector2)transform.position
            ).normalized;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (distanceToPlayer <=
            minimumDistanceFromPlayer)
        {
            return false;
        }

        float actualTeleportDistance =
            Mathf.Min(
                teleportDistance,
                distanceToPlayer -
                minimumDistanceFromPlayer
            );

        if (actualTeleportDistance <= 0.05f)
        {
            return false;
        }

        Vector2 targetPosition =
            (Vector2)transform.position +
            direction *
            actualTeleportDistance;

        Collider2D obstacle =
            Physics2D.OverlapCircle(
                targetPosition,
                0.25f,
                teleportObstacleLayer
            );

        if (obstacle != null)
        {
            return false;
        }

        teleportPosition =
            targetPosition;

        return true;
    }

    // =========================================================
    // PATROL TELEPORT POSITION
    // =========================================================

    private bool FindPatrolTeleportPosition(
        Vector2 target,
        out Vector2 teleportPosition)
    {
        teleportPosition =
            transform.position;

        Vector2 direction =
            target -
            (Vector2)transform.position;

        float distance =
            direction.magnitude;

        if (distance <= 0.05f)
        {
            return false;
        }

        direction.Normalize();

        float maxDistance =
            Mathf.Min(
                patrolTeleportDistance,
                distance
            );

        // Try positions starting from the furthest one.
        // This lets the enemy teleport THROUGH the obstacle
        // instead of stopping directly in front of it.
        float step = 0.1f;

        for (
            float currentDistance = maxDistance;
            currentDistance >= 0.5f;
            currentDistance -= step)
        {
            Vector2 candidate =
                (Vector2)transform.position +
                direction *
                currentDistance;

            Collider2D obstacle =
                Physics2D.OverlapCircle(
                    candidate,
                    patrolObstacleCheckRadius,
                    teleportObstacleLayer
                );

            if (obstacle != null)
            {
                continue;
            }

            teleportPosition =
                candidate;

            return true;
        }

        return false;
    }

    // =========================================================
    // PATH CHECK
    // =========================================================

    private bool IsPathBlocked(
        Vector2 start,
        Vector2 target)
    {
        Vector2 direction =
            target - start;

        float distance =
            direction.magnitude;

        if (distance <= 0.05f)
        {
            return false;
        }

        direction.Normalize();

        RaycastHit2D hit =
            Physics2D.Raycast(
                start,
                direction,
                distance,
                teleportObstacleLayer
            );

        return hit.collider != null;
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MoveTowards(Vector2 target)
    {
        Vector2 direction =
            (
                target -
                (Vector2)transform.position
            ).normalized;

        rb.MovePosition(
            rb.position +
            direction *
            moveSpeed *
            Time.deltaTime
        );

        FaceTowards(target);
    }

    // =========================================================
    // ROTATION
    // =========================================================

    private void FaceTowards(Vector2 target)
    {
        Vector2 direction =
            (
                target -
                (Vector2)transform.position
            ).normalized;

        if (direction.sqrMagnitude > 0.01f)
        {
            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x
                ) * Mathf.Rad2Deg - 90f;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );
        }
    }

    // =========================================================
    // STATE CHANGE
    // =========================================================

    private void ChangeState(
        EnemyState newState)
    {
        if (currentState == newState)
        {
            return;
        }

       

        currentState = newState;

        if (newState == EnemyState.Search)
        {
            searchTimer = 0f;
        }
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.DrawWireSphere(
            transform.position,
            teleportDistance
        );

        Gizmos.DrawWireSphere(
            lastKnownPlayerPosition,
            0.15f
        );
    }
}