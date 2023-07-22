using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    // PieceType (enum) None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6
    double[] pieceValues = { 0, 1, 3, 3.3, 5, 9, 1000 };

    // Transposition table
    private Dictionary<ulong, TranspositionTableEntry> transpositionTable;

    public MyBot()
    {
        // Initialize the transposition table
        transpositionTable = new Dictionary<ulong, TranspositionTableEntry>();
    }

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = default;
        double bestValue = double.MinValue;
        transpositionTable.Clear();

        int maxDepth = 3;  // Initial depth.
                           // plyCount is the number of single moves played by either white or black so far.
                           // So this would be move 26 where we increase the depth
        if (board.PlyCount >= 52) maxDepth = 4;
        // move 34
        else if (board.PlyCount >= 70) maxDepth = 5;
        // move 50
        else if (board.PlyCount >= 100) maxDepth = 6;
        // move 70
        else if (board.PlyCount >= 140) maxDepth = 7;

        int startTime = timer.MillisecondsElapsedThisTurn;
        int timeLimit = 3000;  // Set a time limit of 3 seconds.

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            foreach (var move in board.GetLegalMoves())
            {
                // Check time limit inside the loop
                if (timer.MillisecondsElapsedThisTurn - startTime >= timeLimit)
                {
                    return bestMove;
                }

                board.MakeMove(move);
                double moveValue = -Negamax(board, -10000, 10000, depth);
                board.UndoMove(move);
                if (moveValue > bestValue)
                {
                    bestValue = moveValue;
                    bestMove = move;
                }
            }
        }
        return bestMove;
    }


    double Negamax(Board board, double alpha, double beta, int depth)
    {
        ulong zobristKey = board.ZobristKey;

        // Check if this position is in the transposition table
        if (transpositionTable.TryGetValue(zobristKey, out var entry) && entry.Depth >= depth)
        {
            if (entry.Flag == TranspositionTableEntry.FlagType.Exact)
            {
                return entry.Value;
            }
            else if (entry.Flag == TranspositionTableEntry.FlagType.Lowerbound)
            {
                alpha = Math.Max(alpha, entry.Value);
            }
            else if (entry.Flag == TranspositionTableEntry.FlagType.Upperbound)
            {
                beta = Math.Min(beta, entry.Value);
            }
            if (alpha >= beta)
            {
                return entry.Value;
            }
        }

        if (depth == 0 || board.IsInCheckmate() || board.IsDraw())
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

        // Store the position and its evaluation in the transposition table
        var newEntry = new TranspositionTableEntry
        {
            Value = max,
            Depth = depth,
            Flag = alpha >= beta
                ? TranspositionTableEntry.FlagType.Lowerbound
                : TranspositionTableEntry.FlagType.Upperbound
        };
        transpositionTable[zobristKey] = newEntry;

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

            // Define the endgame squares
            var endgameSquares = new List<Square> {
        new Square("d4"),
        new Square("e4"),
        new Square("d5"),
        new Square("e5"),
        };

            // Define the 'castling' squares
            var castlingSquares = new List<Square> {
        new Square("g1"),
        new Square("h1"),
        new Square("c1"),
        new Square("b1"),
        new Square("g8"),
        new Square("h8"),
        new Square("c8"),
        new Square("b8")
        };

        // Gets an array of all the piece lists
        var pieceLists = board.GetAllPieceLists();
        double score = 0;
        int totalPieces = 0;

        ulong whiteControl = 0;
        ulong blackControl = 0;

        for (int i = 0; i < pieceLists.Length; i++)
        {
            var pieceList = pieceLists[i];
            double pieceValue = pieceValues[(int)pieceList.TypeOfPieceInList] * pieceList.Count;
            totalPieces += pieceList.Count;

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

                // Calculate control for each piece
                ulong attacks;
                switch (piece.PieceType)
                {
                    case PieceType.Bishop:
                    case PieceType.Rook:
                    case PieceType.Queen:
                        attacks = BitboardHelper.GetSliderAttacks(piece.PieceType, piece.Square, board);
                        break;
                    case PieceType.Knight:
                        attacks = BitboardHelper.GetKnightAttacks(piece.Square);
                        break;
                    case PieceType.King:
                        attacks = BitboardHelper.GetKingAttacks(piece.Square);
                        break;
                    case PieceType.Pawn:
                        attacks = BitboardHelper.GetPawnAttacks(piece.Square, piece.IsWhite);
                        break;
                    default:
                        continue;
                }
                if (pieceList.IsWhitePieceList)
                    whiteControl |= attacks;  // Add the attacks to the white control bitboard
                else
                    blackControl |= attacks;  // Add the attacks to the black control bitboard
            }
        }

        // Calculate the score based on the difference in control
        double controlWeight = 0.1;  // Change this to adjust the impact of control
        score += controlWeight * (BitboardHelper.GetNumberOfSetBits(whiteControl) - BitboardHelper.GetNumberOfSetBits(blackControl));

        // If in endgame, add points for king being in the center.
        if (totalPieces <= 16)
        {
            if (endgameSquares.Contains(board.GetKingSquare(true))) score += 1;
            if (endgameSquares.Contains(board.GetKingSquare(false))) score -= 1;
        }
        else // Not in endgame, add points for king being on a castling square. Also points for having the right to castle.
        {
            if (castlingSquares.Contains(board.GetKingSquare(true))) score += 2;
            if (castlingSquares.Contains(board.GetKingSquare(false))) score -= 2;
            if (board.HasKingsideCastleRight(true)) score += 0.5;
            if (board.HasQueensideCastleRight(true)) score += 0.5;
            if (board.HasKingsideCastleRight(false)) score -= 0.5;
            if (board.HasQueensideCastleRight(false)) score -= 0.5;
        }
        return board.IsWhiteToMove ? score : -score;
    }
    public class TranspositionTableEntry
    {
        public enum FlagType
        {
            Exact,      // The value is exactly the evaluation of the position
            Lowerbound, // The value is a lower bound on the evaluation
            Upperbound  // The value is an upper bound on the evaluation
        }

        public double Value { get; set; } // The evaluation of the position
        public int Depth { get; set; } // The depth at which the position was evaluated
        public FlagType Flag { get; set; } // The type of value stored
    }

}