using System;
using System.Collections.Generic;
using System.Linq;

namespace LunaGame.Core
{
    public enum Side
    {
        P1 = 1,
        P2 = 2
    }

    public enum ObjectiveKind
    {
        Tower,
        Outpost
    }

    public sealed class Objective
    {
        public Objective(Side side, int lane, ObjectiveKind kind)
        {
            Side = side;
            Lane = lane;
            Kind = kind;
        }

        public Side Side { get; }
        public int Lane { get; }
        public ObjectiveKind Kind { get; }
    }

    /// <summary>
    /// Byte-for-byte equivalent state transition to the JavaScript LCG in game_core.js.
    /// This is intentionally not System.Random so shared Seeds reproduce across clients.
    /// </summary>
    public sealed class LunaRng
    {
        private uint state;

        public LunaRng(uint seed)
        {
            state = seed == 0 ? 1u : seed;
        }

        public double Next()
        {
            unchecked
            {
                state = 1664525u * state + 1013904223u;
            }

            return state / 4294967296.0;
        }

        public int Int(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            return (int)Math.Floor(Next() * exclusiveMax);
        }

        public IReadOnlyList<T> Sample<T>(IReadOnlyList<T> source, int count)
        {
            if (count < 0 || count > source.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var values = source.ToList();
            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = Int(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            return values.Take(count).ToArray();
        }
    }

    public sealed class DestinyGame
    {
        private static readonly int[] Lanes = { 0, 1, 2, 3, 4 };

        public DestinyGame(uint seed)
        {
            Seed = seed == 0 ? 1u : seed;
            var rng = new LunaRng(Seed);
            TowerCount = 1 + rng.Int(4);
            var p1Towers = rng.Sample(Lanes, TowerCount);
            var p2Towers = rng.Sample(Lanes, TowerCount);
            var objectives = new List<Objective>(10);

            foreach (var lane in Lanes)
            {
                objectives.Add(new Objective(
                    Side.P1,
                    lane,
                    p1Towers.Contains(lane) ? ObjectiveKind.Tower : ObjectiveKind.Outpost));
                objectives.Add(new Objective(
                    Side.P2,
                    lane,
                    p2Towers.Contains(lane) ? ObjectiveKind.Tower : ObjectiveKind.Outpost));
            }

            Objectives = objectives;
        }

        public uint Seed { get; }
        public int TowerCount { get; }
        public IReadOnlyList<Objective> Objectives { get; }

        public IReadOnlyList<int> TowerLanes(Side side)
        {
            return Objectives
                .Where(objective => objective.Side == side && objective.Kind == ObjectiveKind.Tower)
                .Select(objective => objective.Lane)
                .ToArray();
        }
    }
}

