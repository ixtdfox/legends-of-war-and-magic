using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class DebugArtifactWriter
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        public DebugArtifactWriter(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            CreateDirectory(rootDirectory);
        }

        public string RootDirectory { get; }

        public string Combine(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return RootDirectory;
            }

            var path = RootDirectory;
            for (var i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    path = Path.Combine(path, SanitizePathPart(parts[i]));
                }
            }

            return path;
        }

        public bool CreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"DebugTools: failed to create directory '{path}': {exception.Message}");
                return false;
            }
        }

        public bool WriteText(string path, string text)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, text ?? string.Empty, Utf8NoBom);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"DebugTools: failed to write '{path}': {exception.Message}");
                return false;
            }
        }

        public bool WriteJson(string path, object value, bool pretty = true)
        {
            return WriteText(path, DebugJson.ToJson(value, pretty));
        }

        public bool AppendLine(string path, string line)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, (line ?? string.Empty) + Environment.NewLine, Utf8NoBom);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"DebugTools: failed to append '{path}': {exception.Message}");
                return false;
            }
        }

        public static string SanitizePathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '-' : c);
            }

            return builder.ToString().Trim();
        }
    }
}
