using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BDAS2_Flowers.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedBase64);
}

public class HmacSha256PasswordHasher : IPasswordHasher
{
    private readonly byte[] _pepper;
    public HmacSha256PasswordHasher(IConfiguration cfg)
    {
        var pepper = cfg["Auth:Pepper"] ?? throw new InvalidOperationException("Pepper missing");
        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string Hash(string password)
    {
        using var hmac = new HMACSHA256(_pepper);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public bool Verify(string password, string storedBase64)
        => Hash(password) == storedBase64;
}
