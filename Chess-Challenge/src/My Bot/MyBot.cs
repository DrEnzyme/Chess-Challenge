using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class MyBot : IChessBot
{
    static Board board;
    static bool amIWhite;
    static int forwardIsUp = 1;
    static int turnCount = 0;

    public Move Think(Board _b, Timer timer)
    {
        board = _b;

        amIWhite = board.IsWhiteToMove;
        //Fuck off logic to get the bot to know what side of the board it is playing on.
        forwardIsUp = (board.GetPieceBitboard(PieceType.Rook, amIWhite) & 0x0000000000000001) > 0 ? 1 : -1;

        //Should do an actual opener here...

        //Then try and wing it.
        Move bestMove = new Move();
        int bestMoveValue = DepthSearch(3, true, out bestMove, true);

        Console.WriteLine("Trying move \"" + bestMove.MovePieceType + " to: " + bestMove.TargetSquare.Name + " with score: " + bestMoveValue );
        EvaluateMove(board, bestMove, true, true);
        ++turnCount;

        //System.Threading.Thread.Sleep(100);
        return bestMove;
    }

    int DepthSearch( int _depth, bool isMyMove, out Move _bestMove, bool _levelOne = false )
    {
        Move[] moves = board.GetLegalMoves();
        Dictionary<Move, int> movesAndScores = moves.ToDictionary(item => item, value => 0);
        //List<int> moveScores = new List<int>(new int[moves.Length]);
        
        foreach( var keyValuePair in movesAndScores)
        {
            movesAndScores[keyValuePair.Key] = EvaluateMove(board, keyValuePair.Key, isMyMove);
            if (_depth > 0)
            {
                board.MakeMove(keyValuePair.Key);
                    //Add the best/worst move from the rest of our depth search.
                    movesAndScores[keyValuePair.Key] += DepthSearch(_depth - 1, !isMyMove, out _bestMove);
                board.UndoMove(keyValuePair.Key);
            }
        }

        movesAndScores.OrderBy(x => -x.Value);

        //for(int i = 0; i < movesAndScores.Count; ++i )
        //{
        //    //movesAndScores.Set = EvaluateMove(board, moves[i], isMyMove);
        //    if (_depth > 0)
        //    {
        //        board.MakeMove(moves[i]);
        //        //Add the best/worst move from the rest of our depth search.
        //        moveScores[i] += DepthSearch(_depth - 1, !isMyMove, out _bestMove);
        //        board.UndoMove(moves[i]);
        //    }
        //}

        //New approach...
        //Evaluate all the moves.
        //Trim out some of the bad moves.
        //Depth search on the remainders.

        if (movesAndScores.Count == 0)
        {
            _bestMove = new Move();
            return 0;
        }

        KeyValuePair<Move, int> bmkvp = (isMyMove ? movesAndScores.First() : movesAndScores.Last());
        _bestMove = bmkvp.Key;

        if (_levelOne)
        {
            foreach (var kvp in movesAndScores)
                Console.WriteLine("Move: " + kvp.Key.ToString() + "\t" + kvp.Value);
        }
            

        return bmkvp.Value;
    }

    int EvaluateMove(Board _b, Move _move, bool isMyMove, bool _log = false )
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 10, 30, 30, 50, 90, 100 };
        int[] pieceBonusMoveValues = { 0, 5, 12, 11, 10, 13, -5 };

        //Beginning of the game we prefer to move pawns.
        if( turnCount < 6 )
            pieceBonusMoveValues = new int[] { 0, 12, 11, 11, 0, 8, -5 };

        int moveScore = 0;
        
        if (MoveIsCheckmate(_move))
        {
            return 999999;
        }

        Piece capturedPiece = _b.GetPiece(_move.TargetSquare);
        if (pieceValues[(int)capturedPiece.PieceType] > moveScore)
        {
            moveScore += pieceValues[(int)capturedPiece.PieceType];
            if (_log)
                Console.WriteLine("There's a piece I can capture. Move Score is: " + moveScore);
        }

        //File bonus
        moveScore += (3 - Math.Abs(_move.TargetSquare.File - 3 - (_move.TargetSquare.File % 2))) * 1;
        if (_log)
            Console.WriteLine("My file bonus brings us to: " + moveScore);
        //Rank bonus
        moveScore += _move.TargetSquare.Rank * forwardIsUp;
        if (_log)
            Console.WriteLine("My rank bonus brings us to: " + moveScore);
        moveScore += pieceBonusMoveValues[(int)(_move.MovePieceType)];
        if (_log)
            Console.WriteLine("My piece bonus brings us to: " + moveScore);
        //moveScore += IsPieceProtected(_b, _move, isMyMove ? amIWhite : !amIWhite);
        //if (_log)
        //    Console.WriteLine("My protect bonus brings us to: " + moveScore);
        moveScore -= Convert.ToInt32(WillThisCauseRepeated(_move)) * 10;
        if (_log)
            Console.WriteLine("My repeated board penalty brings us to: " + moveScore);

        return moveScore * (isMyMove ? 1 : -1);
    }

    int IsPieceProtected( Board _b, Move _move, bool _whiteIsAllied )
    {
        int bonus = 0;
        int protectBonus = 8;
        Piece p;

        p = _b.GetPiece(new Square(Bound(_move.TargetSquare.File - 1), Bound(_move.TargetSquare.Rank -forwardIsUp)));
        if( (p.IsPawn || p.IsBishop || p.IsQueen) )
        {
            bonus += protectBonus * (p.IsWhite == _whiteIsAllied ? 1 : -2);
        }

        p = _b.GetPiece(new Square( Bound(_move.TargetSquare.File + 1), Bound(_move.TargetSquare.Rank -forwardIsUp)));
        if ((p.IsPawn || p.IsBishop || p.IsQueen) && p.IsWhite == _whiteIsAllied)
        {
            bonus += protectBonus * (p.IsWhite == _whiteIsAllied ? 1 : -2);
        }

        return bonus;
    }

    int Bound( int _i )
    {
        return Math.Clamp(_i, 0, 7);
    }

    bool WillThisCauseRepeated(Move _move)
    {
        board.MakeMove(_move);
        bool isRepeated = board.IsRepeatedPosition();
        board.UndoMove(_move);
        return isRepeated;
    }

    // Test if this move gives checkmate
    bool MoveIsCheckmate(Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
}