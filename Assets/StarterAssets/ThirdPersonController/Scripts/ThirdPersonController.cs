using UnityEngine;
using UnityEngine.AI;

namespace StarterAssets
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 2.0f;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;

        [Header("Random Movement")]
        public bool EnableRandomMovement = true;
        public float RandomMoveRadius = 10.0f;
        public float RandomWaitTime = 2.0f;

        private NavMeshAgent _navMeshAgent;
        private float _waitTimer;
        private bool _hasAnimator;
        private Animator _animator;

        private void Start()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _hasAnimator = TryGetComponent(out _animator);
        }

        private void Update()
        {
            if (EnableRandomMovement)
            {
                HandleRandomMovement();
            }
        }

        private void HandleRandomMovement()
        {
            // Check if the agent has reached its destination
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
            {
                _waitTimer += Time.deltaTime;

                if (_waitTimer >= RandomWaitTime)
                {
                    SetRandomDestination();
                    _waitTimer = 0;
                }

                // Set animator speed to zero when idle
                if (_hasAnimator)
                {
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetFloat("MotionSpeed", 0f);
                }
            }
            else
            {
                // Update animator speed while moving
                if (_hasAnimator)
                {
                    float agentSpeed = _navMeshAgent.velocity.magnitude;
                    _animator.SetFloat("Speed", agentSpeed);
                    _animator.SetFloat("MotionSpeed", 1f);
                }
            }
        }

        private void SetRandomDestination()
        {
            Vector3 randomDirection = Random.insideUnitSphere * RandomMoveRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, RandomMoveRadius, NavMesh.AllAreas))
            {
                _navMeshAgent.SetDestination(hit.position);
            }
        }

        public void OnFootstep(AnimationEvent animationEvent)
        {
            if (FootstepAudioClips.Length > 0)
            {
                int index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.position, FootstepAudioVolume);
            }
        }

        public void OnLand(AnimationEvent animationEvent)
        {
           
        }
    }
}
