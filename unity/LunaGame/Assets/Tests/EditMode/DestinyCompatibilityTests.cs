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
    }
}

