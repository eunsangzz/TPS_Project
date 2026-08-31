using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMeleeAnimation : MonoBehaviour
{
    public const string LayerName = "PlayerMelee";
    public const float Duration = 0.65f;
    public const float ImpactTime = 0.28f;
    private static readonly int ReadyState = Animator.StringToHash(LayerName + ".Ready");
    private static readonly int AttackState = Animator.StringToHash(LayerName + ".Attack");
    private Animator animator;
    private PlayerMelee melee;
    private int layer = -1;

    public bool Available => isActiveAndEnabled && animator != null && animator.isActiveAndEnabled && animator.isHuman &&
        layer >= 0 && animator.HasState(layer, AttackState);

    public void Initialize(PlayerMelee owner, Animator target)
    {
        melee = owner;
        animator = target;
        layer = animator != null ? animator.GetLayerIndex(LayerName) : -1;
    }

    public bool PlayAttack()
    {
        if (!Available) return false;
        animator.SetLayerWeight(layer, 1f);
        animator.CrossFadeInFixedTime(AttackState, 0.035f, layer, 0f);
        return true;
    }

    public void CancelAttack()
    {
        if (!Available) return;
        animator.Play(ReadyState, layer, 0f);
    }

    // Animation event on the same object as Animator; the weapon owner can be a parent.
    public void OnMeleeImpact()
    {
        if (melee != null) melee.DeliverAnimationHit();
    }

    private void Update()
    {
        if (!Available || melee == null) return;
        float target = melee.IsEquipped ? 1f : 0f;
        animator.SetLayerWeight(layer, Mathf.MoveTowards(animator.GetLayerWeight(layer), target, Time.deltaTime * 12f));
    }

    private void OnDisable()
    {
        if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
    }
}
