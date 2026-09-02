using UnityEngine;

namespace Ulon.Client
{
    public sealed class CharacterAnim : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AttackId = Animator.StringToHash("Attack");

        Animator animator;
        ClickMotor motor;

        void Awake()
        {
            Bind();
            motor = GetComponent<ClickMotor>() ?? GetComponentInParent<ClickMotor>();
        }

        void Start()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                Bind();
        }

        void Bind()
        {
            StripEmptyAnimators();
            animator = FindAnimator();
            if (animator == null)
                return;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
                smrs[i].updateWhenOffscreen = true;
        }

        void StripEmptyAnimators()
        {
            var anims = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i].runtimeAnimatorController == null)
                    Destroy(anims[i]);
            }
        }

        Animator FindAnimator()
        {
            var nested = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < nested.Length; i++)
                if (nested[i].runtimeAnimatorController != null)
                    return nested[i];
            return GetComponent<Animator>();
        }

        void Update()
        {
            if (animator == null)
                return;
            animator.SetFloat(SpeedId, motor != null ? motor.PlanarSpeed : 0f);
        }

        public void PlayAttack()
        {
            if (animator == null)
                Bind();
            if (animator == null)
                return;
            animator.ResetTrigger(AttackId);
            animator.SetTrigger(AttackId);
            animator.CrossFadeInFixedTime("Attack", 0.05f, 0, 0f);
        }
    }
}
