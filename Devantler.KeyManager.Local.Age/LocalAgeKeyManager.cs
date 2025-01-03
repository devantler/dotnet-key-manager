using System.Runtime.InteropServices;
using Devantler.AgeCLI;
using Devantler.KeyManager.Core;
using Devantler.KeyManager.Core.Models;
using Devantler.Keys.Age;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Devantler.KeyManager.Local.Age;

/// <summary>
/// A local key manager for SOPS with Age keys.
/// </summary>
public class LocalAgeKeyManager() : ILocalKeyManager<AgeKey>
{
  readonly string _sopsAgeKeyFile = GetSOPSAgeKeyFilePath();
  IDeserializer YAMLDeserializer { get; } = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
  ISerializer YAMLSerializer { get; } = new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

  /// <summary>
  /// Create a new key, and add it to the default key file.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> CreateKeyAsync(CancellationToken cancellationToken = default) => await CreateKeyAsync(_sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> CreateKeyAsync(string outKeyPath, CancellationToken cancellationToken = default)
  {
    // Create a new Age key.
    var key = await AgeKeygen.InMemory(cancellationToken).ConfigureAwait(false);

    // Create the directory if it does not exist.
    string? directory = Path.GetDirectoryName(outKeyPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      _ = Directory.CreateDirectory(directory);

    // Create the file if it does not exist.
    if (!File.Exists(outKeyPath))
    {
      using var fs = File.Create(outKeyPath);
    }

    // Append the key to the file if it does not exist.
    string fileContents = await File.ReadAllTextAsync(outKeyPath, cancellationToken).ConfigureAwait(false);
    if (!fileContents.Contains(key.ToString(), StringComparison.Ordinal))
      await File.AppendAllTextAsync(outKeyPath, key.ToString() + Environment.NewLine, cancellationToken).ConfigureAwait(false);

    return key;
  }

  /// <summary>
  /// Delete a key from the default key file.
  /// </summary>
  /// <param name="key"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> DeleteKeyAsync(AgeKey key, CancellationToken cancellationToken = default) => await DeleteKeyAsync(key, _sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> DeleteKeyAsync(AgeKey key, string keyPath, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(key);

    // Delete the key from the file.
    string fileContents = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);
    if (fileContents.Contains(key.ToString(), StringComparison.Ordinal))
    {
      fileContents = fileContents.Replace(key.ToString(), "", StringComparison.Ordinal);
      if (fileContents.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        fileContents = fileContents[..^Environment.NewLine.Length];
      await File.WriteAllTextAsync(keyPath, fileContents, cancellationToken).ConfigureAwait(false);
    }

    return key;
  }

  /// <summary>
  /// Delete a key by public key from the default key file.
  /// </summary>
  /// <param name="publicKey"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> DeleteKeyAsync(string publicKey, CancellationToken cancellationToken = default) => await DeleteKeyAsync(publicKey, _sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> DeleteKeyAsync(string publicKey, string keyPath, CancellationToken cancellationToken = default)
  {
    // Get the contents of the file.
    string fileContents = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);

    // Find the line number with the public key
    string[] lines = fileContents.Split(Environment.NewLine);
    int lineNumber = Array.IndexOf(lines, "# public key: " + publicKey);
    //Get the line above and below the public key
    string createdAtLine = lines[lineNumber - 1];
    string publicKeyLine = lines[lineNumber];
    string privateKeyLine = lines[lineNumber + 1];

    // Put the lines back together
    string rawKey = createdAtLine + Environment.NewLine + publicKeyLine + Environment.NewLine + privateKeyLine;

    // Parse the key
    var key = new AgeKey(rawKey);

    // Remove the key from the file including the new line characters
    fileContents = fileContents.Replace(rawKey, "", StringComparison.Ordinal);
    if (fileContents.EndsWith(Environment.NewLine, StringComparison.Ordinal))
      fileContents = fileContents[..^Environment.NewLine.Length];
    await File.WriteAllTextAsync(keyPath, fileContents, cancellationToken).ConfigureAwait(false);

    return key;
  }

  /// <summary>
  /// Get a key by public key from the default key file.
  /// </summary>
  /// <param name="publicKey"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> GetKeyAsync(string publicKey, CancellationToken cancellationToken = default) => await GetKeyAsync(publicKey, _sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> GetKeyAsync(string publicKey, string keyPath, CancellationToken cancellationToken = default)
  {
    // Get the contents of the file.
    string fileContents = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);

    if (!fileContents.Contains("# public key: " + publicKey, StringComparison.Ordinal))
      throw new KeyManagerException("the key does not exist in the key file.");

    // Find the line number with the public key
    string[] lines = fileContents.Split(Environment.NewLine);
    int lineNumber = Array.IndexOf(lines, "# public key: " + publicKey);
    //Get the line above and below the public key
    string createdAtLine = lines[lineNumber - 1];
    string publicKeyLine = lines[lineNumber];
    string privateKeyLine = lines[lineNumber + 1];

    // Put the lines back together
    string rawKey = createdAtLine + Environment.NewLine + publicKeyLine + Environment.NewLine + privateKeyLine;

    // Parse the key
    return new AgeKey(rawKey);
  }

  /// <summary>
  /// Import a key from a Key object to the default key file.
  /// </summary>
  /// <param name="inKey"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> ImportKeyAsync(AgeKey inKey, CancellationToken cancellationToken = default) => await ImportKeyAsync(inKey, _sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> ImportKeyAsync(AgeKey inKey, string outKeyPath, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(inKey);

    // Create the directory if it does not exist.
    string? directory = Path.GetDirectoryName(outKeyPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      _ = Directory.CreateDirectory(directory);

    // Create the file if it does not exist.
    if (!File.Exists(outKeyPath))
    {
      using var fs = File.Create(outKeyPath);
    }

    // Append the key to the file if it does not exist.
    string fileContents = await File.ReadAllTextAsync(outKeyPath, cancellationToken).ConfigureAwait(false);
    if (!fileContents.Contains(inKey.ToString(), StringComparison.Ordinal))
      await File.AppendAllTextAsync(outKeyPath, inKey.ToString(), cancellationToken).ConfigureAwait(false);

    return inKey;
  }

  /// <summary>
  /// Import a key from a file to the default key file.
  /// </summary>
  /// <param name="inKeyPath"></param>
  /// <param name="inKeyPublicKey"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="AgeKey"/></returns>
  public async Task<AgeKey> ImportKeyAsync(string inKeyPath, string? inKeyPublicKey = null, CancellationToken cancellationToken = default) => await ImportKeyAsync(inKeyPath, _sopsAgeKeyFile, inKeyPublicKey, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<AgeKey> ImportKeyAsync(string inKeyPath, string outKeyPath, string? inKeyPublicKey = null, CancellationToken cancellationToken = default)
  {
    // Read the key from the in key path.
    string inKeyfileContents = await File.ReadAllTextAsync(inKeyPath, cancellationToken).ConfigureAwait(false);

    // Find the line number with the public key
    string[] lines = inKeyfileContents.Split(Environment.NewLine);
    if (string.IsNullOrWhiteSpace(inKeyPublicKey) && lines.Length > 3)
      throw new InvalidOperationException("The public key must be provided if the key file contains more than one key.");
    else if (string.IsNullOrWhiteSpace(inKeyPublicKey))
      inKeyPublicKey = lines[1].Replace("# public key: ", "", StringComparison.Ordinal);

    int lineNumber = Array.IndexOf(lines, "# public key: " + inKeyPublicKey);
    //Get the line above and below the public key
    string createdAtLine = lines[lineNumber - 1];
    string publicKeyLine = lines[lineNumber];
    string privateKeyLine = lines[lineNumber + 1];

    // Put the lines back together
    string rawInKey = createdAtLine + Environment.NewLine + publicKeyLine + Environment.NewLine + privateKeyLine;

    // Parse the key
    var inKey = new AgeKey(rawInKey);

    // Read the key from the out key path.
    string outKeyfileContents = await File.ReadAllTextAsync(outKeyPath, cancellationToken).ConfigureAwait(false);

    // Append the key to the file if it does not exist.
    if (!outKeyfileContents.Contains(inKey.ToString(), StringComparison.Ordinal))
      await File.AppendAllTextAsync(outKeyPath, inKey.ToString() + Environment.NewLine, cancellationToken).ConfigureAwait(false);

    return inKey;

  }

  /// <summary>
  /// Check if a key exists in the default key file.
  /// </summary>
  /// <param name="publicKey"></param>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="bool"/></returns>
  public async Task<bool> KeyExistsAsync(string publicKey, CancellationToken cancellationToken = default) => await KeyExistsAsync(publicKey, _sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<bool> KeyExistsAsync(string publicKey, string keyPath, CancellationToken cancellationToken = default)
  {
    // Get the contents of the file.
    string fileContents = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);

    // Check if the public key exists in the file.
    return fileContents.Contains("# public key: " + publicKey, StringComparison.Ordinal);
  }

  /// <summary>
  /// List all keys from the default key file.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns><see cref="IEnumerable{AgeKey}"/></returns>
  public async Task<IEnumerable<AgeKey>> ListKeysAsync(CancellationToken cancellationToken = default) => await ListKeysAsync(_sopsAgeKeyFile, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<IEnumerable<AgeKey>> ListKeysAsync(string keyPath, CancellationToken cancellationToken = default)
  {
    if (!File.Exists(keyPath))
      return [];

    string fileContents = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);

    // Find the line number with the public key
    string[] lines = fileContents.Split(Environment.NewLine);
    List<AgeKey> keys = [];
    for (int i = 0; i < lines.Length; i++)
    {
      if (lines[i].StartsWith("# created: ", StringComparison.Ordinal))
      {
        string createdAtLine = lines[i];
        string publicKeyLine = lines[i + 1];
        string privateKeyLine = lines[i + 2];

        // Put the lines back together
        string rawKey = createdAtLine + Environment.NewLine + publicKeyLine + Environment.NewLine + privateKeyLine;

        // Parse the key
        keys.Add(new AgeKey(rawKey));
      }
    }

    return keys;
  }

  /// <inheritdoc/>
  public async Task<SOPSConfig> GetSOPSConfigAsync(string configPath, CancellationToken cancellationToken = default)
  {
    string configContents = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
    var config = YAMLDeserializer.Deserialize<SOPSConfig>(configContents);
    return config;
  }

  /// <inheritdoc/>
  public async Task CreateSOPSConfigAsync(string configPath, SOPSConfig config, bool overwrite = false, CancellationToken cancellationToken = default)
  {
    if (!overwrite && File.Exists(configPath))
      throw new InvalidOperationException("The file already exists and overwrite is set to false.");

    // Create the directory if it does not exist.
    string? directory = Path.GetDirectoryName(configPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      _ = Directory.CreateDirectory(directory);

    string configRaw = YAMLSerializer.Serialize(config);
    await File.WriteAllTextAsync(configPath, configRaw, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Get the path to the SOPS_AGE_KEY_FILE or the default path for the current OS.
  /// </summary>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  static string GetSOPSAgeKeyFilePath()
  {
    string? sopsAgeKeyFileEnvironmentVariable = Environment.GetEnvironmentVariable("SOPS_AGE_KEY_FILE");
    string sopsAgeKeyFile = "";
    if (!string.IsNullOrWhiteSpace(sopsAgeKeyFileEnvironmentVariable))
    {
      if (!File.Exists(sopsAgeKeyFileEnvironmentVariable))
        throw new ArgumentException($"The SOPS_AGE_KEY_FILE environment variable points to a file that does not exist: {sopsAgeKeyFileEnvironmentVariable}");
      sopsAgeKeyFile = sopsAgeKeyFileEnvironmentVariable;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      sopsAgeKeyFile = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Application Support/sops/age/keys.txt";

    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      string xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.config";
      sopsAgeKeyFile = $"{xdgConfigHome}/sops/age/keys.txt";

    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      sopsAgeKeyFile = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/sops/age/keys.txt";
    }
    return sopsAgeKeyFile;
  }
}
