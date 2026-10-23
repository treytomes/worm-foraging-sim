using System.Text.Json;

namespace WormForaging.App.Shell;

/// <summary>
/// An <see cref="ISettingsStore"/> backed by JSON files in a single directory.
/// </summary>
/// <remarks>
/// Reads are forgiving: a missing, unreadable, or malformed file yields <see langword="null"/>
/// (the caller falls back to its default) rather than aborting startup. Writes are best-effort;
/// an I/O failure is swallowed so a read-only profile cannot crash the app over a preference.
/// </remarks>
public sealed class JsonFileSettingsStore : ISettingsStore
{
    #region Constants

    /// <summary>Serializer options: indented for hand-editing, case-insensitive on read.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    #endregion

    #region Fields

    private readonly string _directory;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a store rooted at <paramref name="directory"/>, creating it if needed.
    /// </summary>
    /// <param name="directory">The directory that holds the settings files.</param>
    public JsonFileSettingsStore(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        Directory.CreateDirectory(_directory);
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public T? Load<T>(string fileName) where T : class
    {
        var path = Path.Combine(_directory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Save<T>(string fileName, T settings) where T : class
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(Path.Combine(_directory, fileName), json);
        }
        catch (IOException)
        {
            // Best-effort: a preference is never worth crashing over.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort: a read-only profile keeps its in-memory theme.
        }
    }

    /// <summary>
    /// The default per-user directory for the shell's settings (<c>%LOCALAPPDATA%\WormForaging</c>).
    /// </summary>
    /// <returns>The absolute settings directory path (not created).</returns>
    public static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WormForaging");

    #endregion
}
