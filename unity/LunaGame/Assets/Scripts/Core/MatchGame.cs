using System;
using System.Collections.Generic;
using System.Linq;

namespace LunaGame.Core
{
    public enum UnitKind { Pawn, Gold, Silver, Knight, Lance, Archer, Bishop, Rook }

    public readonly struct BoardPosition : IEquatable<BoardPosition>
    {
        public BoardPosition(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public bool Equals(BoardPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object value) => value is BoardPosition other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"{X},{Y}";
    }

    public sealed class UnitSpec
    {
        public UnitSpec(int cost, int hp, int attack, int cooldown, int range, bool ranged)
        { Cost = cost; Hp = hp; Attack = attack; Cooldown = cooldown; Range = range; Ranged = ranged; }
        public int Cost { get; }
        public int Hp { get; }
        public int Attack { get; }
        public int Cooldown { get; }
        public int Range { get; }
        public bool Ranged { get; }
    }

    public sealed class MatchUnit
    {
        internal MatchUnit(int uid, Side side, UnitKind kind, BoardPosition position, int hp, int readyAt)
        { Uid = uid; Side = side; Kind = kind; Position = position; Hp = hp; NextAction = readyAt; NextAttack = readyAt; Alive = true; }
        public int Uid { get; }
        public Side Side { get; }
        public UnitKind Kind { get; }
        public BoardPosition Position { get; internal set; }
        public int Hp { get; internal set; }
        public int NextAction { get; internal set; }
        public int NextAttack { get; internal set; }
        public bool Alive { get; internal set; }
        public bool Locked { get; internal set; }
    }

    public sealed class MatchPlayer
    {
        internal MatchPlayer() { Points = MatchGame.StartPoints; }
        public int Points { get; internal set; }
        public int Towers { get; internal set; }
    }

    public sealed class AttackOrder
    {
        public AttackOrder(MatchUnit attacker, MatchUnit target)
        { Attacker = attacker; Target = target; }
        public MatchUnit Attacker { get; }
        public MatchUnit Target { get; }
    }

    public sealed class MoveOrder
    {
        public MoveOrder(MatchUnit unit, BoardPosition destination)
        { Unit = unit; Destination = destination; }
        public MatchUnit Unit { get; }
        public BoardPosition Destination { get; }
    }

    public sealed class MatchGame
    {
        public const int Width = 8;
        public const int Height = 5;
        public const int StartPoints = 4;
        public const int MaxPoints = 10;
        public const int MaxUnits = 10;
        public const int ActionTick = 3;
        public const int CaptureSeconds = 3;
        public const int MatchLimitSeconds = 180;

        public static readonly IReadOnlyDictionary<UnitKind, UnitSpec> Specs =
            new Dictionary<UnitKind, UnitSpec>
            {
                [UnitKind.Pawn] = new UnitSpec(1, 1, 1, 3, 1, false),
                [UnitKind.Gold] = new UnitSpec(3, 3, 1, 3, 1, false),
                [UnitKind.Silver] = new UnitSpec(3, 3, 1, 3, 1, false),
                [UnitKind.Knight] = new UnitSpec(2, 1, 1, 3, 1, false),
                [UnitKind.Lance] = new UnitSpec(2, 1, 1, 3, 1, false),
                [UnitKind.Archer] = new UnitSpec(3, 2, 1, 6, 2, true),
                [UnitKind.Bishop] = new UnitSpec(4, 2, 1, 3, 1, false),
                [UnitKind.Rook] = new UnitSpec(4, 2, 2, 3, 1, false)
            };

        private readonly List<MatchUnit> units = new List<MatchUnit>();
        private readonly Dictionary<Side, MatchPlayer> players = new Dictionary<Side, MatchPlayer>
        { [Side.P1] = new MatchPlayer(), [Side.P2] = new MatchPlayer() };
        private int nextUid = 1;

        public MatchGame(uint seed)
        {
            var destiny = new DestinyGame(seed);
            Seed = destiny.Seed;
            TowerCount = destiny.TowerCount;
            Objectives = destiny.Objectives.Select(o => new Objective(o.Side, o.Lane, o.Kind)).ToArray();
        }

        public uint Seed { get; }
        public int TowerCount { get; }
        public int Now { get; private set; }
        public Side? Winner { get; private set; }
        public string WinType { get; private set; }
        public string DrawType { get; private set; }
        public IReadOnlyList<Objective> Objectives { get; }
        public IReadOnlyList<MatchUnit> Units => units;
        public MatchPlayer Player(Side side) => players[side];

        public MatchUnit UnitAt(BoardPosition position) => units.FirstOrDefault(u => u.Alive && u.Position.Equals(position));
        public IEnumerable<MatchUnit> Living(Side? side = null) => units.Where(u => u.Alive && (!side.HasValue || u.Side == side.Value));

        public IReadOnlyList<BoardPosition> SpawnCells(Side side)
        {
            var firstColumn = side == Side.P1 ? 0 : 4;
            var result = new List<BoardPosition>();
            for (var x = firstColumn; x < firstColumn + 4; x++)
                for (var y = 0; y < Height; y++)
                {
                    var position = new BoardPosition(x, y);
                    if (UnitAt(position) == null) result.Add(position);
                }
            return result;
        }

        public MatchUnit AddUnit(Side side, UnitKind kind, BoardPosition position, bool spend = true, bool ready = false)
        {
            if (!InBounds(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (UnitAt(position) != null) throw new InvalidOperationException("occupied");
            if (Living(side).Count() >= MaxUnits) throw new InvalidOperationException("cap");
            var spec = Specs[kind];
            if (spend)
            {
                if (players[side].Points < spec.Cost) throw new InvalidOperationException("points");
                players[side].Points -= spec.Cost;
            }
            var readyAt = ready ? Now : Now + ActionTick;
            var unit = new MatchUnit(nextUid++, side, kind, position, spec.Hp, readyAt);
            units.Add(unit);
            return unit;
        }

        public IReadOnlyList<BoardPosition> LegalMoves(MatchUnit unit)
        {
            if (!unit.Alive || unit.Locked) return Array.Empty<BoardPosition>();
            var candidates = new List<BoardPosition>();
            var x = unit.Position.X; var y = unit.Position.Y; var forward = unit.Side == Side.P1 ? 1 : -1;
            if (unit.Kind == UnitKind.Pawn || unit.Kind == UnitKind.Silver || unit.Kind == UnitKind.Archer)
                candidates.Add(new BoardPosition(x + forward, y));
            else if (unit.Kind == UnitKind.Gold)
                for (var dx = -1; dx <= 1; dx++) for (var dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) candidates.Add(new BoardPosition(x + dx, y + dy));
            else if (unit.Kind == UnitKind.Knight)
                candidates.Add(new BoardPosition(x + 2 * forward, y));
            else if (unit.Kind == UnitKind.Lance)
                AddRay(unit, candidates, forward, 0, 2);
            else if (unit.Kind == UnitKind.Bishop)
                foreach (var direction in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }) AddRay(unit, candidates, direction.Item1, direction.Item2, 5);
            else if (unit.Kind == UnitKind.Rook)
                foreach (var direction in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) AddRay(unit, candidates, direction.Item1, direction.Item2, 5);
            return candidates.Where(position => InBounds(position) && UnitAt(position) == null).ToArray();
        }

        public IReadOnlyList<MatchUnit> Attackables(MatchUnit unit)
        {
            if (!unit.Alive || unit.Locked) return Array.Empty<MatchUnit>();
            var x = unit.Position.X; var y = unit.Position.Y;
            var forward = unit.Side == Side.P1 ? 1 : -1;
            return Living().Where(enemy =>
            {
                if (enemy.Side == unit.Side) return false;
                var dx = enemy.Position.X - x; var dy = enemy.Position.Y - y;
                if (unit.Kind == UnitKind.Pawn || unit.Kind == UnitKind.Silver) return dx == forward && dy == 0;
                if (unit.Kind == UnitKind.Gold) return Math.Max(Math.Abs(dx), Math.Abs(dy)) == 1;
                if (unit.Kind == UnitKind.Knight) return false;
                if (unit.Kind == UnitKind.Lance) return dx == forward && Math.Abs(dy) <= 1;
                if (unit.Kind == UnitKind.Archer) return Math.Abs(dx) + Math.Abs(dy) > 0 && Math.Abs(dx) + Math.Abs(dy) <= 2;
                if (unit.Kind == UnitKind.Bishop) return Math.Abs(dx) == 1 && Math.Abs(dy) == 1;
                return unit.Kind == UnitKind.Rook &&
                    ((Math.Abs(dx) == 1 && dy == 0) || (Math.Abs(dy) == 1 && dx == 0));
            }).ToArray();
        }

        public MatchUnit SelectTarget(MatchUnit attacker, IEnumerable<MatchUnit> enemies)
        {
            return enemies
                .OrderBy(enemy => (double)enemy.Hp / Specs[enemy.Kind].Hp)
                .ThenBy(enemy => attacker.Side == Side.P1 ? enemy.Position.X : Width - 1 - enemy.Position.X)
                .ThenBy(enemy => Math.Abs(enemy.Position.X - attacker.Position.X) + Math.Abs(enemy.Position.Y - attacker.Position.Y))
                .ThenBy(enemy => enemy.Uid)
                .FirstOrDefault();
        }

        public MatchUnit KnightLandingTarget(MatchUnit unit)
        {
            if (!unit.Alive || unit.Kind != UnitKind.Knight) return null;
            var forward = unit.Side == Side.P1 ? 1 : -1;
            var landing = new BoardPosition(unit.Position.X + 2 * forward, unit.Position.Y);
            if (!InBounds(landing)) return null;
            var occupant = UnitAt(landing);
            return occupant != null && occupant.Side != unit.Side ? occupant : null;
        }

        public void KnightJump(MatchUnit unit)
        {
            if (!unit.Alive || unit.Locked || unit.Kind != UnitKind.Knight) throw new InvalidOperationException("not a movable knight");
            if (Now < unit.NextAction) throw new InvalidOperationException("not ready");
            var forward = unit.Side == Side.P1 ? 1 : -1;
            var landing = new BoardPosition(unit.Position.X + 2 * forward, unit.Position.Y);
            if (!InBounds(landing)) throw new InvalidOperationException("illegal jump");
            var occupant = UnitAt(landing);
            if (occupant != null && occupant.Side == unit.Side) throw new InvalidOperationException("occupied");
            if (occupant != null)
            {
                occupant.Hp -= Specs[unit.Kind].Attack;
                if (occupant.Hp <= 0)
                {
                    occupant.Alive = false;
                    unit.Position = landing;
                    StartCapture(unit);
                }
            }
            else
            {
                unit.Position = landing;
                StartCapture(unit);
            }
            unit.NextAction = Now + ActionTick;
        }

        public void ResolveAttacks(IEnumerable<AttackOrder> orders)
        {
            var positions = units.ToDictionary(unit => unit.Uid, unit => unit.Position);
            var damage = new Dictionary<int, int>();
            var valid = new List<AttackOrder>();
            foreach (var order in orders)
            {
                if (!order.Attacker.Alive || !order.Target.Alive || Now < order.Attacker.NextAttack) continue;
                damage[order.Target.Uid] = (damage.TryGetValue(order.Target.Uid, out var current) ? current : 0) + Specs[order.Attacker.Kind].Attack;
                order.Attacker.NextAttack = Now + Specs[order.Attacker.Kind].Cooldown;
                order.Attacker.NextAction = Now + ActionTick;
                valid.Add(order);
            }
            var dead = new HashSet<int>();
            foreach (var entry in damage)
            {
                var target = units.First(unit => unit.Uid == entry.Key);
                target.Hp -= entry.Value;
                if (target.Hp <= 0) dead.Add(target.Uid);
            }
            foreach (var uid in dead) units.First(unit => unit.Uid == uid).Alive = false;

            foreach (var claim in valid
                .Where(order => order.Attacker.Alive && dead.Contains(order.Target.Uid) && !Specs[order.Attacker.Kind].Ranged)
                .GroupBy(order => positions[order.Target.Uid]))
            {
                if (UnitAt(claim.Key) != null) continue;
                var winner = claim.OrderBy(order => order.Attacker.Uid).First().Attacker;
                winner.Position = claim.Key;
                StartCapture(winner);
            }
        }

        public void Move(MatchUnit unit, BoardPosition destination)
        {
            if (Now < unit.NextAction) throw new InvalidOperationException("not ready");
            if (!LegalMoves(unit).Contains(destination)) throw new InvalidOperationException("illegal move");
            unit.Position = destination;
            unit.NextAction = Now + ActionTick;
            StartCapture(unit);
        }

        public void ResolveMoves(IEnumerable<MoveOrder> orders)
        {
            foreach (var claim in orders
                .Where(order => order.Unit.Alive && !order.Unit.Locked && Now >= order.Unit.NextAction && LegalMoves(order.Unit).Contains(order.Destination))
                .GroupBy(order => order.Destination))
            {
                if (UnitAt(claim.Key) != null) continue;
                var winner = claim.OrderBy(order => order.Unit.Uid).First().Unit;
                winner.Position = claim.Key;
                winner.NextAction = Now + ActionTick;
            }
        }

        public void ResolveObjectives()
        {
            foreach (var unit in Living()) StartCapture(unit);
            UpdateCaptures();
        }

        public void AdvanceTick()
        {
            if (Winner.HasValue || DrawType != null) return;
            Now += ActionTick;
            foreach (var player in players.Values) player.Points = Math.Min(MaxPoints, player.Points + 1);
            UpdateCaptures();
            if (!Winner.HasValue && DrawType == null) CheckTimeout();
        }

        public int ObjectiveScore(Side side) => Objectives.Count(o => o.Captured && o.CaptureSide == side);

        private void StartCapture(MatchUnit unit)
        {
            var objective = Objectives.FirstOrDefault(o => o.X == unit.Position.X && o.Lane == unit.Position.Y && !o.Captured && o.Side != unit.Side);
            if (objective != null && objective.CaptureSide != unit.Side)
            { objective.CaptureSide = unit.Side; objective.CaptureStart = Now; }
        }

        private void UpdateCaptures()
        {
            foreach (var objective in Objectives.Where(o => !o.Captured && o.CaptureStart.HasValue))
            {
                var holder = UnitAt(new BoardPosition(objective.X, objective.Lane));
                if (holder == null || holder.Side != objective.CaptureSide)
                { objective.CaptureSide = null; objective.CaptureStart = null; continue; }
                if (Now - objective.CaptureStart.Value < CaptureSeconds) continue;
                objective.Captured = true;
                holder.Locked = true;
                if (objective.Kind == ObjectiveKind.Tower) players[holder.Side].Towers++;
            }
            var p1 = players[Side.P1].Towers >= TowerCount;
            var p2 = players[Side.P2].Towers >= TowerCount;
            if (p1 && p2) DrawType = "SIMULTANEOUS_TOWER_DRAW";
            else if (p1) Winner = Side.P1;
            else if (p2) Winner = Side.P2;
        }

        private void CheckTimeout()
        {
            if (Now < MatchLimitSeconds) return;
            var p1 = ObjectiveScore(Side.P1); var p2 = ObjectiveScore(Side.P2);
            if (p1 > p2) { Winner = Side.P1; WinType = "TIMEOUT"; }
            else if (p2 > p1) { Winner = Side.P2; WinType = "TIMEOUT"; }
            else DrawType = "TIMEOUT_DRAW";
        }

        private void AddRay(MatchUnit unit, ICollection<BoardPosition> output, int dx, int dy, int maxDistance)
        {
            for (var distance = 1; distance <= maxDistance; distance++)
            {
                var position = new BoardPosition(unit.Position.X + dx * distance, unit.Position.Y + dy * distance);
                if (!InBounds(position) || UnitAt(position) != null) break;
                output.Add(position);
            }
        }

        private static bool InBounds(BoardPosition position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    }
}
