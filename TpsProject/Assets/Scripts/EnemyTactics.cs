using UnityEngine;
using UnityEngine.AI;

public class EnemyTactics : MonoBehaviour
{
    [Header("References")]
    public Transform self;
    public Transform player;
    public EnemyHealth health;

    [Header("Type")]
    public EnemyType enemyType = EnemyType.Melee;

    [Header("Tactics")]
    public bool useTactics = true;
    public float coverSearchRadius = 22f;
    public float coverDecisionInterval = 1.0f;
    public float lowHealthCoverRatio = 0.55f;
    public LayerMask obstacleMask = ~0;

    private float nextCoverDexisionTime;

    private void Awake()
    {
        if (self == null) self = transform;
        if (health == null) health = GetComponent<EnemyHealth>();
    }

    public bool ShouldTakeCover(bool canSeePlayer, bool isInAttackState)
    {
        if (!useTactics) return false;
        if (player == null) return false;
        if (!canSeePlayer) return false;
        if (Time.time < nextCoverDexisionTime) return false;

        nextCoverDexisionTime = Time.time + coverDecisionInterval;

        float hpRatio = 1f;

        if(health != null && health.maxHealth > 0.01f)
        {
            hpRatio = health.currentHealth / health.maxHealth;
        }  
        
        if(enemyType == EnemyType.Ranged)
        {
            return hpRatio <= lowHealthCoverRatio || isInAttackState;
        }

        return hpRatio <= lowHealthCoverRatio * 0.7f;
    }

    public CoverPoint FindBestCover(float preferredRange)
    {
        if (!useTactics) return null;
        if (player == null) return null;
        if (CoverPoint.All.Count == 0) return null;

        float bestScore = float.MinValue;
        CoverPoint best = null;


        for (int i = 0; i < CoverPoint.All.Count; i++)
        {
            CoverPoint point = CoverPoint.All[i];
            if (point == null) continue;

            float toPoint = Vector3.Distance(self.position, point.transform.position);
            if (toPoint > coverSearchRadius) continue;

            Vector3 stand = point.GetStandPosition(player.position);
            if (!NavMesh.SamplePosition(stand, out NavMeshHit sampled, 2.0f, NavMesh.AllAreas))
                continue;

            stand = sampled.position;

            Vector3 eye = stand + Vector3.up * 1.2f;
            Vector3 playerEye = player.position + Vector3.up * 1.2f;

            bool blocked = Physics.Linecast(eye, playerEye, obstacleMask, QueryTriggerInteraction.Ignore);
            if (!blocked) continue;

            float distToPlayer = Vector3.Distance(stand, player.position);
            float rangeScore = 1f - Mathf.Clamp01(Mathf.Abs(distToPlayer - preferredRange) / Mathf.Max(preferredRange, 0.1f));

            float score = 0f;
            score += 60f;
            score += rangeScore * 25f;
            score -= toPoint * 1.2f;
            score += Random.Range(0f, 3f);

            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }
}
