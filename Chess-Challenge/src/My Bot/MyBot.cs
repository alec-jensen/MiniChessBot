/*
    I am NOT a C# developer. I have never written a line of C# code before this.
    I have tried my best :)
*/

using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    // Piece values: nothing, pawn, knight, bishop, castle, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

    // Evaluation weights for different factors
    double materialWeight = 1.0, 
    mobilityWeight = 0.5, 
    kingSafetyWeight = 0.3, 
    capturingWeight = 0.6, 
    distanceWeight = 0.4, 
    repetitionWeight = 0.5;

    Queue<Move> previousMoves = new Queue<Move>();

    public void AddMove(Move move)
    {
        previousMoves.Enqueue(move);
        if (previousMoves.Count > 2) previousMoves.Dequeue();
    }

    public Move Think(Board board, Timer timer)
    {
        int maxDepth = 3;
        
        Move bestMove = Move.NullMove;
        int bestScore = int.MinValue;

        List<Move> orderedMoves = OrderMoves(board.GetLegalMoves());

        // Iterative Deepening
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            foreach (Move move in orderedMoves)
            {
                // Play immediate checkmate if possible
                if (board.IsInCheckmate())
                {
                    AddMove(move);
                    return move;
                }

                // Update evaluation weights based on the game phase
                double weight = board.GetAllPieceLists().Sum(pl => pl.Count) <= 12 ? kingSafetyWeight : (board.GetAllPieceLists().Sum(pl => pl.Count) <= 24 ? mobilityWeight : materialWeight);

                // Evaluate the move using the advanced evaluation function and minimax
                board.MakeMove(move);
                int score = (int)(EvaluatePiece(board, board.GetPiece(move.TargetSquare)) * weight) + Minimax(board, depth - 1, false); // Use minimax for deeper evaluations
                board.UndoMove(move);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }
        }
        AddMove(bestMove);
        return bestMove;
    }

    private (Move, int) AlphaBeta(Board board, int depth, int alpha, int beta, bool isMaximizingPlayer)
    {
        if (depth == 0 || board.IsInCheckmate())
        {
            int score = 0;

            foreach (Piece piece in GetPieces(board))
            {
                score += EvaluatePiece(board, piece);
            }

            return (Move.NullMove, score);
        }

        Move bestMove = Move.NullMove;
        int bestScore = isMaximizingPlayer ? int.MinValue : int.MaxValue;
        List<Move> moves = OrderMoves(board.GetLegalMoves());

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int score = AlphaBeta(board, depth - 1, alpha, beta, !isMaximizingPlayer).Item2;
            board.UndoMove(move);

            if (isMaximizingPlayer)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, score);
            }
            else
            {
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                beta = Math.Min(beta, score);
            }

            if (beta <= alpha)
                break;
        }

        return (bestMove, bestScore);
    }


    private List<Move> OrderMoves(Move[] moves)
    {
        // Basic move ordering (captures first)
        List<Move> orderedMoves = new List<Move>();
        foreach (Move move in moves)
        {
            if (move.IsCapture)
            {
                orderedMoves.Insert(0, move); // Captures at the beginning
            }
            else
            {
                orderedMoves.Add(move); // Non-captures at the end
            }
        }

        return orderedMoves;
    }

    private int Minimax(Board board, int depth, bool isMaximizingPlayer)
    {
        if (depth == 0 || board.IsInCheckmate())
        {
            int score = 0;
            
            foreach (Piece piece in GetPieces(board))
            {
                score += EvaluatePiece(board, piece);
            }
            return score;
        }

        int bestScore = isMaximizingPlayer ? int.MinValue : int.MaxValue;

        foreach (Move move in OrderMoves(board.GetLegalMoves()))
        {
            board.MakeMove(move);
            int score = Minimax(board, depth - 1, !isMaximizingPlayer);
            board.UndoMove(move);

            if (isMaximizingPlayer)
            {
                bestScore = System.Math.Max(bestScore, score);
            }
            else
            {
                bestScore = System.Math.Min(bestScore, score);
            }
        }

        return bestScore;
    }

    private int EvaluatePiece(Board board, Piece piece)
    {
        int materialScore = 0;
        int mobilityScore = 0;
        int kingSafetyScore = 0;
        int capturingScore = 0;
        int distanceScore = 0;
        int repetitionScore = 0;

        // Evaluate material count
        int pieceValue = pieceValues[(int)piece.PieceType];
        materialScore += GetPieceCount(board, piece.PieceType, true) * pieceValue;
        materialScore -= GetPieceCount(board, piece.PieceType, false) * pieceValue;

        // For each piece, calculate the number of legal moves it can make
        // and add a mobility bonus based on the number of moves
        foreach (Move move in board.GetLegalMoves())
        {
            if (move.StartSquare == piece.Square)
            {
                mobilityScore += (piece.IsWhite ? 1 : -1) * pieceValue;
            }
        }

        // Evaluate king safety
        if (board.IsInCheck())
        {
            // Penalize the side whose king is in check
            kingSafetyScore += board.IsWhiteToMove ? -1 : 1;
        }

        // Evaluate capture score
        foreach (Move move in board.GetLegalMoves())
        {
            if (move.IsCapture && move.StartSquare == piece.Square)
            {
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];
                capturingScore += capturedPieceValue;
            }
        }

        // Evaluate distance score
        foreach (Move move in board.GetLegalMoves())
        {
            if (move.StartSquare == piece.Square)
            {
                int distance = Math.Abs(move.StartSquare.File - move.TargetSquare.File) + Math.Abs(move.StartSquare.Rank - move.TargetSquare.Rank);
                distanceScore += distance;
            }
        }

        // Evaluate repetition score
        foreach (Move previousMove in previousMoves)
        {
            if (previousMove.StartSquare == piece.Square)
            {
                repetitionScore++;
            }
        }

        // Combine all the scores with the corresponding weights
        int finalScore = (int)(materialWeight * materialScore +
                              mobilityWeight * mobilityScore +
                              kingSafetyWeight * kingSafetyScore +
                              capturingWeight * capturingScore +
                              distanceWeight * distanceScore +
                              repetitionWeight * repetitionScore);

        return finalScore;
    }

    // Helper method to get the count of pieces of a given type and color
    private int GetPieceCount(Board board, PieceType pieceType, bool isWhite)
    {
        return GetPieces(board).Count(piece => piece.IsWhite == isWhite && piece.PieceType == pieceType);
    }

    private IEnumerable<Piece> GetPieces(Board board)
    {
        return board.GetAllPieceLists()
                    .SelectMany(pieceList => pieceList)
                    .Where(piece => piece != null);
    }
}