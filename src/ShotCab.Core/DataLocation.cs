using System;
using System.IO;

namespace ShotCab.Core
{
    public sealed class DataLocation
    {
        public bool IsInstalled { get; }
        public string RootPath { get; }
        public string PointerPath { get; }

        private DataLocation(bool installed, string rootPath, string pointerPath)
        {
            IsInstalled = installed;
            RootPath = rootPath;
            PointerPath = pointerPath;
        }

        public static DataLocation Resolve(string applicationDirectory, string localApplicationDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(applicationDirectory)) throw new ArgumentException("Application directory is required.", nameof(applicationDirectory));
            if (string.IsNullOrWhiteSpace(localApplicationDataDirectory)) throw new ArgumentException("Local application data directory is required.", nameof(localApplicationDataDirectory));

            string application = Path.GetFullPath(applicationDirectory);
            bool installed = File.Exists(Path.Combine(application, "installed.mode"));
            string userDirectory = installed ? Path.Combine(Path.GetFullPath(localApplicationDataDirectory), "ShotCab") : application;
            string pointer = Path.Combine(userDirectory, "data-location.txt");
            string root = installed ? Path.Combine(userDirectory, "Data") : Path.Combine(application, "ShotCab.Data");

            if (File.Exists(pointer))
            {
                string chosen = File.ReadAllText(pointer).Trim();
                if (!Path.IsPathRooted(chosen)) throw new IOException("The configured ShotCab data directory must be absolute: " + pointer);
                root = Path.GetFullPath(chosen);
            }

            return new DataLocation(installed, root, pointer);
        }

        public void WritePointer(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathRooted(rootPath)) throw new ArgumentException("Data directory must be absolute.", nameof(rootPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PointerPath));
            File.WriteAllText(PointerPath, Path.GetFullPath(rootPath));
        }
    }
}
