namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Argon2id password hashing configuration (Req 1.x clarifications).
/// </summary>
public class Argon2Settings
{
    public const string SectionName = "Argon2";

    /// <summary>
    /// Memory cost in KB. Default: 65536 (64 MB).
    /// </summary>
    public int MemoryCostKb { get; set; } = 65536;

    /// <summary>
    /// Number of iterations (time cost). Default: 3.
    /// </summary>
    public int Iterations { get; set; } = 3;

    /// <summary>
    /// Degree of parallelism. Default: 4.
    /// </summary>
    public int Parallelism { get; set; } = 4;

    /// <summary>
    /// Hash length in bytes. Default: 32 (256 bits).
    /// </summary>
    public int HashLength { get; set; } = 32;
}
