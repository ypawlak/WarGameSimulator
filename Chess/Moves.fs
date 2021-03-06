﻿module Moves

open System.Collections.Generic

type MoveType = 
    | PawnInitial
    | PawnSimple
    | PawnCapture
    | PawnEnPassant
    | Knight
    | Bishop
    | Rook
    | Queen
    | KingSimple
    | KingCastle

type MoveEffect = 
    | Standard
    | Promotion
    | EnPassantCapture
    | Castle

type Move =
    {   
        Moved: Pieces.Piece;
        StartSquare: int * int;
        DestinationSquare: int * int;
        Type: MoveType;
    }
    override this.ToString() =
        sprintf "%O %s -> %s [%A]" this.Moved (Board.getPrettyCoordsPrint this.StartSquare) (Board.getPrettyCoordsPrint this.DestinationSquare) this.Type
  
let getMoveTypesFor (pieceRank: Pieces.PieceRank) =
    match pieceRank with
    | Pieces.Pawn -> [PawnInitial; PawnSimple; PawnCapture; PawnEnPassant]
    | Pieces.Knigth -> [Knight]
    | Pieces.Bishop -> [Bishop]
    | Pieces.Rook -> [Rook]
    | Pieces.Queen -> [Queen]
    | Pieces.King -> [KingSimple; KingCastle]

let getDiagonalMoveVectors delta = 
    [(-delta, delta); (delta, delta); (delta, -delta); (-delta, -delta)]

let getPerpendicularkMoveVectors delta =
    [(0, delta); (delta, 0); (0, -delta); (-delta, 0)]

let bishopMoveVectors =
    List.concat ([1..8] |> List.map getDiagonalMoveVectors)

let rookMoveVectors =
    List.concat ([1..8] |> List.map getPerpendicularkMoveVectors)

let kingSimpleMoveVectors =
    let diagonalByOne = getDiagonalMoveVectors 1 in
    let perpendicularByOne = getPerpendicularkMoveVectors 1 in
    diagonalByOne @ perpendicularByOne

let getWhitesMoveVectorsFor moveType =
    match moveType with
    | PawnInitial -> [(0, 2)]
    | PawnSimple -> [(0, 1)]
    | PawnCapture -> [(1, 1); (-1, 1)]
    | PawnEnPassant -> [(1, 1); (-1, 1)]
    | Knight -> [(1, 2); (2, 1); (-1, 2); (-2, 1); (-1, -2); (-2, -1); (1, -2); (2, -1)]
    | Bishop -> bishopMoveVectors
    | Rook -> rookMoveVectors
    | Queen -> bishopMoveVectors @ rookMoveVectors
    | KingSimple -> kingSimpleMoveVectors
    | KingCastle -> [(-3,0); (2, 0)]

let getBlacksMoveVectorsFor moveType =
    getWhitesMoveVectorsFor moveType |> List.map (fun coords -> Utils.mul coords (-1, -1))

let getMoveVectorsFor moveType pieceColor =
    match pieceColor with
    | Pieces.White -> getWhitesMoveVectorsFor moveType
    | Pieces.Black -> getBlacksMoveVectorsFor moveType

let isInsideBoard (move, startSquare, board: Dictionary<int*int, Pieces.Piece option>) =
    board.ContainsKey (Utils.add startSquare move)

let getPassedSquaresForFlatMove sourceSq destSq =
    let xSeq, ySeq = 
        match sourceSq, destSq with
        | (x1, y1), (x2, y2) when x1 = x2
            -> List.init (abs (y2 - y1) + 1) (fun y -> x1), Utils.absRange y1 y2
        | (x1, y1), (x2, y2) when y1 = y2
            -> Utils.absRange x1 x2, List.init (abs (x2 - x1) + 1) (fun x -> y1)
        | (sx, sy), (dx, dy) 
            -> Utils.absRange sx dx, Utils.absRange sy dy
    in
    let squaresSeq = List.zip xSeq ySeq
    squaresSeq.Tail

let getPassedSquares moveType sourceSq destSq =
    match moveType with
    | Knight -> [destSq]
    | _ -> getPassedSquaresForFlatMove sourceSq destSq

let rec hasCollidingPiecesOnTheWay (moveType, passedSquares, movedPiece: Pieces.Piece, board: Dictionary<int*int, Pieces.Piece option>) =
    match moveType, passedSquares with
    | _, [] -> false
    | PawnInitial, [destSquare] | PawnSimple, [destSquare] | PawnEnPassant, [destSquare] 
        -> Board.isOccupied(destSquare, board)
    | _, [destSquare]
        -> Board.isOccupiedByPieceOfGivenColor (destSquare, movedPiece.Color, board)
    | _, head::tail
        -> Board.isOccupied (head, board) || hasCollidingPiecesOnTheWay (moveType, tail, movedPiece, board)

let isNotCollidingWithOtherPieces (moveType, sourceSq, moveVector, board: Dictionary<int*int, Pieces.Piece option>) =
    let passedSquares = getPassedSquares moveType sourceSq (Utils.add sourceSq moveVector) in
    let movedPiece = board.[sourceSq].Value in
    not (hasCollidingPiecesOnTheWay (moveType, passedSquares, movedPiece, board))

let matchesCustomRules (moveType, startSquare, moveVec, board: Dictionary<int*int, Pieces.Piece option>) =
    let movedPiece = board.[startSquare].Value in
    let dest = Utils.add startSquare moveVec in
    let oppositeColor = Pieces.negate movedPiece.Color in
    match moveType with
    | PawnInitial
        ->
            let sx, sy = startSquare in
            movedPiece.Color = Pieces.Black && sy = Board.BlackPawnStartY ||
            movedPiece.Color = Pieces.White && sy = Board.WhitePawnStartY
    | PawnCapture
        -> Board.isOccupiedByPieceOfGivenColor(dest, oppositeColor, board)
    //TODO: chek if this is exactly subsequent move after opposite pawn initial move
    | PawnEnPassant
        ->
            let yDeltaToCaptured = if oppositeColor = Pieces.Black then -1 else 1
            let captX, captY = Utils.add dest (0, yDeltaToCaptured) in
            Board.isOccupiedByPieceOfGivenColor ((captX, captY), oppositeColor, board) &&
            (oppositeColor = Pieces.Black && captY = Board.BlackPawnStartY - 2 ||
             oppositeColor = Pieces.White && captY = Board.WhitePawnStartY + 2)
    | KingCastle -> false // TODO: conditions for castle
    | _ -> true

let filterMoveVectors (moveType, startSquare, moveVcs, board: Dictionary<int*int, Pieces.Piece option>) =
    let possibleMoves =
        moveVcs
        |> List.filter (fun mv ->
            isInsideBoard(mv, startSquare, board) &&
            isNotCollidingWithOtherPieces(moveType, startSquare, mv, board) &&
            matchesCustomRules(moveType, startSquare, mv, board)) in
    moveType, possibleMoves;


let getDestinations startSquare moveVecs = 
    moveVecs
    |> List.map (fun mv -> Utils.add startSquare mv)

let createMoves moved startSquare moveVecs mType =
    moveVecs 
    |> getDestinations startSquare
    |> List.map (fun dest ->
        {
            StartSquare = startSquare;
            DestinationSquare = dest;
            Type = mType;
            Moved = moved;
        })

let getMovesForPiece (startSquare, board: Dictionary<int*int, Pieces.Piece option>) =
    let movedPiece = board.[startSquare].Value in
    let availableMoveTypes = getMoveTypesFor movedPiece.Rank in
    let moves =
        availableMoveTypes
        |> List.map (fun mvType -> mvType, getMoveVectorsFor mvType movedPiece.Color)
        |> List.map (fun (mType, mVecs) -> filterMoveVectors (mType, startSquare, mVecs, board)) 
        |> List.map (fun (mType, mVecs) -> createMoves movedPiece startSquare mVecs mType) in
    List.concat moves

let getMovesForColor (color, board: Dictionary<int*int, Pieces.Piece option>) =
    let movesByPieces =
        Board.getPiecesOfGivenColor (color, board)
        |> List.map (fun coords -> getMovesForPiece (coords, board)) in
    List.concat movesByPieces;

let leadsToOpponentFiguresEnd move =
    match move.DestinationSquare, move.Moved.Color with
    | (_, dy), Pieces.White when dy = Board.BlackFiguresStartY -> true
    | (_, dy), Pieces.Black when dy = Board.WhiteFiguresStartY -> true
    | _ -> false

let getEffect move = 
    match move.Type with
    | PawnSimple | PawnCapture when leadsToOpponentFiguresEnd move -> Promotion
    | PawnEnPassant -> EnPassantCapture
    | KingCastle-> Castle
    | _ -> Standard

let performStandardMove (move, board: Dictionary<int*int, Pieces.Piece option>) =
    board.[move.StartSquare] <- None
    board.[move.DestinationSquare] <- Some move.Moved

let performMove (move, board: Dictionary<int*int, Pieces.Piece option>) =
    let moveEffect =  getEffect move in
    match moveEffect with
    | Standard -> performStandardMove (move, board)
    | _ -> failwith "NotImplementedException"; //TODO
     