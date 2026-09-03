using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LunaGame.Core;

internal static class DestinyParityHarness
{
    private const string JavaScriptSeeds1To10000Sha256 =
        "04068240b8372667efd269528210d79bb5dfccb7a48fb64aa3d88e4a6a9f1e8d";

    private static int Main()
    {
        try
        {
            VerifyFixture(1u, 1, "4", "2");
            VerifyFixture(36u, 2, "3,4", "0,4");
            VerifyFixture(682u, 3, "1,2,4", "0,3,4");
            VerifyFixture(1327u, 4, "0,1,2,4", "0,1,2,3");
            VerifyFirstTenThousandSeeds();
            VerifyMatchRules();
            Console.WriteLine("PASS Unity C# Destiny parity: 10,000 Seeds match game_core.js");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error);
            return 1;
        }
    }

    private static void VerifyMatchRules()
    {
        var movement = new MatchGame(1u);
        var pawn = movement.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(3, 2), spend: false, ready: true);
        Equal("4,2", string.Join(";", movement.LegalMoves(pawn)), "Pawn movement");

        var capture = new MatchGame(1u);
        var target = capture.Objectives.First(o => o.Side == Side.P2 && o.Kind == ObjectiveKind.Tower);
        var unit = capture.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(6, target.Lane), spend: false, ready: true);
        capture.Move(unit, new BoardPosition(7, target.Lane));
        capture.AdvanceTick();
        Equal(true, target.Captured, "Tower captured after three seconds");
        Equal(Side.P1, capture.Winner.Value, "All Destiny towers win the match");

        var timeout = new MatchGame(1327u);
        var outpost = timeout.Objectives.First(o => o.Side == Side.P2 && o.Kind == ObjectiveKind.Outpost);
        var scout = timeout.AddUnit(Side.P1, UnitKind.Pawn, new BoardPosition(6, outpost.Lane), spend: false, ready: true);
        timeout.Move(scout, new BoardPosition(7, outpost.Lane));
        for (var i = 0; i < MatchGame.MatchLimitSeconds / MatchGame.ActionTick; i++) timeout.AdvanceTick();
        Equal(Side.P1, timeout.Winner.Value, "Timeout objective majority");
        Equal("TIMEOUT", timeout.WinType, "Timeout win type");
    }

    private static void VerifyFixture(
        uint seed,
        int towerCount,
        string p1TowerLanes,
        string p2TowerLanes)
    {
        var game = new DestinyGame(seed);
        Equal(towerCount, game.TowerCount, $"Seed {seed} TowerCount");
        Equal(p1TowerLanes, Join(game, Side.P1), $"Seed {seed} P1");
        Equal(p2TowerLanes, Join(game, Side.P2), $"Seed {seed} P2");
    }

    private static void VerifyFirstTenThousandSeeds()
    {
        var text = new StringBuilder();
        for (uint seed = 1; seed <= 10000; seed++)
        {
            var game = new DestinyGame(seed);
            text.Append(seed)
                .Append('|').Append(game.TowerCount)
                .Append('|').Append(Join(game, Side.P1))
                .Append('|').Append(Join(game, Side.P2))
                .Append('\n');
        }

        using (var sha = SHA256.Create())
        {
            var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            var actual = string.Concat(digest.Select(value => value.ToString("x2")));
            Equal(JavaScriptSeeds1To10000Sha256, actual, "10,000-Seed SHA-256");
        }
    }

    private static string Join(DestinyGame game, Side side)
    {
        return string.Join(",", game.TowerLanes(side));
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected}, actual {actual}");
        }
    }
}
