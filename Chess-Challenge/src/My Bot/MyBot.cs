using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    double[] pieceValues = { 0, 1, 3, 3.5, 5, 9, 1000 };

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = default;
        double bestValue = double.MinValue;
        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double moveValue = -Negamax(board, -10000, 10000, 3);
            board.UndoMove(move);
            if (moveValue > bestValue)
            {
                bestValue = moveValue;
                bestMove = move;
            }
        }
        return bestMove;
    }

    double Negamax(Board board, double alpha, double beta, int depth)
    {
        if (depth == 0)
        {
            return Evaluate(board);
        }

        double max = double.MinValue;
        foreach (var move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            double val = -Negamax(board, -beta, -alpha, depth - 1);
            board.UndoMove(move);
            max = Math.Max(max, val);
            alpha = Math.Max(alpha, val);
            if (alpha >= beta)
                break;
        }
        return max;
    }

    double Evaluate(Board board)
    {
        // Define the central squares
        var centerSquares = new List<Square> {
        new Square("d4"),
        new Square("e4"),
        new Square("d5"),
        new Square("e5"),
        new Square("c3"),
        new Square("d3"),
        new Square("e3"),
        new Square("f3"),
        new Square("c4"),
        new Square("f4"),
        new Square("c5"),
        new Square("f5"),
        new Square("c6"),
        new Square("d6"),
        new Square("e6"),
        new Square("f6"),
        };

        var pieceLists = board.GetAllPieceLists();
        double score = 0;

        for (int i = 0; i < pieceLists.Length; i++)
        {
            var pieceList = pieceLists[i];
            double pieceValue = pieceValues[(int)pieceList.TypeOfPieceInList] * pieceList.Count;

            // If the piece list contains white pieces, add the piece value to the score
            // Otherwise, subtract it
            score += pieceList.IsWhitePieceList ? pieceValue : -pieceValue;

            // Check for each piece if it is in the center
            for (int j = 0; j < pieceList.Count; j++)
            {
                var piece = pieceList.GetPiece(j);
                if (centerSquares.Contains(piece.Square))
                {
                    // Add or subtract a bonus for the central control
                    score += pieceList.IsWhitePieceList ? 0.5 : -0.5;
                }
            }
        }

        return board.IsWhiteToMove ? score : -score;
    }

}