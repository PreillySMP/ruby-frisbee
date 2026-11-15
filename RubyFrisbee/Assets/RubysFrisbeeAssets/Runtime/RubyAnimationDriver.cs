using System;
using UnityEngine;
using UnityEngine.Events;

namespace RubysFrisbee.Assets
{
    /// <summary>Visual adapter only. Feed it authoritative game state from your own controller.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class RubyAnimationDriver : MonoBehaviour
    {
        [Serializable] public sealed class CueEvent : UnityEvent<string> { }
        [Tooltip("Animation-event cue identifiers. No gameplay/scoring callbacks are connected by this package.")]
        public CueEvent animationCue = new CueEvent();

        static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        static readonly int IsSitting = Animator.StringToHash("IsSitting");
        static readonly int IsGameOver = Animator.StringToHash("IsGameOver");
        static readonly int Catch = Animator.StringToHash("Catch");
        static readonly int Celebrate = Animator.StringToHash("Celebrate");
        static readonly int GameOver = Animator.StringToHash("GameOver");
        static readonly int RecoverTrigger = Animator.StringToHash("Recover");
        Animator target;
        SpriteRenderer visual;
        bool gameOver;

        public bool GameOverActive => gameOver;
        public Animator TargetAnimator => EnsureAnimator();

        void Awake() { EnsureAnimator(); visual = GetComponent<SpriteRenderer>(); }
        Animator EnsureAnimator()
        {
            if (!target) target = GetComponent<Animator>();
            return target;
        }

        /// <param name="speed">Unsigned locomotion magnitude; facing is controlled separately.</param>
        public void SetLocomotion(float speed, float verticalSpeed, bool grounded)
        {
            var a = EnsureAnimator();
            a.SetFloat(MoveSpeed, Mathf.Abs(speed));
            a.SetFloat(VerticalSpeed, verticalSpeed);
            a.SetBool(IsGrounded, grounded);
            if (!grounded || Mathf.Abs(speed) > 0.12f) a.SetBool(IsSitting, false);
        }

        /// <summary>True faces right; assumes the supplied source sprites face right.</summary>
        public void SetFacing(bool facingRight)
        {
            if (!visual) visual = GetComponent<SpriteRenderer>();
            visual.flipX = !facingRight;
        }

        public void SetSitting(bool sitting) => EnsureAnimator().SetBool(IsSitting, sitting);
        public void TriggerCatch() { if (!gameOver) EnsureAnimator().SetTrigger(Catch); }
        public void TriggerCelebrate() { if (!gameOver) EnsureAnimator().SetTrigger(Celebrate); }

        /// <summary>Only a false-to-true edge triggers GameOver. A true-to-false edge triggers Recover.</summary>
        public void SetGameOver(bool value)
        {
            var a = EnsureAnimator();
            a.SetBool(IsGameOver, value);
            if (value == gameOver) return;
            gameOver = value;
            if (value)
            {
                a.ResetTrigger(RecoverTrigger);
                a.ResetTrigger(Catch);
                a.ResetTrigger(Celebrate);
                a.SetTrigger(GameOver);
            }
            else
            {
                a.ResetTrigger(GameOver);
                a.SetBool(IsSitting, false);
                a.SetTrigger(RecoverTrigger);
            }
        }

        public void Recover() => SetGameOver(false);

        /// <summary>Receiver for manifest clip events; optionally wire to audio, never to score mutation.</summary>
        public void OnAnimationCue(string cue) => animationCue.Invoke(cue);
    }
}
