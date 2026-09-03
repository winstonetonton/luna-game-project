using System.Linq;
using LunaGame.Core;
using NUnit.Framework;

namespace LunaGame.Core.Tests
{
    public sealed class DestinyCompatibilityTests
    {
        [TestCase(1u, 1, "4", "2")]
        [TestCase(36u, 2, "3,4", "0,4")]
        [TestCase(682u, 3, "1,2,4", "0,3,4")]
        [TestCase(1327u, 4, "0,1,2,4", "0,1,2,3")]
        public void DestinyMatchesJavaScriptFixtures(
            uint seed,
            int expectedTowerCount,
            string expectedP1,
            string expectedP2)
        {
            var game = new DestinyGame(seed);

            Assert.That(game.TowerCount, Is.EqualTo(expectedTowerCount));
            Assert.That(string.Join(",", game.TowerLanes(Side.P1)), Is.EqualTo(expectedP1));
            Assert.That(string.Join(",", game.TowerLanes(Side.P2)), Is.EqualTo(expectedP2));
        }

        [Test]
        public void SameSeedProducesSameDestiny()
        {
            var first = new DestinyGame(123456789u);
            var second = new DestinyGame(123456789u);

            Assert.That(first.TowerCount, Is.EqualTo(second.TowerCount));
            Assert.That(first.TowerLanes(Side.P1), Is.EqualTo(second.TowerLanes(Side.P1)));
            Assert.That(first.TowerLanes(Side.P2), Is.EqualTo(second.TowerLanes(Side.P2)));
        }

        [Test]
        public void TowerCountAlwaysStaysWithinCanonicalRange()
        {
            for (uint seed = 1; seed <= 10000; seed++)
            {
                var game = new DestinyGame(seed);
                Assert.That(game.TowerCount, Is.InRange(1, 4), $"Seed {seed}");
                Assert.That(game.Objectives.Count, Is.EqualTo(10));
                Assert.That(game.TowerLanes(Side.P1).Distinct().Count(), Is.EqualTo(game.TowerCount));
                Assert.That(game.TowerLanes(Side.P2).Distinct().Count(), Is.EqualTo(game.TowerCount));
            }
        }

        [Test]
        public void PawnMovesForwardAndCapturesEnemyTowerAfterThreeSeconds()
        {
            var game = new MatchGame(1u);
            var target = game.Objectives.First(o => o.Side == Side.P2 && o.Kind == ObjectiveKind.Tower);
            var unit = game.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(6, target.Lane), false, true);

            game.Move(unit, new BoardPosition(7, target.Lane));
            game.AdvanceTick();

            Assert.That(target.Captured, Is.True);
            Assert.That(unit.Locked, Is.True);
            Assert.That(game.Winner, Is.EqualTo(Side.P1));
        }

        [Test]
        public void TimeoutUsesAllCapturedObjectivesNotOnlyTowers()
        {
            var game = new MatchGame(1327u);
            var outpost = game.Objectives.First(o => o.Side == Side.P2 && o.Kind == ObjectiveKind.Outpost);
            var unit = game.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(6, outpost.Lane), false, true);
            game.Move(unit, new BoardPosition(7, outpost.Lane));

            for (var i = 0; i < MatchGame.MatchLimitSeconds / MatchGame.ActionTick; i++) game.AdvanceTick();

            Assert.That(game.Winner, Is.EqualTo(Side.P1));
            Assert.That(game.WinType, Is.EqualTo("TIMEOUT"));
        }

        [Test]
        public void SimultaneousMeleeAttacksCanKillBothUnits()
        {
            var game = new MatchGame(36u);
            var p1 = game.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(3, 2), false, true);
            var p2 = game.AddUnit(Side.P2, UnitKind.Pawn, new BoardPosition(4, 2), false, true);

            game.ResolveAttacks(new[] { new AttackOrder(p1, p2), new AttackOrder(p2, p1) });

            Assert.That(p1.Alive, Is.False);
            Assert.That(p2.Alive, Is.False);
        }

        [Test]
        public void MeleeAdvancesAfterKillButArcherDoesNot()
        {
            var melee = new MatchGame(36u);
            var pawn = melee.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(3, 2), false, true);
            var pawnTarget = melee.AddUnit(Side.P2, UnitKind.Pawn, new BoardPosition(4, 2), false, true);
            melee.ResolveAttacks(new[] { new AttackOrder(pawn, pawnTarget) });

            var ranged = new MatchGame(36u);
            var archer = ranged.AddUnit(Side.P1, UnitKind.Archer, new BoardPosition(3, 2), false, true);
            var archerTarget = ranged.AddUnit(Side.P2, UnitKind.Pawn, new BoardPosition(5, 2), false, true);
            ranged.ResolveAttacks(new[] { new AttackOrder(archer, archerTarget) });

            Assert.That(pawn.Position, Is.EqualTo(new BoardPosition(4, 2)));
            Assert.That(archer.Position, Is.EqualTo(new BoardPosition(3, 2)));
            Assert.That(archer.NextAttack, Is.EqualTo(6));
        }

        [Test]
        public void KnightFirstStrikeLandsOnlyAfterKill()
        {
            var game = new MatchGame(36u);
            var knight = game.AddUnit(Side.P1, UnitKind.Knight, new BoardPosition(2, 1), false, true);
            var target = game.AddUnit(Side.P2, UnitKind.Pawn, new BoardPosition(4, 1), false, true);

            game.KnightJump(knight);

            Assert.That(target.Alive, Is.False);
            Assert.That(knight.Position, Is.EqualTo(new BoardPosition(4, 1)));
        }

        [Test]
        public void EveryCpuPairingCompletesWithinMatchLimit()
        {
            foreach (AiStyle p1 in System.Enum.GetValues(typeof(AiStyle)))
            foreach (AiStyle p2 in System.Enum.GetValues(typeof(AiStyle)))
            {
                var runner = new MatchRunner((uint)(1000 + (int)p1 * 10 + (int)p2), p1, p2);
                runner.RunToCompletion();

                Assert.That(runner.Finished, Is.True, $"{p1} vs {p2}");
                Assert.That(runner.Game.Now, Is.LessThanOrEqualTo(MatchGame.MatchLimitSeconds));
                Assert.That(runner.Game.Units, Is.Not.Empty);
            }
        }

        [Test]
        public void SameSeedAndCpuStylesProduceSameResult()
        {
            var first = new MatchRunner(424242u, AiStyle.Raid, AiStyle.Defense);
            var second = new MatchRunner(424242u, AiStyle.Raid, AiStyle.Defense);
            first.RunToCompletion();
            second.RunToCompletion();

            Assert.That(second.Game.Now, Is.EqualTo(first.Game.Now));
            Assert.That(second.Game.Winner, Is.EqualTo(first.Game.Winner));
            Assert.That(second.Game.DrawType, Is.EqualTo(first.Game.DrawType));
            Assert.That(second.Game.ObjectiveScore(Side.P1), Is.EqualTo(first.Game.ObjectiveScore(Side.P1)));
            Assert.That(second.Game.ObjectiveScore(Side.P2), Is.EqualTo(first.Game.ObjectiveScore(Side.P2)));
        }

        [Test]
        public void HumanControllerDeploysAndMovesWithoutCpuTakingOver()
        {
            var runner = new MatchRunner(7u, AiStyle.Rush, AiStyle.Defense, Side.P1);

            Assert.That(runner.TryHumanDeploy(UnitKind.Pawn, new BoardPosition(7, 0)), Is.False);
            Assert.That(runner.TryHumanDeploy(UnitKind.Pawn, new BoardPosition(0, 0)), Is.True);
            var pawn = runner.Game.UnitAt(new BoardPosition(0, 0));
            runner.Step();

            Assert.That(pawn.Position, Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(runner.TryHumanAction(pawn, new BoardPosition(1, 0)), Is.True);
        }
    }
}
