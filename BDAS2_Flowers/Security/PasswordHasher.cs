using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BDAS2_Flowers.Security;

/// <summary>
/// Rozhraní definující operace pro hashování hesel a jejich ověřování.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Vytvoří hash zadaného hesla.
    /// </summary>
    /// <param name="password">Čisté heslo zadané uživatelem.</param>
    /// <returns>Hash hesla ve formátu Base64.</returns>
    string Hash(string password);

    /// <summary>
    /// Ověří, zda zadané heslo odpovídá uloženému hash hodnotě.
    /// </summary>
    /// <param name="password">Zadané heslo, které se má ověřit.</param>
    /// <param name="storedBase64">Hash uložený v databázi.</param>
    /// <returns><c>true</c>, pokud heslo odpovídá; jinak <c>false</c>.</returns>
    bool Verify(string password, string storedBase64);
}

/// <summary>
/// Implementace hashování hesel pomocí HMAC-SHA256 s použitím tajného „pepperu“.
/// </summary>
/// <remarks>
/// Hashování využívá HMAC-SHA256, kde je klíčem aplikací definovaný „pepper“.
/// Na rozdíl od soli (salt) je pepper stejný pro všechny uživatele a není ukládán v databázi.
/// Slouží jako další bezpečnostní vrstva při úniku databáze.
/// </remarks>
public class HmacSha256PasswordHasher : IPasswordHasher
{
    private readonly byte[] _pepper;

    /// <summary>
    /// Inicializuje novou instanci <see cref="HmacSha256PasswordHasher"/>.
    /// </summary>
    /// <param name="cfg">Konfigurace aplikace obsahující hodnotu <c>Auth:Pepper</c>.</param>
    /// <exception cref="InvalidOperationException">Vyvolá se, pokud není pepper v konfiguraci nalezen.</exception>
    public HmacSha256PasswordHasher(IConfiguration cfg)
    {
        var pepper = cfg["Auth:Pepper"] ?? throw new InvalidOperationException("Pepper missing");
        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    /// <summary>
    /// Vytvoří hash hesla pomocí HMAC-SHA256.
    /// </summary>
    /// <param name="password">Čisté heslo.</param>
    /// <returns>Hash hesla zakódovaný jako Base64.</returns>
    public string Hash(string password)
    {
        using var hmac = new HMACSHA256(_pepper);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Ověří, zda heslo odpovídá uloženému hash hodnotě.
    /// </summary>
    /// <param name="password">Zadané heslo.</param>
    /// <param name="storedBase64">Hash uložený v databázi.</param>
    /// <returns><c>true</c>, pokud jsou hashe totožné; jinak <c>false</c>.</returns>
    public bool Verify(string password, string storedBase64)
        => Hash(password) == storedBase64;
}
