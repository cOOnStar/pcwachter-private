using System;
using System.IO;

public static class AgentIdentity
{
    private static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PCWaechter",
        "device_install_id.txt");

    public static Guid GetOrCreateInstallId()
    {
        var directory = Path.GetDirectoryName(PathFile);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Install ID path is invalid.");
        }

        Directory.CreateDirectory(directory);

        if (File.Exists(PathFile))
        {
            var txt = File.ReadAllText(PathFile).Trim();
            if (Guid.TryParse(txt, out var gid))
            {
                return gid;
            }
        }

        var id = Guid.NewGuid();
        File.WriteAllText(PathFile, id.ToString());
        return id;
    }
}
