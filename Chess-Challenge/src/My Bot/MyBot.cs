﻿using ChessChallenge.API;
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
    const int CHECK_BONUS = 1000; //-piecevalue.
    const int RANK_FILE_MULTIPLIER = 1;
    const double DISREGARD_MOVES_STD_DEV_MOD = 3;

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 500 };
    int[] pieceBonusMoveValues = { 0, 10, 20, 15, 10, 25, -5 };

public Move Think(Board _b, Timer timer)
    {
        Console.WriteLine("Turn: " + turnCount);

        //Beginning of the game we prefer to move pawns.
        if (turnCount < 3)
            pieceBonusMoveValues = new int[] { 0, 16, 15, 10, 10, 30, -5 };

        board = _b;

        amIWhite = board.IsWhiteToMove;
        //Fuck off logic to get the bot to know what side of the board it is playing on.
        forwardIsUp = (board.GetPieceBitboard(PieceType.Rook, amIWhite) & 0x0000000000000001) > 0 ? 1 : -1;

        //Should do an actual opener here...

        //Then try and wing it.
        Move bestMove = new Move();
        int bestMoveValue = DepthSearch(1, true, out bestMove, true);

        Console.WriteLine("Trying move \"" + bestMove.MovePieceType + " to: " + bestMove.TargetSquare.Name + "\" with score: " + bestMoveValue );
        EvaluateMove(board, bestMove, true);
        ++turnCount;

        //System.Threading.Thread.Sleep(100);
        return bestMove;
    }

    int DepthSearch( int _depth, bool isMyMove, out Move _bestMove, bool _levelOne = false )
    {
        Move[] moves = board.GetLegalMoves();
        int[] moveScores = new int[moves.Length];
        int myMoveMultiplier = (isMyMove ? 1 : -1);

        for (int i = 0; i < moves.Count(); ++i)
        {
            moveScores[i] += EvaluateMove(board, moves[i]) * myMoveMultiplier;
        }

        //Null move check.
        if (moves.Count() == 0)
        {
            _bestMove = new Move();
            return 0;
        }

        //Standard deviation squared.
        //double average = moveScores.Average();
        //double stdDeviation = Math.Sqrt(moveScores.Average(v => Math.Pow(v - average, 2)));



        //Sort array so good moves are at the front
        //Array.Sort(moveScores, moves);
        //if (isMyMove)
        //{
        //    Array.Reverse(moves);
        //    Array.Reverse(moveScores);
        //}

        if ( _depth > 0 )
        {
            for (int i = 0; i < moves.Count(); ++i)
            {
                //Try to consider moves that are better than X stdDeviations from average
                //if (moveScores[i] < (average - DISREGARD_MOVES_STD_DEV_MOD * stdDeviation))
                //{
                //    moveScores[i] += -9999;
                //    continue;
                //}
                //string moveString = moves[i].ToString();
                board.MakeMove(moves[i]);
                //Add the best / worst move from the rest of our depth search. Opponent's score should be inverted.
                int difference = DepthSearch(_depth - 1, !isMyMove, out _bestMove);
                moveScores[i] += difference;
                board.UndoMove(moves[i]);
            }
        }

        //Sort array so good moves are at the front
        Array.Sort(moveScores, moves);

        //If it's out turn, put the best move at the front of the array (highest value).
        //If it's not our turn, put the MOST DAMAGING move at the front of the array (lowest value).
        if( isMyMove )
        {
            Array.Reverse(moves);
            Array.Reverse(moveScores);
        }

        //TODO: Delete this logging.
        if (_levelOne)
        {
            for (int i = 0; i < moves.Count(); ++i)
                Console.WriteLine("Move: " + moves[i] + "\t" + moveScores[i]);
        }

        _bestMove = moves[0];
        return moveScores[0];
    }

    int EvaluateMove(Board _b, Move _move, bool _log = false )
    {
        int moveScore = pieceBonusMoveValues[(int)_move.MovePieceType];

        if (_log) Console.WriteLine("Move score piece bonus: " + moveScore);

        moveScore += CheckOrCheckmate(_move);

        if (_log) Console.WriteLine("With check bonus: " + moveScore);

        Piece capturedPiece = _b.GetPiece(_move.TargetSquare);
        if (capturedPiece.IsNull == false && pieceValues[(int)capturedPiece.PieceType] > moveScore)
        {
            moveScore += pieceValues[(int)capturedPiece.PieceType] - (pieceValues[(int)_move.MovePieceType]/10);
        }

        if (_log) Console.WriteLine("With capture bonus: " + moveScore);

        //File bonus
        moveScore += (3 - Math.Abs(_move.TargetSquare.File - 3 - (_move.TargetSquare.File % 2)))* RANK_FILE_MULTIPLIER;
        //Rank bonus
        moveScore += _move.TargetSquare.Rank * forwardIsUp * RANK_FILE_MULTIPLIER; //Multiplying this by two makes the bot very aggressive.

        if (_log) Console.WriteLine("With rank/file bonus: " + moveScore);

        return moveScore;
    }

    int IsPieceProtected( Board _b, Move _move, bool _whiteIsAllied )
    {
        if (turnCount == 0)
            return 0;

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
    int CheckOrCheckmate(Move move)
    {
        int moveScore = 0;
        board.MakeMove(move);
            moveScore += Convert.ToInt32(board.IsInCheckmate())*999999;
            moveScore += Convert.ToInt32(board.IsInCheck()) * ((CHECK_BONUS - pieceValues[(int)move.MovePieceType])/10);
        board.UndoMove(move);
        return moveScore;
    }
}