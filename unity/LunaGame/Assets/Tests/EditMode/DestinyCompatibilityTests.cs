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
    }
}
