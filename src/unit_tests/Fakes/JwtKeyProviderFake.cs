using System.Text;
using NovaTuneApp.ApiService.Infrastructure.Identity;

namespace NovaTune.UnitTests.Fakes;

public class JwtKeyProviderFake : IJwtKeyProvider
{
    public const string DefaultSigningKey = "test-signing-key-must-be-at-least-32-characters-long";

    public byte[] SigningKey { get; set; } = Encoding.UTF8.GetBytes(DefaultSigningKey);
}