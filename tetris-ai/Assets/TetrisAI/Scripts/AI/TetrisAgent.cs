using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class TetrisAgent : Agent
{
    // Check if we are training or just testing
    public bool IsTraining { get { return behaviorParams.BehaviorType == BehaviorType.Default; } }
    public bool IsHeuristic { get { return behaviorParams.BehaviorType == BehaviorType.HeuristicOnly; } }

    public List<float> States { get; set; }
    public List<int> MaskedActions { get; set; }

    [SerializeField] private TetrisGame mainGame;
    private BehaviorParameters behaviorParams;

    public override void Initialize()
    {
        base.Initialize();
        mainGame.Init(this);
        behaviorParams = GetComponent<BehaviorParameters>();
    }

    // Runs every time a new game starts
    public override void OnEpisodeBegin()
    {
        mainGame.StartGame();
    }

    // This is where the AI actually decides what to do
    public override void OnActionReceived(float[] aiChoices)
    {
        int chosenMove = Mathf.RoundToInt(aiChoices[0]);

        // Make sure the move is actually legal before doing it
        if (!MaskedActions.Contains(chosenMove))
        {
            // Math to figure out the exact column and rotation angle from the AI's single number choice
            int targetRotation = Mathf.RoundToInt(chosenMove % TetrisSettings.NumRotations);
            int targetColumn = Mathf.FloorToInt(chosenMove / TetrisSettings.NumRotations);

            mainGame.CreateBlock(targetColumn, TetrisSettings.Rotations[targetRotation]);
        }
        else
        {
            // If it picked an illegal move (like placing outside the board), kill the game
            mainGame.GameOver();
        }
    }

    // Feed the grid data to the AI
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(States);
    }

    // Block the AI from picking impossible moves
    public override void CollectDiscreteActionMasks(DiscreteActionMasker actionMasker)
    {
        actionMasker.SetMask(0, MaskedActions);
    }

    // Give points for clearing lines
    public void AddLineReward(GridState currentBoard)
    {
        // Favour clearing lines closer to the bottom of the board so it doesn't build too high
        float bottomBonus = (TetrisSettings.GridHeight - currentBoard.LinesMinRow) / 5f;
        float pointsToGive = Mathf.Pow(currentBoard.NumLines, 2) * TetrisSettings.GridWidth * bottomBonus;

        AddReward(pointsToGive);
    }

    public void Log(string prefix = "")
    {
        string badMoves = "";
        foreach (float m in MaskedActions)
        {
            badMoves += m + ",";
        }
        Debug.Log(prefix + "  Masked actions: " + badMoves);
    }
}