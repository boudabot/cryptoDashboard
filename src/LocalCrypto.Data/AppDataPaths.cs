namespace LocalCrypto.Data;

public static class AppDataPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "localCrypto");

    public static string DatabasePath => Path.Combine(DataDirectory, "localcrypto.sqlite");
}
