using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TerraNova.Systems;

/// <summary>
/// Animation state for character animations
/// </summary>
public enum AnimationState
{
    Idle,
    Walk,
    Run,
    Jump,
    Fall,
    Mining,
    Attack
}

/// <summary>
/// Represents a single animation with multiple frames
/// </summary>
public class Animation
{
    public Rectangle[] Frames { get; }
    public float FrameTime { get; } // Time per frame in seconds
    public bool Loop { get; }
    public int FrameCount => Frames.Length;
    
    public Animation(Rectangle[] frames, float frameTime, bool loop = true)
    {
        Frames = frames;
        FrameTime = frameTime;
        Loop = loop;
    }
}

/// <summary>
/// Animation system for sprite-sheet based animations
/// </summary>
public class AnimationSystem
{
    private readonly Dictionary<AnimationState, Animation> _animations;
    private AnimationState _currentState;
    private float _currentFrameTime;
    private int _currentFrameIndex;
    private bool _facingRight;
    
    public AnimationState CurrentState => _currentState;
    public bool FacingRight
    {
        get => _facingRight;
        set => _facingRight = value;
    }
    
    public AnimationSystem()
    {
        _animations = new Dictionary<AnimationState, Animation>();
        _currentState = AnimationState.Idle;
        _currentFrameTime = 0f;
        _currentFrameIndex = 0;
        _facingRight = true;
    }
    
    /// <summary>
    /// Add an animation to the system
    /// </summary>
    public void AddAnimation(AnimationState state, Animation animation)
    {
        _animations[state] = animation;
    }
    
    /// <summary>
    /// Set the current animation state
    /// </summary>
    public void SetState(AnimationState newState)
    {
        if (newState != _currentState)
        {
            _currentState = newState;
            _currentFrameIndex = 0;
            _currentFrameTime = 0f;
        }
    }
    
    /// <summary>
    /// Update animation based on elapsed time
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_animations.TryGetValue(_currentState, out var animation))
            return;
        
        _currentFrameTime += deltaTime;
        
        if (_currentFrameTime >= animation.FrameTime)
        {
            _currentFrameTime = 0f;
            _currentFrameIndex++;
            
            if (_currentFrameIndex >= animation.FrameCount)
            {
                if (animation.Loop)
                {
                    _currentFrameIndex = 0;
                }
                else
                {
                    _currentFrameIndex = animation.FrameCount - 1;
                }
            }
        }
    }
    
    /// <summary>
    /// Get the current frame rectangle for rendering
    /// </summary>
    public Rectangle GetCurrentFrame()
    {
        if (!_animations.TryGetValue(_currentState, out var animation))
        {
            // Return default frame if animation not found
            return new Rectangle(0, 0, 20, 42);
        }
        
        if (_currentFrameIndex >= 0 && _currentFrameIndex < animation.FrameCount)
        {
            return animation.Frames[_currentFrameIndex];
        }
        
        return animation.Frames[0];
    }
    
    /// <summary>
    /// Reset animation to first frame
    /// </summary>
    public void Reset()
    {
        _currentFrameIndex = 0;
        _currentFrameTime = 0f;
    }
}

