namespace WalletFlow.Application.Abstractions;

public interface IUsuarioConsulta
{
    Task<bool> ExisteAsync(string userId, CancellationToken cancellationToken = default);
}
