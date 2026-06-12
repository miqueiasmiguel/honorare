using System.Globalization;
using System.Runtime.CompilerServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace App.Faturamento.Pdf;

internal static class QuestPdfLicense
{
    [ModuleInitializer]
    internal static void Initialize() =>
        QuestPDF.Settings.License = LicenseType.Community;
}

internal sealed class RecursoPdfDocument(RecursoPdfData data) : IDocument
{
    private static readonly string[] _colunasParcial =
        ["Código", "Descrição", "Fator", "PG UNIMED", "VL CORRETO"];

    private static readonly string[] _colunasBranca = ["Código", "Descrição"];

    internal byte[] GeneratePdf()
    {
        IDocument document = this;
        return document.GeneratePdf();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
        });
    }

    private void ComposeHeader(IContainer c)
    {
        c.Column(col =>
        {
            if (data.TenantLogo is not null)
            {
                col.Item().Height(50).Image(data.TenantLogo).FitHeight();
            }

            col.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.Bold().FontSize(12));
                t.Span(data.TenantName);
            });
            col.Item().Text($"Recurso Nº {data.Numero}");
            col.Item().Text($"Operadora: {data.OperadoraNome}");
            col.Item().Text($"Prestador: {data.PrestadorNome}" +
                (data.PrestadorRegistroProfissional is not null
                    ? $" — CRM: {data.PrestadorRegistroProfissional}"
                    : string.Empty));
            col.Item().PaddingTop(4).LineHorizontal(0.5f);
        });
    }

    private void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            foreach (var guia in data.Guias)
            {
                col.Item().Element(ct => ComposeGuia(ct, guia, data.Tipo));
                col.Item().PaddingVertical(4);
            }

            if (data.Tipo == TipoRecurso.GlosaParcial)
            {
                col.Item().Element(ComposeTotaisFinais);
            }
        });
    }

    private static void ComposeGuia(IContainer c, GuiaPdfData guia, TipoRecurso tipo)
    {
        c.Column(col =>
        {
            col.Item().Background("#EEEEEE").Padding(3)
                .Text($"{guia.DataAtendimento:dd/MM/yyyy}  |  Guia: {guia.NumeroGuia}" +
                    (guia.BeneficiarioNome is not null ? $"  |  {guia.BeneficiarioNome}" : string.Empty) +
                    $"  |  {guia.PosicaoExecutorLabel}" +
                    (!string.IsNullOrEmpty(guia.LocalAtendimento) ? $"  |  {guia.LocalAtendimento}" : string.Empty));

            if (!string.IsNullOrEmpty(guia.Observacao))
            {
                col.Item().PaddingLeft(3).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontColor("#CC0000"));
                    t.Span(guia.Observacao!);
                });
            }

            col.Item().Element(ct => ComposeItensTable(ct, guia, tipo));
        });
    }

    private static void ComposeItensTable(IContainer c, GuiaPdfData guia, TipoRecurso tipo)
    {
        if (tipo == TipoRecurso.GlosaBranca)
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(8);
                });

                table.Header(h =>
                {
                    foreach (var label in _colunasBranca)
                    {
                        h.Cell().Border(0.5f).Padding(2).Text(t =>
                        {
                            t.DefaultTextStyle(s => s.Bold());
                            t.Span(label);
                        });
                    }
                });

                foreach (var item in guia.Itens)
                {
                    table.Cell().Border(0.5f).Padding(2).Text(item.CodigoTuss);
                    table.Cell().Border(0.5f).Padding(2).Text(item.Descricao);
                }
            });
        }
        else
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(8);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    foreach (var label in _colunasParcial)
                    {
                        h.Cell().Border(0.5f).Padding(2).Text(t =>
                        {
                            t.DefaultTextStyle(s => s.Bold());
                            t.Span(label);
                        });
                    }
                });

                foreach (var item in guia.Itens)
                {
                    table.Cell().Border(0.5f).Padding(2).Text(item.CodigoTuss);
                    table.Cell().Border(0.5f).Padding(2).Text(item.Descricao);
                    table.Cell().Border(0.5f).Padding(2).Text(item.FatorEfetivo);
                    table.Cell().Border(0.5f).Padding(2).Text(item.ValorPago.ToString("N2", CultureInfo.CurrentCulture));
                    table.Cell().Border(0.5f).Padding(2).Text(item.ValorApurado.ToString("N2", CultureInfo.CurrentCulture));
                }
            });
        }
    }

    private void ComposeTotaisFinais(IContainer c)
    {
        var totalPago = data.Guias.SelectMany(g => g.Itens).Sum(i => i.ValorPago);
        var totalApurado = data.Guias.SelectMany(g => g.Itens).Sum(i => i.ValorApurado);
        var restaPagar = totalApurado - totalPago;

        c.Column(col =>
        {
            col.Item().Text($"Total PG UNIMED: {totalPago.ToString("N2", CultureInfo.CurrentCulture)}");
            col.Item().Text($"Total VL CORRETO: {totalApurado.ToString("N2", CultureInfo.CurrentCulture)}");
            col.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.Bold());
                t.Span($"RESTA PAGAR: {restaPagar.ToString("N2", CultureInfo.CurrentCulture)}");
            });
        });
    }
}
