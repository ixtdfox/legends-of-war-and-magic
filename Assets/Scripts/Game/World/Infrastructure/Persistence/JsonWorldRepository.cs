using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Ports;
using UnityEngine;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Persistence
{
    public sealed class JsonWorldRepository : IWorldRepository
    {
        private const string WorldFileName = "world.json";
        private const string ConfigFileName = "generator_config.json";
        private readonly string saveRootPath;

        public JsonWorldRepository()
            : this(Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", "Worlds"))
        {
        }

        public JsonWorldRepository(string saveRootPath)
        {
            this.saveRootPath = string.IsNullOrWhiteSpace(saveRootPath)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", "Worlds")
                : saveRootPath;
        }

        public string SaveRootPath => saveRootPath;

        public string GetWorldDirectory(WorldId worldId)
        {
            return Path.Combine(saveRootPath, worldId.Value);
        }

        public void Save(GeneratedWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var worldDirectory = GetWorldDirectory(world.Id);
            Directory.CreateDirectory(worldDirectory);
            Directory.CreateDirectory(Path.Combine(worldDirectory, "Maps"));
            Directory.CreateDirectory(Path.Combine(worldDirectory, "Locations"));

            var dto = WorldSaveMapper.ToDto(world);
            var json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(Path.Combine(worldDirectory, WorldFileName), json);
            File.WriteAllText(Path.Combine(worldDirectory, ConfigFileName), world.Metadata.ConfigSnapshot ?? string.Empty);
        }

        public GeneratedWorld Load(WorldId worldId)
        {
            var path = Path.Combine(GetWorldDirectory(worldId), WorldFileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Generated world save not found: {path}", path);
            }

            var dto = JsonUtility.FromJson<WorldSaveDto>(File.ReadAllText(path));
            return WorldSaveMapper.ToDomain(dto);
        }

        public IReadOnlyList<WorldSaveSummary> ListWorlds()
        {
            if (!Directory.Exists(saveRootPath))
            {
                return Array.Empty<WorldSaveSummary>();
            }

            var summaries = new List<WorldSaveSummary>();
            foreach (var directory in Directory.GetDirectories(saveRootPath))
            {
                var path = Path.Combine(directory, WorldFileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var dto = JsonUtility.FromJson<WorldSaveDto>(File.ReadAllText(path));
                    if (dto == null)
                    {
                        continue;
                    }

                    summaries.Add(new WorldSaveSummary(
                        dto.id,
                        dto.name,
                        dto.seed,
                        Enum.TryParse<WorldShapeType>(dto.shapeType, true, out var shape) ? shape : WorldShapeType.HugeIsland,
                        DateTime.TryParse(dto.createdUtc, out var created) ? created.ToUniversalTime() : DateTime.MinValue,
                        dto.worldMapPath));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to read generated world summary at {path}: {exception.Message}");
                }
            }

            return summaries
                .OrderByDescending(summary => summary.CreatedUtc)
                .ToArray();
        }

        public bool Exists(WorldId worldId)
        {
            return File.Exists(Path.Combine(GetWorldDirectory(worldId), WorldFileName));
        }
    }
}
