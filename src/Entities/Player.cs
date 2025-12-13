using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraNova.Core;
using TerraNova.World;
using TerraNova.Systems;

namespace TerraNova.Entities;

/// <summary>
/// Player entity with movement, mining, building, and inventory
/// </summary>
public class Player : Entity
{
    // Stats
    public int Mana { get; private set; }
    public int MaxMana { get; private set; }
    
    // Inventory
    public Inventory Inventory { get; }
    public int SelectedSlot { get; set; } = 0;
    
    // Mining
    private float _miningProgress;
    private (int x, int y)? _miningTarget;
    private const float BaseMiningSpeed = 2.0f; // Increased for faster mining
    
    // Movement
    private bool _wasOnGround;
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private const float JumpBufferTime = 0.1f;
    private const float CoyoteTime = 0.1f;
    
    // Animation
    private AnimationSystem _animationSystem;
    private bool _isMoving;
    
    // Effects
    private float _respawnTimer;
    private Vector2 _spawnPoint;
    
    public Player(Vector2 position) : base(position, GameConfig.PlayerWidth, GameConfig.PlayerHeight)
    {
        // Setup collision box (smaller than sprite)
        CollisionOffsetX = 3;
        CollisionOffsetY = 2;
        CollisionWidth = 14;
        CollisionHeight = 40;
        
        // Initialize stats
        MaxHealth = TerraNovaGame.Config.MaxHealth;
        Health = MaxHealth;
        MaxMana = TerraNovaGame.Config.MaxMana;
        Mana = MaxMana;
        
        // Initialize inventory with starter items
        Inventory = new Inventory(40); // 40 slots
        Inventory.AddItem(new Item(ItemType.CopperPickaxe, 1));
        Inventory.AddItem(new Item(ItemType.CopperAxe, 1));
        Inventory.AddItem(new Item(ItemType.CopperSword, 1));
        Inventory.AddItem(new Item(ItemType.Torch, 50));
        Inventory.AddItem(new Item(ItemType.Wood, 100));
        
        // Initialize animation system
        _animationSystem = new AnimationSystem();
        InitializeAnimations();
        
        _spawnPoint = position;
    }
    
    private void InitializeAnimations()
    {
        const int frameWidth = 20;
        const int frameHeight = 42;
        
        // Idle animation (3 frames, row 0)
        var idleFrames = new Rectangle[3];
        for (int i = 0; i < 3; i++)
        {
            idleFrames[i] = new Rectangle(i * frameWidth, 0 * frameHeight, frameWidth, frameHeight);
        }
        _animationSystem.AddAnimation(AnimationState.Idle, new Animation(idleFrames, 0.15f, true));
        
        // Walk animation (6 frames, row 1)
        var walkFrames = new Rectangle[6];
        for (int i = 0; i < 6; i++)
        {
            walkFrames[i] = new Rectangle(i * frameWidth, 1 * frameHeight, frameWidth, frameHeight);
        }
        _animationSystem.AddAnimation(AnimationState.Walk, new Animation(walkFrames, 0.1f, true));
        
        // Run animation (6 frames, row 2)
        var runFrames = new Rectangle[6];
        for (int i = 0; i < 6; i++)
        {
            runFrames[i] = new Rectangle(i * frameWidth, 2 * frameHeight, frameWidth, frameHeight);
        }
        _animationSystem.AddAnimation(AnimationState.Run, new Animation(runFrames, 0.08f, true));
        
        // Jump animation (2 frames, row 3)
        var jumpFrames = new Rectangle[2];
        for (int i = 0; i < 2; i++)
        {
            jumpFrames[i] = new Rectangle(i * frameWidth, 3 * frameHeight, frameWidth, frameHeight);
        }
        _animationSystem.AddAnimation(AnimationState.Jump, new Animation(jumpFrames, 0.2f, false));
        
        // Fall animation (1 frame, row 4)
        var fallFrames = new Rectangle[1];
        fallFrames[0] = new Rectangle(0, 4 * frameHeight, frameWidth, frameHeight);
        _animationSystem.AddAnimation(AnimationState.Fall, new Animation(fallFrames, 0.2f, true));
        
        // Mining animation (3 frames, row 5)
        var miningFrames = new Rectangle[3];
        for (int i = 0; i < 3; i++)
        {
            miningFrames[i] = new Rectangle(i * frameWidth, 5 * frameHeight, frameWidth, frameHeight);
        }
        _animationSystem.AddAnimation(AnimationState.Mining, new Animation(miningFrames, 0.12f, true));
    }
    
    public void Update(GameTime gameTime, InputManager input, GameWorld world, ParticleSystem particles)
    {
        if (!IsActive) return;
        
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Handle respawn
        if (IsDead)
        {
            _respawnTimer -= deltaTime;
            if (_respawnTimer <= 0)
            {
                Respawn();
            }
            return;
        }
        
        // Store previous ground state for coyote time
        _wasOnGround = IsOnGround;
        
        // Handle input
        HandleMovement(input, deltaTime);
        HandleJump(input, deltaTime);
        HandleHotbarSelection(input);
        
        // Update base (physics, collision)
        base.Update(gameTime, world);
        
        // Update coyote time
        if (_wasOnGround && !IsOnGround)
        {
            _coyoteTimer = CoyoteTime;
        }
        else if (IsOnGround)
        {
            _coyoteTimer = 0;
        }
        else
        {
            _coyoteTimer -= deltaTime;
        }
        
        // Update animation
        UpdateAnimation(deltaTime);
        _animationSystem.Update(deltaTime);
    }
    
    private void HandleMovement(InputManager input, float deltaTime)
    {
        float speed = TerraNovaGame.Config.PlayerSpeed;
        
        // Water slows movement
        if (IsInWater)
            speed *= 0.6f;
        
        _isMoving = false;
        
        float targetVelX = 0;
        
        if (input.IsKeyDown(Keys.A) || input.IsKeyDown(Keys.Left))
        {
            targetVelX = -speed;
            FacingRight = false;
            _isMoving = true;
        }
        if (input.IsKeyDown(Keys.D) || input.IsKeyDown(Keys.Right))
        {
            targetVelX = speed;
            FacingRight = true;
            _isMoving = true;
        }
        
        // Smooth acceleration
        float accel = IsOnGround ? 0.3f : 0.15f;
        Velocity = new Vector2(
            MathHelper.Lerp(Velocity.X, targetVelX, accel),
            Velocity.Y
        );
    }
    
    private void HandleJump(InputManager input, float deltaTime)
    {
        // Jump buffer
        if (input.IsJumpPressed)
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= deltaTime;
        }
        
        // Can jump if on ground or within coyote time
        bool canJump = IsOnGround || _coyoteTimer > 0;
        
        // Jump if buffered and can jump
        if (_jumpBufferTimer > 0 && canJump)
        {
            float jumpForce = TerraNovaGame.Config.JumpForce;
            
            // Reduced jump in water
            if (IsInWater)
                jumpForce *= 0.7f;
            
            Velocity = new Vector2(Velocity.X, -jumpForce);
            IsOnGround = false;
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
        }
        
        // Variable jump height - release to reduce upward velocity
        if (!input.IsJumpHeld && Velocity.Y < 0)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y * 0.5f);
        }
        
        // Swimming - hold jump to swim up
        if (IsInWater && input.IsJumpHeld)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y - 0.5f);
        }
    }
    
    private void HandleHotbarSelection(InputManager input)
    {
        var slot = input.GetHotbarSlotPressed();
        if (slot.HasValue)
        {
            SelectedSlot = slot.Value;
        }
        
        // Mouse wheel scroll
        int scroll = input.MouseScrollDelta;
        if (scroll != 0)
        {
            SelectedSlot = (SelectedSlot - Math.Sign(scroll) + 9) % 9;
        }
    }
    
    public void Mine(GameWorld world, int tileX, int tileY, ParticleSystem particles)
    {
        var tile = world.GetTile(tileX, tileY);
        
        // #region agent log
        TerraNovaGame.AgentLog("Player.Mine", "entry", new {
            tileX,
            tileY,
            tileType = tile.ToString(),
            tileIsAir = tile == TileType.Air,
            tileIsWater = tile == TileType.Water
        }, "H2-mining-entry");
        // #endregion
        
        if (tile == TileType.Air || tile == TileType.Water)
        {
            ResetMining();
            return;
        }
        
        var tileData = TileProperties.Get(tile);
        if (tileData.Hardness >= float.MaxValue)
        {
            ResetMining();
            return;
        }
        
        // Check if target changed
        if (_miningTarget == null || _miningTarget.Value.x != tileX || _miningTarget.Value.y != tileY)
        {
            _miningProgress = 0;
            _miningTarget = (tileX, tileY);
        }
        
        // Get mining power from equipped tool
        float miningPower = GetMiningPower();
        float hardness = tileData.Hardness;
        
        // Progress mining - use deltaTime instead of fixed 1/60
        float deltaTime = 1f / 60f; // Approximate, should be passed from Update
        
        // Calculate mining progress - ensure it works even with low mining power
        float progressPerSecond = (miningPower / hardness) * BaseMiningSpeed;
        _miningProgress += progressPerSecond * deltaTime;
        
        // #region agent log
        TerraNovaGame.AgentLog("Player.Mine", "mining-calculation", new {
            miningPower,
            hardness,
            progressPerSecond,
            deltaTime,
            progressBefore = _miningProgress - (progressPerSecond * deltaTime),
            progressAfter = _miningProgress,
            baseMiningSpeed = BaseMiningSpeed
        }, "H4-mining-calculation");
        // #endregion
        
        // Spawn mining particles based on progress
        var worldPos = new Vector2(
            tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
            tileY * GameConfig.TileSize + GameConfig.TileSize / 2
        );
        particles.SpawnMiningParticles(worldPos, tile, _miningProgress);
        
        // #region agent log
        TerraNovaGame.AgentLog("Player.Mine", "progress", new {
            miningPower,
            hardness,
            progress = _miningProgress,
            progressPercent = _miningProgress * 100
        }, "H3-mining-progress");
        // #endregion
        
        // Spawn particles while mining
        if (Random.Shared.NextDouble() < 0.3)
        {
            var tileCenter = new Vector2(
                tileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
                tileY * GameConfig.TileSize + GameConfig.TileSize / 2f
            );
            particles.SpawnTileBreakParticle(tileCenter, tile);
        }
        
        // Complete mining
        if (_miningProgress >= 1f)
        {
            // Get drop
            var drop = TileProperties.GetDrop(tile);
            if (drop != TileType.Air)
            {
                // Convert tile type to item type
                var itemType = TileToItem(drop);
                if (itemType != ItemType.None)
                {
                    Inventory.AddItem(new Item(itemType, 1));
                }
            }
            
            // Spawn break particles
            var center = new Vector2(
                tileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
                tileY * GameConfig.TileSize + GameConfig.TileSize / 2f
            );
            particles.SpawnTileBreakBurst(center, tile, 15);
            
            // Remove tile
            world.SetTile(tileX, tileY, TileType.Air);
            
            ResetMining();
        }
    }
    
    public void ResetMining()
    {
        _miningProgress = 0;
        _miningTarget = null;
    }
    
    public (int x, int y, float progress)? GetMiningInfo()
    {
        if (_miningTarget == null) return null;
        return (_miningTarget.Value.x, _miningTarget.Value.y, _miningProgress);
    }
    
    public void PlaceBlock(GameWorld world, int tileX, int tileY)
    {
        var selectedItem = Inventory.GetItem(SelectedSlot);
        if (selectedItem == null || selectedItem.Count <= 0) return;
        
        // Check if item is placeable
        var tileType = ItemToTile(selectedItem.Type);
        if (tileType == TileType.Air) return;
        
        // Check if tile is empty
        if (world.GetTile(tileX, tileY) != TileType.Air) return;
        
        // Don't place inside player
        var tileBounds = new Rectangle(
            tileX * GameConfig.TileSize,
            tileY * GameConfig.TileSize,
            GameConfig.TileSize,
            GameConfig.TileSize
        );
        if (CollisionBounds.Intersects(tileBounds)) return;
        
        // Place tile
        world.SetTile(tileX, tileY, tileType);
        
        // Consume item
        Inventory.RemoveItem(SelectedSlot, 1);
    }
    
    public float GetMiningPower()
    {
        var item = Inventory.GetItem(SelectedSlot);
        if (item == null) return 1f; // Base mining power without tool
        
        // Tool mining power
        return item.Type switch
        {
            ItemType.CopperPickaxe => 2f,
            ItemType.IronPickaxe => 3f,
            ItemType.GoldPickaxe => 4f,
            ItemType.DiamondPickaxe => 6f,
            ItemType.CopperAxe => 1.5f,
            ItemType.IronAxe => 2.5f,
            _ => 1f
        };
    }
    
    private static ItemType TileToItem(TileType tile)
    {
        return tile switch
        {
            TileType.Dirt => ItemType.Dirt,
            TileType.Grass => ItemType.Dirt, // Grass drops dirt
            TileType.Stone => ItemType.Stone,
            TileType.Sand => ItemType.Sand,
            TileType.Snow => ItemType.Snow,
            TileType.Wood => ItemType.Wood,
            TileType.Leaves => ItemType.None, // Leaves don't drop items
            TileType.CopperOre => ItemType.CopperOre,
            TileType.IronOre => ItemType.IronOre,
            TileType.GoldOre => ItemType.GoldOre,
            TileType.DiamondOre => ItemType.Diamond,
            TileType.Coal => ItemType.Coal,
            TileType.Mud => ItemType.Mud,
            TileType.JungleGrass => ItemType.Mud, // Jungle grass drops mud
            TileType.Torch => ItemType.Torch,
            TileType.Chest => ItemType.Chest,
            TileType.CraftingTable => ItemType.CraftingTable,
            TileType.Furnace => ItemType.Furnace,
            TileType.Anvil => ItemType.Anvil,
            TileType.Crystal => ItemType.Crystal,
            TileType.BlueCrystal => ItemType.BlueCrystal,
            TileType.RedCrystal => ItemType.RedCrystal,
            TileType.Mushroom => ItemType.Mushroom,
            TileType.GlowingMushroom => ItemType.GlowingMushroom,
            TileType.WoodPlatform => ItemType.WoodPlatform,
            _ => ItemType.None
        };
    }
    
    private static TileType ItemToTile(ItemType item)
    {
        return item switch
        {
            ItemType.Dirt => TileType.Dirt,
            ItemType.Stone => TileType.Stone,
            ItemType.Sand => TileType.Sand,
            ItemType.Snow => TileType.Snow,
            ItemType.Wood => TileType.Wood,
            ItemType.Mud => TileType.Mud,
            ItemType.CopperOre => TileType.CopperOre,
            ItemType.IronOre => TileType.IronOre,
            ItemType.GoldOre => TileType.GoldOre,
            ItemType.Coal => TileType.Coal,
            ItemType.Torch => TileType.Torch,
            ItemType.Chest => TileType.Chest,
            ItemType.CraftingTable => TileType.CraftingTable,
            ItemType.Furnace => TileType.Furnace,
            ItemType.Anvil => TileType.Anvil,
            ItemType.WoodPlatform => TileType.WoodPlatform,
            _ => TileType.Air
        };
    }
    
    private void UpdateAnimation(float deltaTime)
    {
        // Determine animation state based on player state
        AnimationState newState;
        
        if (_miningTarget.HasValue)
        {
            newState = AnimationState.Mining;
        }
        else if (!IsOnGround)
        {
            newState = Velocity.Y < 0 ? AnimationState.Jump : AnimationState.Fall;
        }
        else if (_isMoving)
        {
            // Determine if running or walking based on speed
            float speed = Math.Abs(Velocity.X);
            newState = speed > 3.0f ? AnimationState.Run : AnimationState.Walk;
        }
        else
        {
            newState = AnimationState.Idle;
        }
        
        // Update facing direction
        _animationSystem.FacingRight = FacingRight;
        
        // Set animation state
        _animationSystem.SetState(newState);
    }
    
    protected override void OnDeath()
    {
        _respawnTimer = 3f; // 3 second respawn
        // Drop some items on death (optional)
    }
    
    private void Respawn()
    {
        Position = _spawnPoint;
        Velocity = Vector2.Zero;
        Health = MaxHealth / 2; // Respawn with half health
        Mana = MaxMana;
        IsActive = true;
        InvulnerabilityTimer = 2f; // 2 seconds of invulnerability after respawn
    }
    
    public void SetSpawnPoint(Vector2 position)
    {
        _spawnPoint = position;
    }
    
    public override void Draw(SpriteBatch spriteBatch)
    {
        // #region agent log
        TerraNovaGame.AgentLog("Player.Draw", "entry", new { IsActive, IsDead, IsInvulnerable, PositionX = Position.X, PositionY = Position.Y, Width, Height }, "H1-player-inactive");
        // #endregion
        
        if (!IsActive && !IsDead) return;
        
        // Flicker when invulnerable
        if (IsInvulnerable && (int)(InvulnerabilityTimer * 10) % 2 == 0)
            return;
        
        var destRect = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
        
        // #region agent log
        TerraNovaGame.AgentLog("Player.Draw", "drawing", new { destRectX = destRect.X, destRectY = destRect.Y, destRectWidth = destRect.Width, destRectHeight = destRect.Height, PlayerSpriteNull = TextureManager.PlayerSprite == null }, "H2-sprite-null");
        // #endregion
        
        // Get current animation frame
        var sourceRect = _animationSystem.GetCurrentFrame();
        
        // Flip sprite if facing left
        var effects = _animationSystem.FacingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        
        spriteBatch.Draw(
            TextureManager.PlayerSpriteSheet,
            destRect,
            sourceRect,
            Color.White,
            0f,
            Vector2.Zero,
            effects,
            0f
        );
        
        // Draw mining progress
        var miningInfo = GetMiningInfo();
        if (miningInfo.HasValue)
        {
            int mx = miningInfo.Value.x * GameConfig.TileSize;
            int my = miningInfo.Value.y * GameConfig.TileSize;
            float progress = miningInfo.Value.progress;
            
            // Crack overlay
            var crackRect = new Rectangle(mx, my, GameConfig.TileSize, GameConfig.TileSize);
            spriteBatch.Draw(TextureManager.Pixel, crackRect, Color.Black * (progress * 0.5f));
            
            // Progress bar
            var barBg = new Rectangle(mx, my - 6, GameConfig.TileSize, 4);
            var barFg = new Rectangle(mx, my - 6, (int)(GameConfig.TileSize * progress), 4);
            spriteBatch.Draw(TextureManager.Pixel, barBg, Color.Gray);
            spriteBatch.Draw(TextureManager.Pixel, barFg, Color.Red);
        }
    }
}
