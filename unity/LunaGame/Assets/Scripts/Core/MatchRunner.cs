using System;
using System.Collections.Generic;
using System.Linq;

namespace LunaGame.Core
{
    public enum AiStyle { Rush, Ranged, Raid, Defense }

    public sealed class DeploymentPlan
    {
        public DeploymentPlan(UnitKind kind, BoardPosition position) { Kind = kind; Position = position; }
        public UnitKind Kind { get; }
        public BoardPosition Position { get; }
    }

    internal enum CpuActionKind { Idle, Knight, Attack, Move }

    internal sealed class CpuAction
    {
        public CpuAction(MatchUnit unit, CpuActionKind kind, MatchUnit target = null, BoardPosition? destination = null)
        { Unit = unit; Kind = kind; Target = target; Destination = destination; }
        public MatchUnit Unit { get; }
        public CpuActionKind Kind { get; }
        public MatchUnit Target { get; }
        public BoardPosition? Destination { get; }
    }

    public sealed class MatchRunner
    {
        public MatchRunner(uint seed, AiStyle p1 = AiStyle.Rush, AiStyle p2 = AiStyle.Ranged)
        { Game = new MatchGame(seed); P1Style = p1; P2Style = p2; }

        public MatchGame Game { get; }
        public AiStyle P1Style { get; }
        public AiStyle P2Style { get; }
        public bool Finished => Game.Winner.HasValue || Game.DrawType != null;

        public void Step()
        {
            if (Finished) return;
            Game.AdvanceTick();
            if (Finished) return;
            var phase = Game.Now / MatchGame.ActionTick + (int)Game.Seed;

            Deploy(Side.P1, P1Style, phase);
            Deploy(Side.P2, P2Style, phase);

            var actions = Game.Living().Select(unit => ChooseAction(unit, Style(unit.Side), phase)).ToArray();
            foreach (var action in actions.Where(action => action.Kind == CpuActionKind.Knight && action.Unit.Alive))
                Game.KnightJump(action.Unit);

            Game.ResolveAttacks(actions
                .Where(action => action.Kind == CpuActionKind.Attack && action.Target != null)
                .Select(action => new AttackOrder(action.Unit, action.Target)));

            Game.ResolveMoves(actions
                .Where(action => action.Kind == CpuActionKind.Move && action.Destination.HasValue)
                .Select(action => new MoveOrder(action.Unit, action.Destination.Value)));
            Game.ResolveObjectives();
        }

        public void RunToCompletion(int maximumSteps = 100)
        {
            for (var step = 0; step < maximumSteps && !Finished; step++) Step();
            if (!Finished) throw new InvalidOperationException("Match did not finish within the step limit.");
        }

        public DeploymentPlan PlanDeployment(Side side, AiStyle style, int phase)
        {
            if (Game.Living(side).Count() >= MatchGame.MaxUnits) return null;
            var kind = RosterChoice(side, style, phase);
            if (!kind.HasValue) return null;
            var lane = style == AiStyle.Defense ? DefenseLane(side) ?? TargetLane(side, style, phase) : TargetLane(side, style, phase);
            if (style == AiStyle.Defense && Game.Living(side).Count(unit => unit.Position.Y == lane && !unit.Locked) >= 2)
                lane = TargetLane(side, AiStyle.Rush, phase);
            var edge = side == Side.P1 ? 3 : 4;
            var cell = Game.SpawnCells(side)
                .OrderBy(position => Game.Living(side).Any(unit => unit.Locked && unit.Position.Y == position.Y) ? 1 : 0)
                .ThenBy(position => Math.Abs(position.Y - lane))
                .ThenBy(position => LaneLoad(side, position.Y))
                .ThenBy(position => Math.Abs(position.X - edge))
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X)
                .FirstOrDefault();
            return Game.SpawnCells(side).Count == 0 ? null : new DeploymentPlan(kind.Value, cell);
        }

        private void Deploy(Side side, AiStyle style, int phase)
        {
            var plan = PlanDeployment(side, style, phase);
            if (plan != null) Game.AddUnit(side, plan.Kind, plan.Position);
        }

        private CpuAction ChooseAction(MatchUnit unit, AiStyle style, int phase)
        {
            if (!unit.Alive || unit.Locked) return new CpuAction(unit, CpuActionKind.Idle);
            if (unit.Kind == UnitKind.Knight && Game.Now >= unit.NextAction)
            {
                var forward = unit.Side == Side.P1 ? 1 : -1;
                var landing = new BoardPosition(unit.Position.X + 2 * forward, unit.Position.Y);
                var occupant = Game.UnitAt(landing);
                if (landing.X >= 0 && landing.X < MatchGame.Width && (occupant == null || occupant.Side != unit.Side))
                    return new CpuAction(unit, CpuActionKind.Knight);
            }
            var enemies = Game.Attackables(unit);
            if (enemies.Count > 0 && Game.Now >= unit.NextAttack)
                return new CpuAction(unit, CpuActionKind.Attack, Game.SelectTarget(unit, enemies));
            if (Game.Now < unit.NextAction) return new CpuAction(unit, CpuActionKind.Idle);
            var move = Game.LegalMoves(unit)
                .OrderByDescending(position => MoveScore(unit, position, style, phase))
                .FirstOrDefault();
            return Game.LegalMoves(unit).Count == 0
                ? new CpuAction(unit, CpuActionKind.Idle)
                : new CpuAction(unit, CpuActionKind.Move, destination: move);
        }

        private UnitKind? RosterChoice(Side side, AiStyle style, int phase)
        {
            var points = Game.Player(side).Points;
            var active = Game.Living(side).Count();
            UnitKind[] preference;
            if (style == AiStyle.Rush)
            {
                preference = new[] { UnitKind.Lance, UnitKind.Knight, UnitKind.Pawn };
                if (active >= 6 && points < 2) return null;
            }
            else if (style == AiStyle.Ranged)
            {
                if (points < 3 && !Game.Living(side).Any(unit => unit.Kind == UnitKind.Archer || unit.Kind == UnitKind.Gold)) return null;
                preference = phase % 4 == 0
                    ? new[] { UnitKind.Gold, UnitKind.Archer, UnitKind.Pawn }
                    : new[] { UnitKind.Archer, UnitKind.Pawn, UnitKind.Gold };
                if (active >= 5 && points < 3) return null;
            }
            else if (style == AiStyle.Raid)
            {
                preference = phase % 4 == 0
                    ? new[] { UnitKind.Gold, UnitKind.Knight, UnitKind.Lance, UnitKind.Pawn }
                    : new[] { UnitKind.Knight, UnitKind.Lance, UnitKind.Pawn, UnitKind.Gold };
                if (active >= 5 && points < 2) return null;
            }
            else
            {
                if (points < 3 && !Game.Living(side).Any(unit => unit.Kind == UnitKind.Archer || unit.Kind == UnitKind.Gold)) return null;
                preference = phase % 3 == 0
                    ? new[] { UnitKind.Gold, UnitKind.Archer, UnitKind.Pawn }
                    : new[] { UnitKind.Archer, UnitKind.Gold, UnitKind.Pawn };
                if (active >= 5 && points < 3) return null;
            }
            return preference.Cast<UnitKind?>().FirstOrDefault(kind => MatchGame.Specs[kind.Value].Cost <= points);
        }

        private int TargetLane(Side side, AiStyle style, int phase)
        {
            var enemy = side == Side.P1 ? Side.P2 : Side.P1;
            var objectives = Game.Objectives.Where(objective => objective.Side == enemy && !objective.Captured).ToArray();
            var towers = objectives.Where(objective => objective.Kind == ObjectiveKind.Tower).ToArray();
            var pool = style == AiStyle.Raid && towers.Length > 0 ? towers : objectives;
            if (pool.Length == 0) return 2;
            return pool
                .OrderBy(objective => LaneLoad(side, objective.Lane) * 3 + Math.Abs(objective.Lane - 2) + (objective.Kind == ObjectiveKind.Tower ? -3 : 0))
                .ThenBy(objective => (objective.Lane + phase) % MatchGame.Height)
                .First().Lane;
        }

        private int? DefenseLane(Side side)
        {
            var enemy = side == Side.P1 ? Side.P2 : Side.P1;
            return Game.Living(enemy)
                .Select(unit => new { unit.Position.Y, Progress = side == Side.P1 ? MatchGame.Width - 1 - unit.Position.X : unit.Position.X })
                .OrderByDescending(threat => threat.Progress)
                .Select(threat => (int?)threat.Y)
                .FirstOrDefault();
        }

        private int MoveScore(MatchUnit unit, BoardPosition position, AiStyle style, int phase)
        {
            var forward = unit.Side == Side.P1 ? 1 : -1;
            var progress = (position.X - unit.Position.X) * forward;
            var score = progress * 8 - Math.Abs(position.Y - TargetLane(unit.Side, style, phase)) * 4 - LaneLoad(unit.Side, position.Y) * 2;
            if (style == AiStyle.Rush) score += progress * 7;
            if (style == AiStyle.Raid) score += progress * 3;
            if (style == AiStyle.Defense) score -= Math.Max(0, progress) * 2;
            if (style == AiStyle.Ranged && unit.Kind == UnitKind.Archer) score -= Math.Max(0, progress) * 4;
            return score;
        }

        private int LaneLoad(Side side, int lane) => Game.Living(side).Count(unit => unit.Position.Y == lane && !unit.Locked);
        private AiStyle Style(Side side) => side == Side.P1 ? P1Style : P2Style;
    }
}
