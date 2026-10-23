namespace WormForaging.App.Shell;

/// <summary>
/// A tiny JSON-file store for shell preferences.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the named settings file, or returns <see langword="null"/> when it does not exist or
    /// cannot be read/parsed.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="fileName">The file name, relative to the store's directory.</param>
    /// <returns>The deserialized settings, or <see langword="null"/>.</returns>
    T? Load<T>(string fileName) where T : class;

    /// <summary>
    /// Writes the named settings file, creating the directory if needed.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="fileName">The file name, relative to the store's directory.</param>
    /// <param name="settings">The settings to serialize.</param>
    void Save<T>(string fileName, T settings) where T : class;
}
