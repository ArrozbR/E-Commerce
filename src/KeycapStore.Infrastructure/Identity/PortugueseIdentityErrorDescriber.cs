using Microsoft.AspNetCore.Identity;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    private const string EmailAlreadyRegistered = "Este e-mail já está cadastrado.";
    private const string InvalidEmailMessage = "E-mail inválido.";

    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), EmailAlreadyRegistered);

    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), EmailAlreadyRegistered);

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), InvalidEmailMessage);

    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), InvalidEmailMessage);

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"A senha precisa ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "A senha precisa ter pelo menos um símbolo (ex.: #, @, !).");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "A senha precisa ter pelo menos um número (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "A senha precisa ter pelo menos uma letra minúscula (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "A senha precisa ter pelo menos uma letra maiúscula (A-Z).");

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Não foi possível concluir. Tente novamente.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
