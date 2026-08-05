using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkspaceFiles.Services
{
    internal static class WorkspaceSettingsService
    {
        private static readonly JsonSerializerOptions _readOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions _writeOptions = new()
        {
            WriteIndented = true
        };

        private sealed class SettingsJson
        {
            [JsonPropertyName("folders")]
            public List<string> Folders { get; set; }
        }

        /// <summary>
        /// Returns the full path to the settings JSON file for the given solution.
        /// For example, "C:\MySolution\MySolution.sln" → "C:\MySolution\MySolution.wsfiles.json".
        /// Works with both .sln and .slnx solution files.
        /// </summary>
        public static string GetSettingsFilePath(string solutionFullPath)
        {
            var dir = Path.GetDirectoryName(solutionFullPath);
            var name = Path.GetFileNameWithoutExtension(solutionFullPath);
            return Path.Combine(dir, name + ".wsfiles.json");
        }

        /// <summary>
        /// Loads workspace folder paths from the settings JSON file.
        /// Returns <c>null</c> if the file does not exist.
        /// Returns an empty list if the file exists but contains no folders.
        /// </summary>
        public static IReadOnlyList<string> LoadFolders(string settingsFilePath)
        {
            if (!File.Exists(settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(settingsFilePath);
            SettingsJson settings = JsonSerializer.Deserialize<SettingsJson>(json, _readOptions);
            return settings?.Folders ?? [];
        }

        /// <summary>
        /// Saves workspace folder paths to the settings JSON file.
        /// Deletes the file if <paramref name="relativePaths"/> is empty,
        /// keeping the repository clean when no custom folders are configured.
        /// </summary>
        public static void SaveFolders(string settingsFilePath, IEnumerable<string> relativePaths)
        {
            var list = new List<string>(relativePaths);

            if (list.Count == 0)
            {
                if (File.Exists(settingsFilePath))
                {
                    File.Delete(settingsFilePath);
                }

                return;
            }

            var settings = new SettingsJson { Folders = list };
            var json = JsonSerializer.Serialize(settings, _writeOptions);
            File.WriteAllText(settingsFilePath, json);
        }

        /// <summary>
        /// Parses the legacy pipe-separated value stored in <c>DTE.Solution.Globals["FileExplorer"]</c>
        /// and returns the individual relative folder paths.
        /// </summary>
        public static IReadOnlyList<string> MigrateFromGlobals(string globalsValue)
        {
            if (string.IsNullOrEmpty(globalsValue))
            {
                return [];
            }

            return globalsValue.Split(['|'], StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
