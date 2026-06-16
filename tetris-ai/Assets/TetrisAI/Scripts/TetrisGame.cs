using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.MLAgents;
using UnityEngine;

public class TetrisGame : MonoBehaviour
{
    [SerializeField] private UIController ui;
    [SerializeField] private TetrisPiece[] tetrominoes;

    private TetrisGrid gridCtrl;
    private TetrisAgent aiPlayer;
    private TetrisBag pieceBag;

    private int scoreTrack = 0;
    private int linesTrack = 0;
    private int activePieceId;
    private int[,] testGrid;
    private int[] clearTypes = new int[4]; // Tracks singles, doubles, triples, tetrises

    public void Init(TetrisAgent agentScript)
    {
        this.aiPlayer = agentScript;
        gridCtrl = GetComponent<TetrisGrid>();
        testGrid = new int[TetrisSettings.GridWidth, TetrisSettings.GridHeight];
        ui.SetHighScore(0, 0);

        // Spawn all the pieces hidden at the start to save performance later
        for (int i = 0; i < tetrominoes.Length; i++)
        {
            tetrominoes[i] = Instantiate(tetrominoes[i], Vector3.zero, Quaternion.identity).GetComponent<TetrisPiece>();
            tetrominoes[i].transform.SetParent(transform);
            tetrominoes[i].Init(this, gridCtrl, true);
            tetrominoes[i].gameObject.SetActive(false);
        }
    }

    public void StartGame()
    {
        ResetGame();
        GetNextStates();
    }

    private void ResetGame()
    {
        pieceBag = new TetrisBag();
        gridCtrl.Reset();
        ResetScore();
    }

    public void BlockPlaced(GridState boardState)
    {
        if (boardState.NumLines > 0)
        {
            clearTypes[boardState.NumLines - 1]++;
            AddToScore(boardState.NumLines);
            aiPlayer.AddLineReward(boardState); // Give AI big points
        }
        else
        {
            aiPlayer.AddReward(TetrisSettings.Reward.BlockPlaced); // Give AI small points for surviving
        }

        if (aiPlayer.IsHeuristic)
        {
            gridCtrl.LogState();
        }

        GetNextStates();
    }

    private void AddToScore(int linesCleared)
    {
        scoreTrack += TetrisSettings.Points[linesCleared - 1];
        linesTrack += linesCleared;
        ui.SetScore(scoreTrack, linesTrack);
    }

    private void ResetScore()
    {
        scoreTrack = 0;
        linesTrack = 0;
        clearTypes = new int[4];
        ui.SetScore(0, 0);
    }

    public void GameOver()
    {
        UpdateStats();
        aiPlayer.AddReward(TetrisSettings.Reward.GameOver); // Huge penalty
        aiPlayer.EndEpisode();
    }

    public void CreateBlock(int targetX, float rotAngle)
    {
        V2Int[] startPos = tetrominoes[activePieceId].GetBlockPositions(targetX, TetrisSettings.SpawnY, rotAngle);
        TetrisPiece newBlock = Instantiate(tetrominoes[activePieceId]).GetComponent<TetrisPiece>();

        newBlock.transform.SetParent(transform);
        newBlock.gameObject.SetActive(true);
        newBlock.Init(this, gridCtrl, aiPlayer);
        newBlock.SetBlockPositions(startPos);
    }

    private async void GetNextStates()
    {
        aiPlayer.States = new List<float>();
        aiPlayer.MaskedActions = new List<int>();

        activePieceId = pieceBag.GetPiece();

        // Let this run in the background so the game doesn't freeze
        await Task.Run(() => PopulateStates());

        // If every possible move is masked (illegal), we lost
        if (aiPlayer.MaskedActions.Count >= TetrisSettings.PossibleStates)
        {
            GameOver();
        }
        else if (!aiPlayer.IsHeuristic)
        {
            aiPlayer.RequestDecision();
        }
    }

    private void PopulateStates()
    {
        int actionIndex = 0;

        for (int x = 0; x < TetrisSettings.GridWidth; x++)
        {
            for (int r = 0; r < TetrisSettings.Rotations.Length; r++)
            {
                float[] simState = GetState(activePieceId, x, r);

                aiPlayer.States.AddRange(simState);

                // If the state returned -1, it's an illegal move
                if (simState[0] == -1)
                {
                    aiPlayer.MaskedActions.Add(actionIndex);
                }

                actionIndex++;
            }
        }
    }

    public float[] GetState(int piece, int x, int rot)
    {
        float[] metrics = new float[4];

        // simulate dropping the piece at this x and rotation
        V2Int[] simPositions = tetrominoes[piece].GetBlockPositions(x, TetrisSettings.SpawnY, TetrisSettings.Rotations[rot]);

        testGrid = gridCtrl.GetTempGrid();

        if (gridCtrl.CheckPositionsAreValid(simPositions, testGrid))
        {
            gridCtrl.MoveBlockDownToPlace(simPositions, ref testGrid);

            gridCtrl.GetLines(ref testGrid);
            gridCtrl.GetGridProperties(testGrid, ref metrics[0], ref metrics[1], ref metrics[2], ref metrics[3]);

            // Keep everything between 0 and 1 so the neural network doesn't freak out
            metrics[0] = metrics[0] / 4f;
            metrics[1] = metrics[1] / TetrisSettings.GridSize;
            metrics[2] = metrics[2] / TetrisSettings.GridSize;
            metrics[3] = metrics[3] / TetrisSettings.GridSize;
        }
        else
        {
            // Flag as a bad move
            metrics[0] = -1;
            metrics[1] = -1;
            metrics[2] = -1;
            metrics[3] = -1;
        }

        return metrics;
    }

    private void UpdateStats()
    {
        if (aiPlayer.IsTraining)
        {
            Academy.Instance.StatsRecorder.Add("Score", scoreTrack);
            Academy.Instance.StatsRecorder.Add("Lines", linesTrack);
            Academy.Instance.StatsRecorder.Add("Line x1", clearTypes[0]);
            Academy.Instance.StatsRecorder.Add("Line x2", clearTypes[1]);
            Academy.Instance.StatsRecorder.Add("Line x3", clearTypes[2]);
            Academy.Instance.StatsRecorder.Add("Line x4", clearTypes[3]);
        }
    }
}