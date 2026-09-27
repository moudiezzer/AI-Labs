using UnityEngine;

public class StandardEnemyAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private WaypointPatrol patrol;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float waypointReachDistance = 0.1f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float fieldOfView = 180f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 10;

    [Header("Attack Animation")]
    [SerializeField] private float attackScaleMultiplier = 1.35f;
    [SerializeField] private float attackScaleDuration = 0.3f;

    private bool isAttackScaling;
    private float attackScaleTimer;
    private Vector3 originalScale;

    [Header("Search")]
    [SerializeField] private float searchDuration = 3f;
    [SerializeField] private float searchWaitTime = 1f;

    private EnemyState currentState = EnemyState.Idle;

    private Rigidbody2D rb;

    private int currentWaypoint;

    private Vector2 lastKnownPlayerPosition;

    private float searchTimer;
    private float searchWaitTimer;
    private float attackTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Store the original scale for the attack animation.
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // Update attack scale animation.
        if (isAttackScaling)
        {
            UpdateAttackScale();
        }

        // FSM.
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
        }

        // Update attack cooldown.
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    // =========================================================
    // IDLE
    // =========================================================

    private void UpdateIdle()
    {
        if (CanSeePlayer())
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
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        if (patrol == null || patrol.Count == 0)
        {
            return;
        }

        Transform waypoint = patrol.GetWaypoint(currentWaypoint);

        if (waypoint == null)
        {
            return;
        }

        MoveTowards(waypoint.position);

        if (Vector2.Distance(
            transform.position,
            waypoint.position
        ) <= waypointReachDistance)
        {
            currentWaypoint++;

            if (currentWaypoint >= patrol.Count)
            {
                currentWaypoint = 0;
            }
        }
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void UpdateChase()
    {
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;
        }
        else
        {
            // Player is no longer visible.
            ChangeState(EnemyState.Search);
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        // Enter attack state when close enough.
        if (distance <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
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

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        // Return to chase when the player leaves attack range.
        if (distance > attackRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Face the player.
        FaceTowards(player.position);

        // Attack when the cooldown is ready.
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
            // Start the visual attack animation.
            StartAttackScale();

            // Deal damage to the player.
            health.TakeDamage(attackDamage);
        }
        else
        {
            Debug.LogError(
                "PlayerHealth component was not found on the Player!"
            );
        }
    }

    // =========================================================
    // ATTACK SCALE ANIMATION
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
            attackScaleTimer / attackScaleDuration;

        progress = Mathf.Clamp01(progress);

        // Creates a smooth 0 -> 1 -> 0 animation.
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

        // Return exactly to the original scale.
        if (progress >= 1f)
        {
            transform.localScale = originalScale;
            isAttackScaling = false;
        }
    }

    // =========================================================
    // SEARCH
    // =========================================================

    private void UpdateSearch()
    {
        searchTimer += Time.deltaTime;

        // Move toward the player's last known position.
        if (searchWaitTimer <= searchWaitTime)
        {
            MoveTowards(lastKnownPlayerPosition);

            float distance = Vector2.Distance(
                transform.position,
                lastKnownPlayerPosition
            );

            if (distance <= waypointReachDistance)
            {
                searchWaitTimer += Time.deltaTime;
            }
        }

        // Player found again.
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;

            ChangeState(EnemyState.Chase);
            return;
        }

        // Search finished.
        if (searchTimer >= searchDuration)
        {
            searchTimer = 0f;
            searchWaitTimer = 0f;

            ChangeState(EnemyState.Patrol);
        }
    }

    // =========================================================
    // DETECTION
    // =========================================================

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        Vector2 directionToPlayer =
            (player.position - transform.position).normalized;

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                player.position
            );

        // Detection range.
        if (distanceToPlayer > detectionRange)
        {
            return false;
        }

        // 180 degree field of view.
        float angle =
            Vector2.Angle(
                transform.up,
                directionToPlayer
            );

        if (angle > fieldOfView / 2f)
        {
            return false;
        }

        // Line of sight.
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            directionToPlayer,
            distanceToPlayer,
            obstacleLayer | playerLayer
        );

        if (hit.collider == null)
        {
            return false;
        }

        // The first object hit must be the player.
        if (hit.transform == player)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MoveTowards(Vector2 target)
    {
        Vector2 direction =
            (target - (Vector2)transform.position).normalized;

        rb.MovePosition(
            rb.position +
            direction *
            moveSpeed *
            Time.deltaTime
        );

        FaceTowards(target);
    }

    private void FaceTowards(Vector2 target)
    {
        Vector2 direction =
            (target - (Vector2)transform.position).normalized;

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

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;

        if (newState == EnemyState.Search)
        {
            searchTimer = 0f;
            searchWaitTimer = 0f;
        }
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        // Detection range.
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        // Attack range.
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        // FOV.
        Vector3 leftDirection =
            Quaternion.Euler(
                0f,
                0f,
                fieldOfView / 2f
            ) * transform.up;

        Vector3 rightDirection =
            Quaternion.Euler(
                0f,
                0f,
                -fieldOfView / 2f
            ) * transform.up;

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            leftDirection *
            detectionRange
        );

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            rightDirection *
            detectionRange
        );

        // Last known player position.
        Gizmos.DrawWireSphere(
            lastKnownPlayerPosition,
            0.15f
        );
    }
}