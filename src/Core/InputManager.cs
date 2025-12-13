using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TerraNova.Core;

public enum MouseButton
{
    Left,
    Middle,
    Right
}

/// <summary>
/// Handles all input - keyboard, mouse, and gamepad
/// Tracks current and previous states for press/release detection
/// </summary>
public class InputManager
{
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    
    private GamePadState _currentGamePad;
    private GamePadState _previousGamePad;
    
    // Mouse properties
    public Vector2 MousePosition => new(_currentMouse.X, _currentMouse.Y);
    public int MouseScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
    
    // Movement vectors (normalized)
    public Vector2 MovementDirection { get; private set; }
    
    public void Update()
    {
        // Store previous states
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _previousGamePad = _currentGamePad;
        
        // Get current states
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
        _currentGamePad = GamePad.GetState(PlayerIndex.One);
        
        // Calculate movement direction
        CalculateMovement();
    }
    
    /// <summary>
    /// Reset input state - useful when closing menus to prevent stuck input
    /// </summary>
    public void ResetState()
    {
        // Set previous states to current states to prevent "stuck" pressed states
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _previousGamePad = _currentGamePad;
    }
    
    private void CalculateMovement()
    {
        var movement = Vector2.Zero;
        
        // Keyboard
        if (IsKeyDown(Keys.A) || IsKeyDown(Keys.Left))
            movement.X -= 1;
        if (IsKeyDown(Keys.D) || IsKeyDown(Keys.Right))
            movement.X += 1;
        if (IsKeyDown(Keys.W) || IsKeyDown(Keys.Up))
            movement.Y -= 1;
        if (IsKeyDown(Keys.S) || IsKeyDown(Keys.Down))
            movement.Y += 1;
        
        // Gamepad (if connected)
        if (_currentGamePad.IsConnected)
        {
            var stick = _currentGamePad.ThumbSticks.Left;
            if (Math.Abs(stick.X) > 0.2f)
                movement.X = stick.X;
            if (Math.Abs(stick.Y) > 0.2f)
                movement.Y = -stick.Y; // Y is inverted on gamepad
        }
        
        // Normalize if not zero
        if (movement.LengthSquared() > 0)
            movement.Normalize();
        
        MovementDirection = movement;
    }
    
    // Keyboard methods
    public bool IsKeyDown(Keys key) => _currentKeyboard.IsKeyDown(key);
    public bool IsKeyUp(Keys key) => _currentKeyboard.IsKeyUp(key);
    public bool IsKeyPressed(Keys key) => _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    public bool IsKeyReleased(Keys key) => _currentKeyboard.IsKeyUp(key) && _previousKeyboard.IsKeyDown(key);
    
    // Mouse methods
    public bool IsMouseButtonDown(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => _currentMouse.LeftButton == ButtonState.Pressed,
            MouseButton.Middle => _currentMouse.MiddleButton == ButtonState.Pressed,
            MouseButton.Right => _currentMouse.RightButton == ButtonState.Pressed,
            _ => false
        };
    }
    
    public bool IsMouseButtonUp(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => _currentMouse.LeftButton == ButtonState.Released,
            MouseButton.Middle => _currentMouse.MiddleButton == ButtonState.Released,
            MouseButton.Right => _currentMouse.RightButton == ButtonState.Released,
            _ => true
        };
    }
    
    public bool IsMouseButtonPressed(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released,
            MouseButton.Middle => _currentMouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Released,
            MouseButton.Right => _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released,
            _ => false
        };
    }
    
    public bool IsMouseButtonReleased(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => _currentMouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed,
            MouseButton.Middle => _currentMouse.MiddleButton == ButtonState.Released && _previousMouse.MiddleButton == ButtonState.Pressed,
            MouseButton.Right => _currentMouse.RightButton == ButtonState.Released && _previousMouse.RightButton == ButtonState.Pressed,
            _ => false
        };
    }
    
    // Gamepad methods
    public bool IsButtonDown(Buttons button) => _currentGamePad.IsButtonDown(button);
    public bool IsButtonUp(Buttons button) => _currentGamePad.IsButtonUp(button);
    public bool IsButtonPressed(Buttons button) => _currentGamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button);
    public bool IsButtonReleased(Buttons button) => _currentGamePad.IsButtonUp(button) && _previousGamePad.IsButtonDown(button);
    
    // Common game actions
    public bool IsJumpPressed => IsKeyPressed(Keys.Space) || IsKeyPressed(Keys.W) || IsButtonPressed(Buttons.A);
    public bool IsJumpHeld => IsKeyDown(Keys.Space) || IsKeyDown(Keys.W) || IsButtonDown(Buttons.A);
    public bool IsInteractPressed => IsKeyPressed(Keys.E) || IsButtonPressed(Buttons.X);
    public bool IsInventoryPressed => IsKeyPressed(Keys.Tab) || IsKeyPressed(Keys.I) || IsButtonPressed(Buttons.Y);
    
    // Hotbar slot selection (1-9 keys)
    public int? GetHotbarSlotPressed()
    {
        for (int i = 0; i < 9; i++)
        {
            if (IsKeyPressed(Keys.D1 + i))
                return i;
        }
        return null;
    }
}
