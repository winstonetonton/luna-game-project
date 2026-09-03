using System;
using System.Collections.Generic;
using System.Linq;
using LunaGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LunaGame.Client
{
    public sealed class LunaGamePresenter : MonoBehaviour
    {
        private readonly Button[,] cells = new Button[MatchGame.Width, MatchGame.Height];
        private readonly Dictionary<UnitKind, Button> unitButtons = new Dictionary<UnitKind, Button>();
        private MatchRunner runner;
        private MatchUnit selectedUnit;
        private UnitKind? selectedKind;
        private Text hud;
        private Text status;
        private Button autoButton;
        private bool autoPlay;
        private float autoElapsed;

        private static readonly Color P1 = Hex("#34D399");
        private static readonly Color P2 = Hex("#FB7185");
        private static readonly Color Ink = Hex("#E5E7EB");
        private static readonly Color Panel = Hex("#172033");
        private static readonly Color Selected = Hex("#FBBF24");

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            BuildInterface();
            NewMatch();
        }

        private void Update()
        {
            if (!autoPlay || runner == null || runner.Finished) return;
            autoElapsed += Time.unscaledDeltaTime;
            if (autoElapsed < 2f) return;
            autoElapsed = 0f;
            AdvanceMatch();
        }

        private void NewMatch()
        {
            var seed = unchecked((uint)DateTime.UtcNow.Ticks);
            runner = new MatchRunner(seed, AiStyle.Raid, AiStyle.Defense, Side.P1);
            selectedUnit = null;
            selectedKind = UnitKind.Pawn;
            autoPlay = false;
            autoElapsed = 0f;
            status.text = "駒を選び、自陣の空きマスへ配置してください";
            Refresh();
        }

        private void AdvanceMatch()
        {
            if (runner.Finished) return;
            runner.Step();
            selectedUnit = null;
            status.text = runner.Finished ? ResultText() : "3秒進行しました。次の指示をどうぞ";
            Refresh();
        }

        private void TapCell(int x, int y)
        {
            if (runner.Finished) return;
            var position = new BoardPosition(x, y);
            var occupant = runner.Game.UnitAt(position);
            if (occupant != null && occupant.Side == Side.P1)
            {
                selectedUnit = occupant;
                selectedKind = null;
                status.text = occupant.Locked ? "この駒は拠点を確保中です" : $"{UnitName(occupant.Kind)}を選択。移動先か敵をタップ";
                Refresh();
                return;
            }
            if (selectedUnit != null)
            {
                if (runner.TryHumanAction(selectedUnit, position))
                {
                    status.text = "行動しました。続けるか、+3秒進行してください";
                    selectedUnit = null;
                }
                else status.text = "そこには行動できません。光っている候補を選んでください";
                Refresh();
                return;
            }
            if (selectedKind.HasValue)
            {
                if (runner.TryHumanDeploy(selectedKind.Value, position)) status.text = $"{UnitName(selectedKind.Value)}を配置しました";
                else status.text = "配置できません。ポイントと自陣の空きマスを確認してください";
                Refresh();
            }
        }

        private void SelectKind(UnitKind kind)
        {
            selectedKind = kind;
            selectedUnit = null;
            status.text = $"{UnitName(kind)}（{MatchGame.Specs[kind].Cost}pt）を配置するマスをタップ";
            Refresh();
        }

        private void ToggleAuto()
        {
            autoPlay = !autoPlay;
            autoElapsed = 0f;
            status.text = autoPlay ? "自動進行中。もう一度押すと停止します" : "自動進行を停止しました";
            Refresh();
        }

        private void Refresh()
        {
            var game = runner.Game;
            hud.text = $"SEED {game.Seed}    TIME {game.Now}/{MatchGame.MatchLimitSeconds}\n" +
                $"YOU  {game.Player(Side.P1).Points}pt  OBJ {game.ObjectiveScore(Side.P1)}    CPU  {game.Player(Side.P2).Points}pt  OBJ {game.ObjectiveScore(Side.P2)}";
            var actionCells = selectedUnit == null ? new List<BoardPosition>() : game.LegalMoves(selectedUnit).ToList();
            if (selectedUnit != null && selectedUnit.Kind == UnitKind.Knight && game.Now >= selectedUnit.NextAction)
            {
                var forward = selectedUnit.Side == Side.P1 ? 1 : -1;
                var landing = new BoardPosition(selectedUnit.Position.X + 2 * forward, selectedUnit.Position.Y);
                var occupant = game.UnitAt(landing);
                if (landing.X >= 0 && landing.X < MatchGame.Width && (occupant == null || occupant.Side != selectedUnit.Side))
                    actionCells.Add(landing);
            }
            var attackUnits = selectedUnit == null ? Array.Empty<MatchUnit>() : game.Attackables(selectedUnit).ToArray();
            for (var y = 0; y < MatchGame.Height; y++)
            for (var x = 0; x < MatchGame.Width; x++)
            {
                var position = new BoardPosition(x, y);
                var button = cells[x, y];
                var unit = game.UnitAt(position);
                var objective = game.Objectives.FirstOrDefault(item => item.X == x && item.Lane == y);
                button.image.color = (x + y) % 2 == 0 ? Hex("#26344D") : Hex("#202C42");
                if (actionCells.Contains(position) || attackUnits.Contains(unit)) button.image.color = Hex("#5B4A1B");
                if (selectedUnit == unit) button.image.color = Selected;
                var objectiveText = objective == null ? "" : ObjectiveText(objective) + "\n";
                button.GetComponentInChildren<Text>().text = objectiveText + (unit == null ? "" : $"{(unit.Side == Side.P1 ? "YOU" : "CPU")}\n{UnitMark(unit.Kind)} {unit.Hp}");
                button.GetComponentInChildren<Text>().color = unit == null ? Ink : unit.Side == Side.P1 ? P1 : P2;
            }
            foreach (var entry in unitButtons)
            {
                entry.Value.image.color = selectedKind == entry.Key ? Selected : Panel;
                entry.Value.interactable = !runner.Finished && game.Player(Side.P1).Points >= MatchGame.Specs[entry.Key].Cost;
            }
            autoButton.GetComponentInChildren<Text>().text = autoPlay ? "AUTO STOP" : "AUTO PLAY";
            if (runner.Finished) status.text = ResultText();
        }

        private string ResultText()
        {
            if (runner.Game.Winner == Side.P1) return "YOU WIN — NEW GAMEで再戦できます";
            if (runner.Game.Winner == Side.P2) return "CPU WIN — NEW GAMEで再戦できます";
            return $"DRAW ({runner.Game.DrawType})";
        }

        private void BuildInterface()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var background = canvasObject.AddComponent<Image>();
            background.color = Hex("#0B1020");

            CreateText(canvas.transform, "LUNA — DESTINY", 42, TextAnchor.MiddleCenter, new Vector2(0, -55), new Vector2(1000, 80));
            hud = CreateText(canvas.transform, "HUD", 28, TextAnchor.MiddleCenter, new Vector2(0, -150), new Vector2(1000, 110));

            var board = CreatePanel(canvas.transform, "Board", new Vector2(0, -280), new Vector2(1000, 620), Hex("#111827"));
            var grid = board.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = MatchGame.Width;
            grid.cellSize = new Vector2(118, 118);
            grid.spacing = new Vector2(4, 4);
            grid.padding = new RectOffset(14, 14, 8, 8);
            for (var y = 0; y < MatchGame.Height; y++)
            for (var x = 0; x < MatchGame.Width; x++)
            {
                var cellX = x; var cellY = y;
                cells[x, y] = CreateButton(board, $"Cell {x},{y}", "", 21, () => TapCell(cellX, cellY));
            }

            status = CreateText(canvas.transform, "", 27, TextAnchor.MiddleCenter, new Vector2(0, -940), new Vector2(1000, 120));
            var roster = CreatePanel(canvas.transform, "Roster", new Vector2(0, -1080), new Vector2(1000, 250), Hex("#111827"));
            var rosterGrid = roster.gameObject.AddComponent<GridLayoutGroup>();
            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = 4;
            rosterGrid.cellSize = new Vector2(235, 105);
            rosterGrid.spacing = new Vector2(8, 8);
            rosterGrid.padding = new RectOffset(14, 14, 14, 14);
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
            {
                var selected = kind;
                unitButtons[kind] = CreateButton(roster, kind.ToString(), $"{UnitName(kind)}  {MatchGame.Specs[kind].Cost}pt", 24, () => SelectKind(selected));
            }

            var controls = CreatePanel(canvas.transform, "Controls", new Vector2(0, -1370), new Vector2(1000, 120), Hex("#111827"));
            var controlsLayout = controls.gameObject.AddComponent<HorizontalLayoutGroup>();
            controlsLayout.spacing = 12;
            controlsLayout.padding = new RectOffset(14, 14, 12, 12);
            controlsLayout.childForceExpandWidth = true;
            CreateButton(controls, "Step", "+3 SEC", 28, AdvanceMatch);
            autoButton = CreateButton(controls, "Auto", "AUTO PLAY", 25, ToggleAuto);
            CreateButton(controls, "New", "NEW GAME", 25, NewMatch);
            CreateText(canvas.transform, "遊び方：駒ボタン → 自陣4列の空きマス。盤上の自軍駒 → 移動先または敵。\n敵側のT（Tower）をすべて3秒確保すると勝利。180秒時はT＋Oの確保数で判定。", 23, TextAnchor.MiddleCenter, new Vector2(0, -1540), new Vector2(1000, 190));
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            var item = new GameObject("Text", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = item.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, int fontSize, UnityEngine.Events.UnityAction action)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var button = item.GetComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            button.image.color = Panel;
            button.onClick.AddListener(action);
            var text = CreateText(item.transform, label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(-12, -8);
            return button;
        }

        private static string ObjectiveText(Objective objective)
        {
            var mark = objective.Kind == ObjectiveKind.Tower ? "T" : "O";
            if (objective.Captured) return mark + "✓";
            return objective.CaptureStart.HasValue ? mark + "…" : mark;
        }

        private static string UnitName(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Pawn: return "歩";
                case UnitKind.Gold: return "金";
                case UnitKind.Silver: return "銀";
                case UnitKind.Knight: return "桂馬";
                case UnitKind.Lance: return "香車";
                case UnitKind.Archer: return "弓";
                case UnitKind.Bishop: return "角";
                default: return "飛車";
            }
        }

        private static string UnitMark(UnitKind kind) => kind.ToString().Substring(0, kind == UnitKind.Knight ? 1 : Math.Min(2, kind.ToString().Length)).ToUpperInvariant();
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
    }
}
