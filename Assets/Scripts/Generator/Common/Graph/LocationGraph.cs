using System.Collections.Generic;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;

namespace LegendsOfWarAndMagic.Generator.Common.Graph
{
    public sealed class LocationGraph
    {
        private readonly IReadOnlyCollection<WorldLocation> locations;

        public LocationGraph(IReadOnlyCollection<WorldLocation> locations)
        {
            this.locations = locations ?? new List<WorldLocation>();
        }

        public bool IsConnected()
        {
            if (locations.Count <= 1)
            {
                return true;
            }

            var byId = locations.ToDictionary(location => location.Id.Value);
            var visited = new HashSet<string>();
            var queue = new Queue<WorldLocation>();
            var first = locations.First();
            visited.Add(first.Id.Value);
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var connection in current.Connections)
                {
                    if (!byId.TryGetValue(connection.To.Value, out var next) || !visited.Add(next.Id.Value))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return visited.Count == locations.Count;
        }

        public bool HasSymmetricConnections()
        {
            var byId = locations.ToDictionary(location => location.Id.Value);
            foreach (var location in locations)
            {
                foreach (var connection in location.Connections)
                {
                    if (!byId.TryGetValue(connection.To.Value, out var target))
                    {
                        return false;
                    }

                    var opposite = Opposite(connection.Direction);
                    var reverseFound = target.Connections.Any(reverse =>
                        reverse.To.Equals(location.Id) &&
                        reverse.Direction == opposite);
                    if (!reverseFound)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static WorldDirection Opposite(WorldDirection direction)
        {
            return direction switch
            {
                WorldDirection.North => WorldDirection.South,
                WorldDirection.South => WorldDirection.North,
                WorldDirection.East => WorldDirection.West,
                WorldDirection.West => WorldDirection.East,
                WorldDirection.NorthEast => WorldDirection.SouthWest,
                WorldDirection.NorthWest => WorldDirection.SouthEast,
                WorldDirection.SouthEast => WorldDirection.NorthWest,
                WorldDirection.SouthWest => WorldDirection.NorthEast,
                _ => WorldDirection.South
            };
        }
    }

    public sealed class RegionGraph
    {
        public RegionGraph(IReadOnlyCollection<WorldRegion> regions)
        {
            Regions = regions ?? new List<WorldRegion>();
        }

        public IReadOnlyCollection<WorldRegion> Regions { get; }
    }
}
