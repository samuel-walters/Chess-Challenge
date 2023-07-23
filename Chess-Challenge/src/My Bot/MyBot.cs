using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    // PieceType (enum) None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6
    double[] pieceValues = { 0, 1, 3, 3.3, 5, 9, 1000 };

    // Transposition table
    private Dictionary<ulong, TranspositionTableEntry> transpositionTable;

    private readonly ulong centralSquaresBitboard;
    private readonly ulong castlingSquaresBitboard;

    public MyBot()
    {
        // Initialize the transposition table
        transpositionTable = new Dictionary<ulong, TranspositionTableEntry>();

        centralSquaresBitboard = GetBitboardForSquares(new List<string> { "d4", "e4", "d5", "e5" });
        castlingSquaresBitboard = GetBitboardForSquares(new List<string> { "g1", "h1", "c1", "b1", "g8", "h8", "c8", "b8" });
    }

    private ulong GetBitboardForSquares(List<string> squareNames)
    {
        ulong bitboard = 0UL;
        foreach (var squareName in squareNames)
        {
            BitboardHelper.SetSquare(ref bitboard, new Square(squareName));
        }
        return bitboard;
    }

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = default;
        double bestValue = double.MinValue;
        transpositionTable.Clear();

        int depth;
        int plyCount = board.PlyCount;
        if (plyCount < 6) // Opening stage
        {
            depth = 2;
        }
        else if (plyCount < 60) // Middlegame
        {
            depth = 3;
        }
        else if (plyCount < 90) // endgame?
        {
            depth = 4;
        }
        else
        {
            depth = 5;
        }

        // Get capture moves
        var captureMoves = new List<Move>(board.GetLegalMoves(capturesOnly: true));

        // Get all moves
        var allMoves = new List<Move>(board.GetLegalMoves());

        // Remove capture moves from all moves
        allMoves.RemoveAll(move => captureMoves.Contains(move));

        // Combine capture moves and the remaining moves
        captureMoves.AddRange(allMoves);

        // Now captureMoves contains all moves, with capture moves at the start

        foreach (var move in captureMoves)
        {
            board.MakeMove(move);
            double moveValue = -Negamax(board, -10000, 10000, depth);
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

        // Get capture moves
        var captureMoves = new List<Move>(board.GetLegalMoves(capturesOnly: true));

        // Get all moves
        var allMoves = new List<Move>(board.GetLegalMoves());

        // Remove capture moves from all moves
        allMoves.RemoveAll(move => captureMoves.Contains(move));

        // Combine capture moves and the remaining moves
        captureMoves.AddRange(allMoves);

        // Now captureMoves contains all moves, with capture moves at the start

        foreach (var move in captureMoves)
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
        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? -10000 : 10000;
        }

        if (board.IsDraw())
        {
            return 0;
        }

        var pieceLists = board.GetAllPieceLists();
        double score = 0;
        int totalPieces = 0;

        int whiteCentralControl = 0;
        int blackCentralControl = 0;

        for (int i = 0; i < pieceLists.Length; i++)
        {
            var pieceList = pieceLists[i];
            double pieceValue = pieceValues[(int)pieceList.TypeOfPieceInList] * pieceList.Count;
            totalPieces += pieceList.Count;
            score += pieceList.IsWhitePieceList ? pieceValue : -pieceValue;

            for (int j = 0; j < pieceList.Count; j++)
            {
                var piece = pieceList.GetPiece(j);
                if (board.PlyCount < 30 && piece.PieceType == PieceType.Queen)
                {
                    continue;
                }

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

                ulong centralAttacks = attacks & centralSquaresBitboard;
                if (pieceList.IsWhitePieceList)
                    whiteCentralControl += BitboardHelper.GetNumberOfSetBits(centralAttacks);
                else
                    blackCentralControl += BitboardHelper.GetNumberOfSetBits(centralAttacks);
            }
        }

        double controlWeight = 0.1;
        score += controlWeight * (whiteCentralControl - blackCentralControl);

        if (totalPieces <= 16)
        {
            if (BitboardHelper.SquareIsSet(centralSquaresBitboard, board.GetKingSquare(true))) score += 1;
            if (BitboardHelper.SquareIsSet(centralSquaresBitboard, board.GetKingSquare(false))) score -= 1;
        }
        else
        {
            if (BitboardHelper.SquareIsSet(castlingSquaresBitboard, board.GetKingSquare(true))) score += 2;
            if (BitboardHelper.SquareIsSet(castlingSquaresBitboard, board.GetKingSquare(false))) score -= 2;
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
