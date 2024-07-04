using System;
using _Script.Input;
using FishNet.Object;
using UnityEngine;

namespace _Script.Player
{
    public sealed class Player : NetworkBehaviour
    {
        [Header("References")] 
        public PlayerMovementStats MoveStats;
        [SerializeField] private Collider2D _feetColl;
        [SerializeField] private Collider2D _bodyColl;
        
        private Rigidbody2D _rigidbody2D;
        private Animator _animator;
        
        // Movement vars
        private Vector2 _moveVelocity;
        private bool _isFacingRight;
        
        // Collision check vars
        private RaycastHit2D _groundHit;
        private RaycastHit2D _headHit;
        private bool _isGrounded;
        private bool _bumpHead;
        
        // Jump vars
        public float VerticalVelocity { get; private set; }
        private bool _isJumping;
        private bool _isFastFalling;
        private bool _isFalling;
        private float _fastFallTime;
        private float _fastFallReleaseSpeed;
        private int _numberOrJumpsUsed;
        
        // apex vars
        private float _apexPoint;
        private float _timePastApexThreshold;
        private bool _isPastApexThreshold;
        
        // jump buffer vars
        private float _jumpBufferTimer;
        private bool _jumpReleasedDuringBuffer;
        
        // coyote time vars
        private float _coyoteTimer;
        
        // Animation hash vars
        private static readonly int PlAYER_IDLE = Animator.StringToHash("Player_Idle");
        private static readonly int PLAYER_RUN = Animator.StringToHash("Player_Run");

        private void Awake()
        {
            _isFacingRight = true;
            
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (!base.IsOwner) return;
            
            CountTimers();
            JumpChecks();
        }

        private void FixedUpdate()
        {
            if (!base.IsOwner) return;
            
            CollisionChecks();
            Jump();
            
            if (_isGrounded)
                Move(MoveStats.GroundAcceleration, MoveStats.GroundDeceleration, InputManager.Movement);
            else 
                Move(MoveStats.AirAcceleration, MoveStats.AirDeceleration, InputManager.Movement);
        }
        
        private void Jump()
        {
            // APPLY GRAVITY WHILE JUMPING
            if (_isJumping)
            {
                // CHECK FOR HEAD BUMP
                if (_bumpHead)
                    _isFastFalling = true;
                
                // GRAVITY ON ASCENDING
                if (VerticalVelocity >= 0f)
                {
                    // APEX CONTROLS
                    _apexPoint = Mathf.InverseLerp(MoveStats.InitialJumpVelocity, 0f, VerticalVelocity);

                    if (_apexPoint > MoveStats.ApexThreshold)
                    {
                        if (!_isPastApexThreshold)
                        {
                            _isPastApexThreshold = true;
                            _timePastApexThreshold = 0f;
                        }

                        if (_isPastApexThreshold)
                        {
                            _timePastApexThreshold += Time.fixedDeltaTime;
                            if (_timePastApexThreshold < MoveStats.ApexHangTime)
                                VerticalVelocity = 0f;
                            else 
                                VerticalVelocity = -0.01f;
                        }
                    }
                    // GRAVITY ON ASCENDING BUT NOT PAST APEX THRESHOLD
                    else
                    {
                        VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
                        if (_isPastApexThreshold)
                            _isPastApexThreshold = false;
                    }
                }
                
                // GRAVITY ON DESCENDING
                else if (!_isFastFalling)
                    VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
                else if (VerticalVelocity < 0f)
                    if (!_isFalling)
                       _isFalling = true;
            }
            
            // JUMP CUT
            if (_isFastFalling)
            {
                if (_fastFallTime >= MoveStats.TimeForUpwardsCancel)
                    VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
                else if (_fastFallTime < MoveStats.TimeForUpwardsCancel)
                {
                    VerticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / MoveStats.TimeForUpwardsCancel));
                }
                
                _fastFallTime += Time.fixedDeltaTime;
            }
            
            // NORMAL GRAVITY WHILE FALLING
            if (!_isGrounded && !_isJumping)
            {
                if (!_isFalling)
                    _isFalling = true;
                
                VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
            }
            
            // CLAMP FALL SPEED
            VerticalVelocity = Mathf.Clamp(VerticalVelocity, -MoveStats.MaxFallSpeed, 50f);
            _rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, VerticalVelocity);
        }

        private void Move(float acceleration, float deceleration, Vector2 moveInput)
        {
            if (moveInput != Vector2.zero)
            {
                TurnCheck(moveInput);
                
                var targetVelocity = Vector2.zero;
                if (InputManager.RunIsHeld)
                {
                    targetVelocity = new Vector2(moveInput.x, 0f) * MoveStats.MaxRunSpeed;
                    _animator.Play(PLAYER_RUN);
                }
                else
                {
                    targetVelocity = new Vector2(moveInput.x, 0f) * MoveStats.MaxWalkSpeed;
                    _animator.Play(PLAYER_RUN);
                }
                
                _moveVelocity = Vector2.Lerp(_moveVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
                _rigidbody2D.velocity = new Vector2(_moveVelocity.x, _rigidbody2D.velocity.y);
            } else if (moveInput == Vector2.zero)
            {
                _moveVelocity = Vector2.Lerp(_moveVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
                _rigidbody2D.velocity = new Vector2(_moveVelocity.x, _rigidbody2D.velocity.y);
                
                if (Mathf.Abs(_rigidbody2D.velocity.x) <= MoveStats.IdleThreshold)
                    _animator.Play(PlAYER_IDLE);
            }
        }

        private void TurnCheck(Vector2 moveInput)
        {
            switch (_isFacingRight)
            {
                case true when moveInput.x < 0:
                    Turn(false);
                    break;
                case false when moveInput.x > 0:
                    Turn(true);
                    break;
            }
        }

        private void Turn(bool turnRight)
        {
            if (turnRight)
            {
                _isFacingRight = true;
                transform.Rotate(0, 180f, 0);
            }
            else
            {
                _isFacingRight = false;
                transform.Rotate(0, -180f, 0);
            }
        }

        private void JumpChecks()
        {
            // WHEN WE PRESS THE JUMP BUTTON
            if (InputManager.JumpWasPressed)
            {
                _jumpBufferTimer = MoveStats.JumpBufferTime;
                _jumpReleasedDuringBuffer = false;
            }
            
            // WHEN WE RELEASE THE JUMP BUTTON
            if (InputManager.JumpWasReleased)
            {
                if (_jumpBufferTimer > 0f)
                {
                    _jumpReleasedDuringBuffer = true;
                }
                
                if (_isJumping && VerticalVelocity > 0f)
                {
                    if (_isPastApexThreshold)
                    {
                        _isPastApexThreshold = false;
                        _isFastFalling = true;
                        _fastFallTime = MoveStats.TimeForUpwardsCancel;
                        VerticalVelocity = 0f;
                    }
                    else
                    {
                        _isFastFalling = true;
                        _fastFallReleaseSpeed = VerticalVelocity;
                    }
                }
            }
            
            // INITIATE JUMP WITHH JUMP BUFFERING AND COYOTE TIME
            if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f))
            {
                InitiateJump(1);
                
                if (_jumpReleasedDuringBuffer)
                {
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = VerticalVelocity;
                }
            }
            
            // DOUBLE JUMP
            else if (_jumpBufferTimer > 0f && _isJumping && _numberOrJumpsUsed < MoveStats.NumberOfJumpsAllowed)
            {
                _isFastFalling = false;
                InitiateJump(1);
            }
            
            // HANDLE AIR JUMP AFTER THE COYOTE TIME HAS LAPSED (TAKE OFF AN EXTRA JUMP SO WE DON'T GET A BONUS JUMP)
            else if (_jumpBufferTimer > 0f && _isFalling && _numberOrJumpsUsed < MoveStats.NumberOfJumpsAllowed - 1)
            {
                InitiateJump(2);
                _isFastFalling = false;
            }
            
            // LANDED
            if ((_isJumping || _isFalling) && _isGrounded && VerticalVelocity <= 0f)
            {
                _isJumping = false;
                _isFalling = false;
                _isFastFalling = false;
                _fastFallTime = 0f;
                _isPastApexThreshold = false;
                _numberOrJumpsUsed = 0;
                VerticalVelocity = Physics2D.gravity.y;
            }
        }

        private void InitiateJump(int numberOfJumpsUsed)
        {
            if (!_isJumping)
                _isJumping = true;
            
            _jumpBufferTimer = 0f;
            _numberOrJumpsUsed += numberOfJumpsUsed;
            VerticalVelocity = MoveStats.InitialJumpVelocity;
        }

        private void CountTimers()
        {
            _jumpBufferTimer -= Time.deltaTime;
            
            if (!_isGrounded)
                _coyoteTimer -= Time.deltaTime;
            else 
                _coyoteTimer = MoveStats.JumpCoyoteTime;
        }
        
        private void CollisionChecks()
        {
            IsGrounded();
            BumpedHead();
        }

        private void BumpedHead()
        {
            var boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _bodyColl.bounds.max.y);
            var boxCastSize = new Vector2(_feetColl.bounds.size.x * MoveStats.HeadWidth, MoveStats.HeadDectectionRayLength);
            
            _headHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.up, MoveStats.HeadDectectionRayLength, MoveStats.GroundLayer);
            _bumpHead = _headHit.collider != null;

            #region Debug visualization

            if (MoveStats.DebugShowHeadBumpBox)
            {
                var headWidth = MoveStats.HeadWidth;
                
                Color rayColor;
                if (_bumpHead)
                    rayColor = Color.green;
                else
                    rayColor = Color.red;
                
                Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2 * headWidth, boxCastOrigin.y), Vector2.up * MoveStats.HeadDectectionRayLength, rayColor);
                Debug.DrawRay(new Vector2(boxCastOrigin.x + (boxCastSize.x / 2) * headWidth, boxCastOrigin.y), Vector2.up * MoveStats.HeadDectectionRayLength, rayColor);
                Debug.DrawRay(
                    new Vector2(boxCastOrigin.x - boxCastSize.x / 2 * headWidth,
                        boxCastOrigin.y + MoveStats.HeadDectectionRayLength), Vector2.right * boxCastSize.x * headWidth,
                    rayColor);
            }
            
            #endregion
        }

        private void IsGrounded()
        {
            var boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _feetColl.bounds.min.y);
            var boxCastSize = new Vector2(_feetColl.bounds.size.x, MoveStats.GroundDetectionRayLength);
            
            _groundHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.down, MoveStats.GroundDetectionRayLength, MoveStats.GroundLayer);
            _isGrounded = _groundHit.collider != null;

            #region Debug Visualization

            if (MoveStats.DebugShowIsGroundedBox)
            {
                Color rayColor;
                if (_isGrounded)
                    rayColor = Color.green;
                else
                    rayColor = Color.red;
                
                Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2, boxCastOrigin.y), Vector2.down * MoveStats.GroundDetectionRayLength, rayColor);
                Debug.DrawRay(new Vector2(boxCastOrigin.x + boxCastSize.x / 2, boxCastOrigin.y), Vector2.down * MoveStats.GroundDetectionRayLength, rayColor);
                Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2, boxCastOrigin.y - MoveStats.GroundDetectionRayLength), Vector2.right * boxCastSize.x, rayColor);
            }
            
            #endregion
        }
    }
}
