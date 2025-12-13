namespace TerraNova.World;

/// <summary>
/// Simplex noise implementation for procedural generation
/// Based on Stefan Gustavson's implementation
/// </summary>
public class SimplexNoise
{
    private readonly byte[] _perm = new byte[512];
    private readonly byte[] _permMod12 = new byte[512];
    
    private static readonly int[][] Grad3 = {
        new[] {1,1,0}, new[] {-1,1,0}, new[] {1,-1,0}, new[] {-1,-1,0},
        new[] {1,0,1}, new[] {-1,0,1}, new[] {1,0,-1}, new[] {-1,0,-1},
        new[] {0,1,1}, new[] {0,-1,1}, new[] {0,1,-1}, new[] {0,-1,-1}
    };
    
    private const double F2 = 0.5 * (1.7320508075688772 - 1.0); // (sqrt(3) - 1) / 2
    private const double G2 = (3.0 - 1.7320508075688772) / 6.0; // (3 - sqrt(3)) / 6
    
    private const double F3 = 1.0 / 3.0;
    private const double G3 = 1.0 / 6.0;
    
    public SimplexNoise(int seed)
    {
        var p = new byte[256];
        
        // Initialize with seed
        var random = new Random(seed);
        for (int i = 0; i < 256; i++)
            p[i] = (byte)i;
        
        // Shuffle using Fisher-Yates
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        
        // Duplicate for overflow handling
        for (int i = 0; i < 512; i++)
        {
            _perm[i] = p[i & 255];
            _permMod12[i] = (byte)(_perm[i] % 12);
        }
    }
    
    /// <summary>
    /// 2D Simplex noise
    /// </summary>
    public double Noise2D(double x, double y)
    {
        double n0, n1, n2;
        
        // Skew input space
        double s = (x + y) * F2;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);
        
        // Unskew
        double t = (i + j) * G2;
        double X0 = i - t;
        double Y0 = j - t;
        double x0 = x - X0;
        double y0 = y - Y0;
        
        // Determine simplex
        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }
        
        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1.0 + 2.0 * G2;
        double y2 = y0 - 1.0 + 2.0 * G2;
        
        // Hash coordinates
        int ii = i & 255;
        int jj = j & 255;
        int gi0 = _permMod12[ii + _perm[jj]];
        int gi1 = _permMod12[ii + i1 + _perm[jj + j1]];
        int gi2 = _permMod12[ii + 1 + _perm[jj + 1]];
        
        // Calculate contributions
        double t0 = 0.5 - x0 * x0 - y0 * y0;
        if (t0 < 0) n0 = 0.0;
        else
        {
            t0 *= t0;
            n0 = t0 * t0 * Dot(Grad3[gi0], x0, y0);
        }
        
        double t1 = 0.5 - x1 * x1 - y1 * y1;
        if (t1 < 0) n1 = 0.0;
        else
        {
            t1 *= t1;
            n1 = t1 * t1 * Dot(Grad3[gi1], x1, y1);
        }
        
        double t2 = 0.5 - x2 * x2 - y2 * y2;
        if (t2 < 0) n2 = 0.0;
        else
        {
            t2 *= t2;
            n2 = t2 * t2 * Dot(Grad3[gi2], x2, y2);
        }
        
        // Scale to [-1, 1]
        return 70.0 * (n0 + n1 + n2);
    }
    
    /// <summary>
    /// 3D Simplex noise
    /// </summary>
    public double Noise3D(double x, double y, double z)
    {
        double n0, n1, n2, n3;
        
        // Skew
        double s = (x + y + z) * F3;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);
        int k = FastFloor(z + s);
        
        double t = (i + j + k) * G3;
        double X0 = i - t;
        double Y0 = j - t;
        double Z0 = k - t;
        double x0 = x - X0;
        double y0 = y - Y0;
        double z0 = z - Z0;
        
        // Determine simplex
        int i1, j1, k1;
        int i2, j2, k2;
        
        if (x0 >= y0)
        {
            if (y0 >= z0) { i1=1; j1=0; k1=0; i2=1; j2=1; k2=0; }
            else if (x0 >= z0) { i1=1; j1=0; k1=0; i2=1; j2=0; k2=1; }
            else { i1=0; j1=0; k1=1; i2=1; j2=0; k2=1; }
        }
        else
        {
            if (y0 < z0) { i1=0; j1=0; k1=1; i2=0; j2=1; k2=1; }
            else if (x0 < z0) { i1=0; j1=1; k1=0; i2=0; j2=1; k2=1; }
            else { i1=0; j1=1; k1=0; i2=1; j2=1; k2=0; }
        }
        
        double x1 = x0 - i1 + G3;
        double y1 = y0 - j1 + G3;
        double z1 = z0 - k1 + G3;
        double x2 = x0 - i2 + 2.0*G3;
        double y2 = y0 - j2 + 2.0*G3;
        double z2 = z0 - k2 + 2.0*G3;
        double x3 = x0 - 1.0 + 3.0*G3;
        double y3 = y0 - 1.0 + 3.0*G3;
        double z3 = z0 - 1.0 + 3.0*G3;
        
        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;
        int gi0 = _permMod12[ii + _perm[jj + _perm[kk]]];
        int gi1 = _permMod12[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]];
        int gi2 = _permMod12[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]];
        int gi3 = _permMod12[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]];
        
        double t0 = 0.6 - x0*x0 - y0*y0 - z0*z0;
        if (t0 < 0) n0 = 0.0;
        else { t0 *= t0; n0 = t0 * t0 * Dot(Grad3[gi0], x0, y0, z0); }
        
        double t1 = 0.6 - x1*x1 - y1*y1 - z1*z1;
        if (t1 < 0) n1 = 0.0;
        else { t1 *= t1; n1 = t1 * t1 * Dot(Grad3[gi1], x1, y1, z1); }
        
        double t2 = 0.6 - x2*x2 - y2*y2 - z2*z2;
        if (t2 < 0) n2 = 0.0;
        else { t2 *= t2; n2 = t2 * t2 * Dot(Grad3[gi2], x2, y2, z2); }
        
        double t3 = 0.6 - x3*x3 - y3*y3 - z3*z3;
        if (t3 < 0) n3 = 0.0;
        else { t3 *= t3; n3 = t3 * t3 * Dot(Grad3[gi3], x3, y3, z3); }
        
        return 32.0 * (n0 + n1 + n2 + n3);
    }
    
    /// <summary>
    /// Fractal Brownian Motion - multiple octaves of noise
    /// </summary>
    public double FBM(double x, double y, int octaves, double persistence = 0.5, double lacunarity = 2.0)
    {
        double total = 0;
        double frequency = 1;
        double amplitude = 1;
        double maxValue = 0;
        
        for (int i = 0; i < octaves; i++)
        {
            total += Noise2D(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        
        return total / maxValue;
    }
    
    /// <summary>
    /// Ridged noise - creates ridge-like patterns
    /// </summary>
    public double RidgedNoise(double x, double y, int octaves = 4)
    {
        double total = 0;
        double frequency = 1;
        double amplitude = 1;
        double weight = 1;
        
        for (int i = 0; i < octaves; i++)
        {
            double signal = Noise2D(x * frequency, y * frequency);
            signal = 1.0 - Math.Abs(signal);
            signal *= signal;
            signal *= weight;
            weight = Math.Clamp(signal * 2, 0, 1);
            total += signal * amplitude;
            frequency *= 2;
            amplitude *= 0.5;
        }
        
        return total;
    }
    
    private static int FastFloor(double x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }
    
    private static double Dot(int[] g, double x, double y)
    {
        return g[0] * x + g[1] * y;
    }
    
    private static double Dot(int[] g, double x, double y, double z)
    {
        return g[0] * x + g[1] * y + g[2] * z;
    }
}
