namespace App;

internal abstract record DomainError(string Message);

internal sealed record ValidationError(string Message) : DomainError(Message);

internal sealed record ConflictError(string Message) : DomainError(Message);

internal sealed record NotFoundError(string Message) : DomainError(Message);

internal sealed record UnauthorizedError(string Message) : DomainError(Message);

internal sealed record ForbiddenError(string Message) : DomainError(Message);
