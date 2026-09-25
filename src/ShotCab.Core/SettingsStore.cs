using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace ShotCab.Core
{
    public sealed class SettingsStore
    {
        private readonly string settingsPath;

        public SettingsStore(string root)
        {
            RootPath = StoragePath.NormalizeRoot(root);
            Directory.CreateDirectory(RootPath);
            settingsPath = Path.Combine(RootPath, "settings.json");
        }

        public string RootPath { get; }
        public string SettingsPath => settingsPath;

        public AppSettings Load()
        {
            if (!File.Exists(settingsPath)) return new AppSettings();
            string json = File.ReadAllText(settingsPath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            StoragePath.AtomicWrite(settingsPath, Encoding.UTF8.GetBytes(json));
        }
    }

    internal static class StoragePath
    {
        internal static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A data root is required.", nameof(root));
            string full = Path.GetFullPath(root);
            string pathRoot = Path.GetPathRoot(full);
            if (string.Equals(full, pathRoot, StringComparison.OrdinalIgnoreCase)) return pathRoot;
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        internal static string ResolveUnderRoot(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            string full = Path.GetFullPath(Path.Combine(root, relativePath));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Stored path escapes the data root: " + relativePath);
            return full;
        }

        internal static void AtomicWrite(string destination, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null, true);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
