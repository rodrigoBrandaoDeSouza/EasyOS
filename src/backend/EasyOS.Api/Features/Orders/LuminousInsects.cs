namespace EasyOS.Api.Features.Orders;

/// <summary>
/// Na contagem das armadilhas luminosas o legado grava códigos fixos de inseto no campo idplague
/// (não são ids da tabela Plagues). O código 5 tem rótulo configurável por cliente ("Outros"/"Abelhas").
/// O código 0 representa a quantidade total sem identificação da espécie.
/// </summary>
public static class LuminousInsects
{
    public static readonly int[] Codes = [1, 2, 3, 4, 5];

    public static string Label(int code, string otherLabel) => code switch
    {
        1 => "Mosca",
        2 => "Mosquito",
        3 => "Mariposa",
        4 => "Besouro",
        5 => otherLabel,
        _ => "Não especificado",
    };

    /// <summary>Cabeçalhos das colunas na ordem usada pelos relatórios: códigos 1–5 e "Não especificado".</summary>
    public static IReadOnlyList<string> Columns(string otherLabel) =>
        [.. Codes.Select(c => Label(c, otherLabel)), Label(0, otherLabel)];
}
