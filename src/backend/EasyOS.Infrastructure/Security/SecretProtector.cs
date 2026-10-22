using Microsoft.AspNetCore.DataProtection;

namespace EasyOS.Infrastructure.Security;

/// <summary>Cifra segredos guardados no catálogo (connection strings, senha SMTP).</summary>
public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

public sealed class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("EasyOS.Catalog.Secrets.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
