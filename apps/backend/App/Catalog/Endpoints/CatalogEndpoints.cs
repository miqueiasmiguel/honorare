namespace App.Catalog.Endpoints;

internal static class CatalogEndpoints
{
    internal static void MapCatalogEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/operadoras").RequireAuthorization("TenantAccess");

        g.MapGet("", ListarOperadorasAsync);
        g.MapGet("{id:guid}", ObterOperadoraAsync);
        g.MapPost("", CriarOperadoraAsync);
        g.MapPut("{id:guid}", AtualizarOperadoraAsync);
        g.MapDelete("{id:guid}", ExcluirOperadoraAsync);

        var gp = app.MapGroup("/api/v1/admin/procedimentos").RequireAuthorization("TenantAccess");

        gp.MapGet("", ListarProcedimentosAsync);
        gp.MapGet("{id:guid}", ObterProcedimentoAsync);
        gp.MapPost("", CriarProcedimentoAsync);
        gp.MapPut("{id:guid}", AtualizarProcedimentoAsync);
        gp.MapDelete("{id:guid}", ExcluirProcedimentoAsync);
        gp.MapPost("importar-csv", ImportarCsvAsync).DisableAntiforgery();
        gp.MapGet("{procId:guid}/valores", ListarValoresProcedimentoAsync);
        gp.MapPut("{procId:guid}/valores/{operadoraId:guid}", UpsertValorProcedimentoAsync);
        gp.MapDelete("{procId:guid}/valores/{operadoraId:guid}", ExcluirValorProcedimentoAsync);

        var gta = app.MapGroup("/api/v1/admin/tabelas").RequireAuthorization("TenantAccess");

        gta.MapGet("", ListarTabelasAsync);
        gta.MapGet("{id:guid}", ObterTabelaAsync);
        gta.MapPost("", CriarTabelaAsync);
        gta.MapPut("{id:guid}", AtualizarTabelaAsync);
        gta.MapDelete("{id:guid}", ExcluirTabelaAsync);
        gta.MapPost("importar-csv", ImportarTabelaCsvAsync).DisableAntiforgery();

        var gpr = app.MapGroup("/api/v1/admin/prestadores").RequireAuthorization("TenantAccess");

        gpr.MapGet("", ListarPrestadoresAsync);
        gpr.MapGet("{id:guid}", ObterPrestadorAsync);
        gpr.MapPost("", CriarPrestadorAsync);
        gpr.MapPut("{id:guid}", AtualizarPrestadorAsync);
        gpr.MapPatch("{id:guid}/email-acesso", DefinirEmailAcessoAsync);
        gpr.MapDelete("{id:guid}", ExcluirPrestadorAsync);

        var gb = app.MapGroup("/api/v1/admin/beneficiarios").RequireAuthorization("TenantAccess");

        gb.MapGet("", ListarBeneficiariosAsync);
        gb.MapGet("{id:guid}", ObterBeneficiarioPorIdAsync);
        gb.MapPost("lookup-or-create", LookupOrCreateBeneficiarioAsync);
        gb.MapPut("{id:guid}", AtualizarBeneficiarioAsync);
        gb.MapDelete("{id:guid}", ExcluirBeneficiarioAsync);

        var gporte = app
            .MapGroup("/api/v1/admin/tabelas-porte-anestesico")
            .RequireAuthorization("TenantAccess");

        gporte.MapGet("", ListarPortesAnestesicoAsync);
        gporte.MapDelete("{id:guid}", ExcluirPorteAnestesicoAsync);
        gporte.MapPost("importar-unimed-csv", ImportarPorteAnestesicoCsvAsync).DisableAntiforgery();

        var gordem = app
            .MapGroup("/api/v1/admin/operadoras/{operadoraId:guid}/tabela-ordem")
            .RequireAuthorization("TenantAccess");

        gordem.MapGet("", ListarTabelaOrdemAsync);
        gordem.MapPut("", SalvarTabelaOrdemAsync);
        gordem.MapDelete("", ExcluirTabelaOrdemAsync);
    }

    // ── Operadora handlers ────────────────────────────────────────────────────

    private static async Task<IResult> ListarOperadorasAsync(
        [AsParameters] ListarOperadorasRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarOperadorasQuery(req.Nome, req.Ativa, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterOperadoraAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarOperadoraAsync(
        CriarOperadoraRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new CriarOperadoraCommand(body.Nome, body.RegistroAns, body.Cnpj, body.TipoRuleSet);
        var result = await service.CriarAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/operadoras/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarOperadoraAsync(
        Guid id, AtualizarOperadoraRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new AtualizarOperadoraCommand(
            body.Nome, body.RegistroAns, body.Cnpj, body.TipoRuleSet, body.Ativa);
        var result = await service.AtualizarAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirOperadoraAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    // ── Procedimento handlers ─────────────────────────────────────────────────

    private static async Task<IResult> ListarProcedimentosAsync(
        [AsParameters] ListarProcedimentosRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarProcedimentosQuery(req.Busca, req.Ativo, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarProcedimentosAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterProcedimentoAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterProcedimentoPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarProcedimentoAsync(
        SalvarProcedimentoRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarProcedimentoCommand(
            body.CodigoTuss, body.Descricao, body.Porte,
            body.PorteAnestesico, body.EhSadt, body.TemPorteProprioVideo, body.Ativo);
        var result = await service.CriarProcedimentoAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/procedimentos/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarProcedimentoAsync(
        Guid id, SalvarProcedimentoRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarProcedimentoCommand(
            body.CodigoTuss, body.Descricao, body.Porte,
            body.PorteAnestesico, body.EhSadt, body.TemPorteProprioVideo, body.Ativo);
        var result = await service.AtualizarProcedimentoAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirProcedimentoAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirProcedimentoAsync(id, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status404NotFound,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    // ── Tabela handlers ───────────────────────────────────────────────────────

    private static async Task<IResult> ListarTabelasAsync(
        [AsParameters] ListarTabelasRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarTabelasQuery(req.OperadoraId, req.CodigoTuss, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarTabelasAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterTabelaAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterTabelaPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarTabelaAsync(
        SalvarTabelaRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarTabelaCommand(body.OperadoraId, body.ProcedimentoId, body.Valor);
        var result = await service.CriarTabelaAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/tabelas/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarTabelaAsync(
        Guid id, SalvarTabelaRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarTabelaCommand(body.OperadoraId, body.ProcedimentoId, body.Valor);
        var result = await service.AtualizarTabelaAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirTabelaAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirTabelaAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ListarValoresProcedimentoAsync(
        Guid procId, CatalogService service, CancellationToken ct)
    {
        var result = await service.ListarValoresPorProcedimentoAsync(procId, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpsertValorProcedimentoAsync(
        Guid procId, Guid operadoraId, UpsertValorRequest body,
        CatalogService service, CancellationToken ct)
    {
        var result = await service.UpsertValorAsync(procId, operadoraId, body.Valor, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ValidationError => StatusCodes.Status422UnprocessableEntity,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        if (result.Value!.Criado)
        {
            return Results.Created(
                $"/api/v1/admin/tabelas/{result.Value.Tabela.Id}",
                result.Value.Tabela);
        }

        return Results.Ok(result.Value.Tabela);
    }

    private static async Task<IResult> ExcluirValorProcedimentoAsync(
        Guid procId, Guid operadoraId, CatalogService service, CancellationToken ct)
    {
        await service.ExcluirValorPorProcOpAsync(procId, operadoraId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ImportarTabelaCsvAsync(
        Guid operadoraId, IFormFile? file, CatalogService service, CancellationToken ct)
    {
        if (file is null ||
            !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "É necessário enviar um arquivo com extensão .csv.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "O arquivo excede o tamanho máximo de 5 MB.");
        }

        using var stream = file.OpenReadStream();
        var result = await service.ImportarTabelaCsvAsync(stream, operadoraId, ct);
        return Results.Ok(result);
    }

    // ── Prestador handlers ────────────────────────────────────────────────────

    private static async Task<IResult> ListarPrestadoresAsync(
        [AsParameters] ListarPrestadoresRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarPrestadoresQuery(req.Busca, req.Ativo, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarPrestadoresAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterPrestadorAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterPrestadorPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarPrestadorAsync(
        CriarPrestadorRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new CriarPrestadorCommand(body.Nome, body.RegistroProfissional, body.EmailAcesso);
        var result = await service.CriarPrestadorAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/prestadores/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarPrestadorAsync(
        Guid id, AtualizarPrestadorRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new AtualizarPrestadorCommand(body.Nome, body.RegistroProfissional, body.Ativo);
        var result = await service.AtualizarPrestadorAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> DefinirEmailAcessoAsync(
        Guid id, DefinirEmailAcessoRequest body, CatalogService service, CancellationToken ct)
    {
        var result = await service.DefinirEmailAcessoAsync(id, body.Email, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirPrestadorAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirPrestadorAsync(id, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status404NotFound,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    // ── Beneficiário handlers ─────────────────────────────────────────────────

    private static async Task<IResult> ListarBeneficiariosAsync(
        [AsParameters] ListarBeneficiariosRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarBeneficiariosQuery(req.Carteira, req.Nome, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarBeneficiariosAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterBeneficiarioPorIdAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterBeneficiarioPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> LookupOrCreateBeneficiarioAsync(
        LookupOrCreateBeneficiarioRequest body, CatalogService service, CancellationToken ct)
    {
        var result = await service.LookupOrCreateAsync(body.Carteira, body.Nome, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error!.Message);
        }

        var dto = result.Value!;
        if (dto.Criado)
        {
            return Results.Created(
                $"/api/v1/admin/beneficiarios/{dto.Beneficiario.Id}",
                new { dto.Beneficiario.Id, dto.Beneficiario.Carteira, dto.Beneficiario.Nome, dto.Beneficiario.CriadoEm, dto.Criado });
        }

        return Results.Ok(
            new { dto.Beneficiario.Id, dto.Beneficiario.Carteira, dto.Beneficiario.Nome, dto.Beneficiario.CriadoEm, dto.Criado });
    }

    private static async Task<IResult> AtualizarBeneficiarioAsync(
        Guid id, AtualizarBeneficiarioRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new AtualizarBeneficiarioCommand(body.Nome);
        var result = await service.AtualizarBeneficiarioAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirBeneficiarioAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirBeneficiarioAsync(id, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status404NotFound,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ImportarCsvAsync(
        IFormFile? file, CatalogService service, CancellationToken ct)
    {
        if (file is null ||
            !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "É necessário enviar um arquivo com extensão .csv.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "O arquivo excede o tamanho máximo de 5 MB.");
        }

        using var stream = file.OpenReadStream();
        var result = await service.ImportarProcedimentosCsvAsync(stream, ct);
        return Results.Ok(result);
    }

    // ── TabelaPorteAnestesico handlers ────────────────────────────────────────

    private static async Task<IResult> ListarPortesAnestesicoAsync(
        Guid operadoraId, CatalogService service, CancellationToken ct)
    {
        var result = await service.ListarPortesAnestesicoAsync(operadoraId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ExcluirPorteAnestesicoAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        await service.ExcluirPorteAnestesicoAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ImportarPorteAnestesicoCsvAsync(
        IFormFile? file, Guid operadoraId, CatalogService service, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "É necessário enviar um arquivo CSV.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "O arquivo excede o tamanho máximo de 5 MB.");
        }

        using var stream = file.OpenReadStream();
        var result = await service.ImportarTabelaUnimedAnestesistaAsync(stream, operadoraId, ct);
        return Results.Ok(result);
    }

    // ── TabelaOrdemOperadora handlers ─────────────────────────────────────────

    private static async Task<IResult> ListarTabelaOrdemAsync(
        Guid operadoraId, CatalogService service, CancellationToken ct)
    {
        var result = await service.ListarTabelaOrdemAsync(operadoraId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SalvarTabelaOrdemAsync(
        Guid operadoraId,
        SalvarTabelaOrdemRequest[] body,
        CatalogService service,
        CancellationToken ct)
    {
        if (body.Any(i => i.Percentual < 0.01m || i.Percentual > 1.00m))
        {
            return Results.UnprocessableEntity(
                new { error = "Percentual deve estar entre 0.01 e 1.00." });
        }

        var itens = body
            .Select(i => new SalvarOrdemItem(i.NumeroProcedimento, i.TipoVia, i.Percentual))
            .ToList();

        await service.SalvarTabelaOrdemAsync(operadoraId, itens, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ExcluirTabelaOrdemAsync(
        Guid operadoraId, CatalogService service, CancellationToken ct)
    {
        await service.ExcluirTabelaOrdemAsync(operadoraId, ct);
        return Results.NoContent();
    }
}

internal sealed record ListarOperadorasRequest(
    string? Nome = null,
    bool? Ativa = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record CriarOperadoraRequest(
    string Nome,
    string? RegistroAns,
    string? Cnpj,
    TipoRuleSet TipoRuleSet);

internal sealed record AtualizarOperadoraRequest(
    string Nome,
    string? RegistroAns,
    string? Cnpj,
    TipoRuleSet TipoRuleSet,
    bool Ativa);

internal sealed record ListarProcedimentosRequest(
    string? Busca = null,
    bool? Ativo = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record SalvarProcedimentoRequest(
    string CodigoTuss,
    string Descricao,
    string? Porte,
    string? PorteAnestesico,
    bool EhSadt,
    bool TemPorteProprioVideo,
    bool Ativo);

internal sealed record ListarTabelasRequest(
    Guid? OperadoraId = null,
    string? CodigoTuss = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record SalvarTabelaRequest(
    Guid OperadoraId,
    Guid ProcedimentoId,
    decimal Valor);

internal sealed record UpsertValorRequest(decimal Valor);

internal sealed record ListarPrestadoresRequest(
    string? Busca = null,
    bool? Ativo = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record CriarPrestadorRequest(
    string Nome,
    string? RegistroProfissional,
    string? EmailAcesso);

internal sealed record AtualizarPrestadorRequest(
    string Nome,
    string? RegistroProfissional,
    bool Ativo);

internal sealed record DefinirEmailAcessoRequest(string Email);

internal sealed record ListarBeneficiariosRequest(
    string? Carteira = null,
    string? Nome = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record LookupOrCreateBeneficiarioRequest(string Carteira, string Nome);

internal sealed record AtualizarBeneficiarioRequest(string Nome);

internal sealed record SalvarTabelaOrdemRequest(
    int NumeroProcedimento,
    TipoViaOrdem TipoVia,
    decimal Percentual);
