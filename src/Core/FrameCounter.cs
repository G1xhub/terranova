namespace TerraNova.Core;

/// <summary>
/// Tracks and calculates frames per second
/// </summary>
public class FrameCounter
{
    private readonly Queue<float> _sampleBuffer = new();
    private const int MaxSamples = 100;
    
    public float CurrentFramesPerSecond { get; private set; }
    public float AverageFramesPerSecond { get; private set; }
    public float MinFramesPerSecond { get; private set; } = float.MaxValue;
    public float MaxFramesPerSecond { get; private set; }
    
    public void Update(float deltaTime)
    {
        if (deltaTime <= 0) return;
        
        CurrentFramesPerSecond = 1f / deltaTime;
        
        _sampleBuffer.Enqueue(CurrentFramesPerSecond);
        
        if (_sampleBuffer.Count > MaxSamples)
        {
            _sampleBuffer.Dequeue();
        }
        
        AverageFramesPerSecond = _sampleBuffer.Average();
        
        if (CurrentFramesPerSecond < MinFramesPerSecond)
            MinFramesPerSecond = CurrentFramesPerSecond;
        if (CurrentFramesPerSecond > MaxFramesPerSecond)
            MaxFramesPerSecond = CurrentFramesPerSecond;
    }
    
    public void Reset()
    {
        _sampleBuffer.Clear();
        MinFramesPerSecond = float.MaxValue;
        MaxFramesPerSecond = 0;
    }
}
