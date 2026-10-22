using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using EasyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyOS.Api.Common;

public static class Text
{
    public static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Required(string? value, string field) =>
        Clean(value) ?? throw new BusinessException($"Informe {field}.");

    /// <summary>
    /// Filtro "contém", sem diferenciar acentos nem maiúsculas (como a busca do legado).
    /// No SQL Server usa collation AI; em outros providers (testes) compara em minúsculas.
    /// </summary>
    public static IQueryable<T> WhereContains<T>(this IQueryable<T> query, TenantDbContext db, Expression<Func<T, string>> property, string? term)
    {
        var clean = Clean(term);
        if (clean is null) return query;

        var param = property.Parameters[0];
        Expression body;
        if (db.Database.IsSqlServer())
        {
            var collate = typeof(RelationalDbFunctionsExtensions).GetMethod(nameof(RelationalDbFunctionsExtensions.Collate))!.MakeGenericMethod(typeof(string));
            var collated = Expression.Call(collate, Expression.Constant(EF.Functions), property.Body, Expression.Constant("Latin1_General_CI_AI"));
            body = Expression.Call(collated, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, Expression.Constant(clean));
        }
        else
        {
            var lower = Expression.Call(property.Body, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            body = Expression.Call(lower, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, Expression.Constant(clean.ToLowerInvariant()));
        }
        return query.Where(Expression.Lambda<Func<T, bool>>(body, param));
    }
}
