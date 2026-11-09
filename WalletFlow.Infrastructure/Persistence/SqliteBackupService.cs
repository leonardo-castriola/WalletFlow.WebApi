using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WalletFlow.Infrastructure.Persistence;

public static class SqliteBackupService
{
    private static readonly byte[] EncryptedHeaderMagic = "WFBK01"u8.ToArray();
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 600_000;

    public static async Task<string> CreateConsistentBackupAsync(
        string sourceDatabasePath,
        string destinationBackupPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationBackupPath);

        var fullSource = Path.GetFullPath(sourceDatabasePath);
        var fullDestination = Path.GetFullPath(destinationBackupPath);
        var destinationDir = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrWhiteSpace(destinationDir))
            Directory.CreateDirectory(destinationDir);

        if (File.Exists(fullDestination))
            File.Delete(fullDestination);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullSource,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            var escapedDestination = fullDestination.Replace("'", "''", StringComparison.Ordinal);
            command.CommandText = $"VACUUM INTO '{escapedDestination}';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await VerifyIntegrityAsync(fullDestination, cancellationToken);

        await using var fileStream = File.OpenRead(fullDestination);
        var hashBytes = await SHA256.HashDataAsync(fileStream, cancellationToken);
        var checksum = Convert.ToHexStringLower(hashBytes);
        await File.WriteAllTextAsync($"{fullDestination}.sha256", $"{checksum}  {Path.GetFileName(fullDestination)}{Environment.NewLine}", cancellationToken);
        return checksum;
    }

    public static async Task<string> CreateEncryptedBackupAsync(
        string sourceDatabasePath,
        string destinationEncryptedPath,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationEncryptedPath);
        if (string.IsNullOrWhiteSpace(passphrase) || Encoding.UTF8.GetByteCount(passphrase) < 16)
            throw new ArgumentException("A chave de criptografia do backup deve ter pelo menos 16 bytes.", nameof(passphrase));

        var fullEncryptedPath = Path.GetFullPath(destinationEncryptedPath);
        var destinationDir = Path.GetDirectoryName(fullEncryptedPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
            Directory.CreateDirectory(destinationDir);

        var tempSnapshot = Path.Combine(Path.GetTempPath(), $"walletflow-snapshot-{Guid.NewGuid():N}.db");
        try
        {
            await CreateConsistentBackupAsync(sourceDatabasePath, tempSnapshot, cancellationToken);
            var plaintext = await File.ReadAllBytesAsync(tempSnapshot, cancellationToken);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            using (var aesGcm = new AesGcm(key, TagSize))
            {
                aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, EncryptedHeaderMagic);
            }

            await using (var output = File.Create(fullEncryptedPath))
            {
                await output.WriteAsync(EncryptedHeaderMagic, cancellationToken);
                await output.WriteAsync(salt, cancellationToken);
                await output.WriteAsync(nonce, cancellationToken);
                await output.WriteAsync(tag, cancellationToken);
                await output.WriteAsync(ciphertext, cancellationToken);
            }

            await using var encryptedStream = File.OpenRead(fullEncryptedPath);
            var hashBytes = await SHA256.HashDataAsync(encryptedStream, cancellationToken);
            var checksum = Convert.ToHexStringLower(hashBytes);
            await File.WriteAllTextAsync(
                $"{fullEncryptedPath}.sha256",
                $"{checksum}  {Path.GetFileName(fullEncryptedPath)}{Environment.NewLine}",
                cancellationToken);
            return checksum;
        }
        finally
        {
            foreach (var tempFile in new[] { tempSnapshot, $"{tempSnapshot}.sha256" })
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }

    public static async Task RestoreEncryptedBackupAsync(
        string encryptedBackupPath,
        string targetDatabasePath,
        string passphrase,
        string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedBackupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        var fullEncrypted = Path.GetFullPath(encryptedBackupPath);
        if (!File.Exists(fullEncrypted))
            throw new FileNotFoundException("O arquivo de backup criptografado não foi encontrado.", fullEncrypted);

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            await using var fileStream = File.OpenRead(fullEncrypted);
            var actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fileStream, cancellationToken));
            if (!string.Equals(actualHash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O checksum SHA-256 do backup criptografado não confere.");
        }

        var payload = await File.ReadAllBytesAsync(fullEncrypted, cancellationToken);
        var headerLength = EncryptedHeaderMagic.Length + SaltSize + NonceSize + TagSize;
        if (payload.Length <= headerLength ||
            !payload.AsSpan(0, EncryptedHeaderMagic.Length).SequenceEqual(EncryptedHeaderMagic))
        {
            throw new InvalidOperationException("O arquivo de backup criptografado possui formato inválido.");
        }

        var offset = EncryptedHeaderMagic.Length;
        var salt = payload.AsSpan(offset, SaltSize).ToArray();
        offset += SaltSize;
        var nonce = payload.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = payload.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var ciphertext = payload.AsSpan(offset).ToArray();

        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        var plaintext = new byte[ciphertext.Length];
        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, EncryptedHeaderMagic);
        }

        var tempDecrypted = Path.Combine(Path.GetTempPath(), $"walletflow-restore-{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllBytesAsync(tempDecrypted, plaintext, cancellationToken);
            await RestoreConsistentBackupAsync(tempDecrypted, targetDatabasePath, expectedSha256: null, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempDecrypted))
                File.Delete(tempDecrypted);
        }
    }

    public static async Task RestoreConsistentBackupAsync(
        string backupDatabasePath,
        string targetDatabasePath,
        string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);

        var fullBackup = Path.GetFullPath(backupDatabasePath);
        if (!File.Exists(fullBackup))
            throw new FileNotFoundException("O arquivo de backup não foi encontrado.", fullBackup);

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            await using var fileStream = File.OpenRead(fullBackup);
            var actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fileStream, cancellationToken));
            if (!string.Equals(actualHash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O checksum SHA-256 do backup não confere.");
        }

        await VerifyIntegrityAsync(fullBackup, cancellationToken);

        SqliteConnection.ClearAllPools();

        var fullTarget = Path.GetFullPath(targetDatabasePath);
        var targetDir = Path.GetDirectoryName(fullTarget);
        if (!string.IsNullOrWhiteSpace(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (var walOrShm in new[] { $"{fullTarget}-wal", $"{fullTarget}-shm" })
        {
            if (File.Exists(walOrShm))
                File.Delete(walOrShm);
        }

        File.Copy(fullBackup, fullTarget, overwrite: true);
        await VerifyIntegrityAsync(fullTarget, cancellationToken);
    }

    public static async Task VerifyIntegrityAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Falha na verificação de integridade do SQLite: {result}");
    }
}
