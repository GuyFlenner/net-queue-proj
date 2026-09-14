using FluentValidation;

namespace EmailProcessing.Api;

/// <summary>
/// The request DTO for <c>POST /api/emails</c>. This is the wire contract only — the
/// enqueued work item closes over the three primitive fields, never over this record or
/// any scoped/disposed DI service (see <see cref="EmailEndpoints"/>).
/// </summary>
public sealed record SendEmailRequest(string To, string Subject, string Body);

/// <summary>
/// FluentValidation rules for <see cref="SendEmailRequest"/>, run at the API boundary via
/// <see cref="ValidationFilter{T}"/> — never trust Minimal API model binding alone.
/// </summary>
public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress();
        RuleFor(x => x.Subject).NotEmpty();
        RuleFor(x => x.Body).NotEmpty();
    }
}

/// <summary>Response body for a successfully queued email: <c>{"status":"Queued"}</c>.</summary>
public sealed record SendEmailResponse(string Status);
