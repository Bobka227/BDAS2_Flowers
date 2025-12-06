using Microsoft.AspNetCore.Http;
using System.Text.Json;

/// <summary>
/// Rozšiřující metody pro práci se Session pomocí serializace do JSON.
/// </summary>
public static class SessionExtensions
{
    /// <summary>
    /// Uloží libovolný objekt do session pod zadaným klíčem jako JSON.
    /// </summary>
    /// <typeparam name="T">Typ ukládané hodnoty.</typeparam>
    /// <param name="session">Instance session, do které se má hodnota uložit.</param>
    /// <param name="key">Klíč, pod kterým bude hodnota v session uložena.</param>
    /// <param name="value">Hodnota, která se má uložit.</param>
    public static void SetJson<T>(this ISession session, string key, T value) =>
        session.SetString(key, JsonSerializer.Serialize(value));

    /// <summary>
    /// Načte hodnotu ze session a deserializuje ji z JSONu na zadaný typ.
    /// </summary>
    /// <typeparam name="T">Očekávaný typ uložené hodnoty.</typeparam>
    /// <param name="session">Instance session, ze které se má hodnota načíst.</param>
    /// <param name="key">Klíč, pod kterým je hodnota v session uložena.</param>
    /// <returns>
    /// Deserializovaná hodnota typu <typeparamref name="T"/>,
    /// nebo <c>null</c>, pokud klíč v session neexistuje.
    /// </returns>
    public static T? GetJson<T>(this ISession session, string key) =>
        session.GetString(key) is { } s ? JsonSerializer.Deserialize<T>(s) : default;
}
