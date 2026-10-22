using EasyOS.Domain.Entities;

namespace EasyOS.Api.Common;

/// <summary>
/// Convenção de datas do legado para visitas e OS: o banco guarda "horário local − offset"
/// (na prática, UTC) e o offset em minutos (ex.: −180 para Brasília). Para exibir, soma-se o offset.
/// A API recebe e devolve sempre o horário local de parede (sem fuso), junto com o offset.
/// </summary>
public static class LegacyTime
{
    public static DateTime ToStored(DateTime localWallClock, int offsetMinutes) =>
        DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified).AddMinutes(-offsetMinutes);

    public static DateTime ToLocal(DateTime stored, int offsetMinutes) =>
        DateTime.SpecifyKind(stored.AddMinutes(offsetMinutes), DateTimeKind.Unspecified);

    public static DateTime? ToLocal(DateTime? stored, int offsetMinutes) =>
        stored is null ? null : ToLocal(stored.Value, offsetMinutes);

    /// <summary>Offset enviado pelo navegador; valores absurdos caem no padrão de Brasília.</summary>
    public static int NormalizeOffset(int? offset) =>
        offset is >= -14 * 60 and <= 14 * 60 ? offset.Value : Visit.DefaultOffsetMinutes;
}
