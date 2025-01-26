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

        // --- NUOVE VARIABILI PER EVITARE STALLO ---
        [Header("Anti-Stallo Settings")]
        [Tooltip("Tempo (in secondi) dopo il quale, se il pedone è praticamente fermo, deve forzare un movimento a grande distanza.")]
        public float StuckTimeThreshold = 5f;
        [Tooltip("Raggio di spostamento molto ampio da usare quando si è bloccati.")]
        public float BigMoveRadius = 30f;
        // -----------------------------------------

        private NavMeshAgent _navMeshAgent;
        private float _waitTimer;
        private bool _hasAnimator;
        private Animator _animator;

        // --- PER TRACCIARE SE IL PEDONE È FERMO ---
        private float _timeSinceMove = 0f;
        private Vector3 _lastPosition;
        private float _minMoveThreshold = 0.01f; // se ci muoviamo meno di questa soglia, consideriamo il pedone "fermo"
        // ------------------------------------------

        private void Start()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _hasAnimator = TryGetComponent(out _animator);

            // Inizializza lastPosition alla posizione di partenza
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (EnableRandomMovement)
            {
                HandleRandomMovement();
            }

            CheckIfStuck();
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

        /// <summary>
        /// Imposta una destinazione casuale usando il raggio RandomMoveRadius.
        /// </summary>
        private void SetRandomDestination()
        {
            Vector3 randomDirection = Random.insideUnitSphere * RandomMoveRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, RandomMoveRadius, NavMesh.AllAreas))
            {
                _navMeshAgent.SetDestination(hit.position);
            }
        }

        /// <summary>
        /// Controlla se il pedone è fermo da troppo tempo e, in tal caso, lo sposta in modo massiccio.
        /// </summary>
        private void CheckIfStuck()
        {
            // Quanto si è mosso rispetto all'ultimo frame
            float distMoved = Vector3.Distance(transform.position, _lastPosition);

            if (distMoved < _minMoveThreshold)
            {
                _timeSinceMove += Time.deltaTime;

                // Se supera la soglia di tempo "StuckTimeThreshold", forza un grande spostamento
                if (_timeSinceMove >= StuckTimeThreshold)
                {
                    ForceBigMove();
                    _timeSinceMove = 0f;
                }
            }
            else
            {
                // Se ci siamo mossi abbastanza, resettiamo il timer e aggiorniamo la posizione
                _timeSinceMove = 0f;
                _lastPosition = transform.position;
            }
        }

        /// <summary>
        /// Forza lo spostamento su una destinazione molto lontana, per sbloccare eventuali stalli.
        /// </summary>
        private void ForceBigMove()
        {
            Vector3 randomDirection = Random.insideUnitSphere * BigMoveRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, BigMoveRadius, NavMesh.AllAreas))
            {
                _navMeshAgent.SetDestination(hit.position);
                Debug.Log($"{name} era fermo. Forzato spostamento lontano a {hit.position}");
            }
        }

        // -------------------------
        // GESTIONE FOOTSTEP AUDIO
        // -------------------------
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
            // Questo evento atterra qui, se lo usi nelle animazioni
        }
    }
}
