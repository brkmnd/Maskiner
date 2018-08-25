namespace Maskiner
module GrammarParserExt =
    open System.Collections.Generic
    open System.Text.RegularExpressions
    type Tokens =
        | Token_as
        | Token_assoc
        | Token_bang
        | Token_comma
        | Token_direction of string
        | Token_group
        | Token_identifier of string
        | Token_lbrace
        | Token_mid
        | Token_num of string
        | Token_prec
        | Token_prod
        | Token_rarrow
        | Token_rbrace
        | Token_string of string
        | Token_token
        | Token_dollar
    type Action =
        | Shift of int
        | Reduce of int
        | Accept
        | Error of string
    //type ProdExp =
        //| NonTerm of string
        //| Term of string
        //| Delim
        //| Dollar
    type Tree =
        | EmptyTree
        | LeafStr of string
        | LeafId of string
        | LeafNum of string
        | LeafDir of string
        | RSide of GrammarParser.ProdExp list
        | TokenAtt of Tree list
        | TupleArgs of Tree list
        | DataProds of Dictionary<string,(GrammarParser.ProdExp list) list>
        | DataTokens of Dictionary<string,string>
        | DataBTokens of List<string>
        | DataPrecs of Dictionary<string,int>
        | DataAssocs of Dictionary<string,string>
        | DataGroups of Dictionary<string,string list>
    (* Add leaf nodes/tokens here.
     * That is nodes of type token
     * that is to be leafs in the tree
     * *)
    let addLeaf2tree tree node =
        match node with
        | Token_string str -> (LeafStr str)::tree
        | Token_identifier id -> (LeafId id)::tree
        | Token_num n -> (LeafNum n)::tree
        | Token_direction dir -> (LeafDir dir)::tree
        |  -> tree
    let data_productions = new Dictionary<string,(GrammarParser.ProdExp list) list>()
    let data_tokens = new Dictionary<string,string>()
    let data_btokens = new List<string>()
    let data_assoc = new Dictionary<string,string>()
    let data_prec = new Dictionary<string,int>()
    let data_groups = new Dictionary<string,string list>()
    (* Initialize tree stack.
     * This is passed on to the production_funs as tree
     * *)
    let initTreeStack = [
        (DataProds data_productions)
        (DataTokens data_tokens)
        (DataBTokens data_btokens)
        (DataPrecs data_prec)
        (DataAssocs data_assoc)
        (DataGroups data_groups)
        ]
    let data_add2prods pName rSide =
        let dContains = data_productions.ContainsKey(pName)
        let newRSide =
            let d0 =
                if dContains then
                    []::data_productions.[pName]
                else [[]]
            List.fold
                (fun acc x ->
                    let head = List.head acc
                    let tail = List.tail acc
                    match x with
                    | GrammarParser.Delim -> []::head::tail
                    | t -> (head @ [t])::tail
                    )
                d0
                rSide
        if dContains then
            data_productions.[pName] <- newRSide
        else
            data_productions.Add(pName,newRSide)
    let data_add2prec tName lev =
        if data_prec.ContainsKey(tName) then ()
        else
            data_prec.Add(tName,(int) lev)
    let data_add2tokens tName tVal =
        if data_tokens.ContainsKey(tName) then ()
        else
            data_tokens.Add(tName,tVal)
    let data_add2assoc tName tVal =
        if data_assoc.ContainsKey(tName) then ()
        else
            data_assoc.Add(tName,tVal)
    let data_add2groups gName gElms =
        let createElms () =
            List.fold
                (fun acc x ->
                    match x with
                    | LeafStr gelm -> gelm::acc
                    | _ -> acc
                    )
                []
                gElms
        if data_groups.ContainsKey(gName) then ()
        else
            data_groups.Add(gName,createElms())
    let data_add2btokens args =
        List.fold
            (fun u x ->
                match x with
                | LeafStr btoken -> data_btokens.Add(btoken); u
                | _ -> u
                )
            ()
            args
    (* Add handling of productions here.
     * That is insert into tree or outside
     * data-structures.
     * *)
    let productions_fun = [|
        //[0] __ -> Exp 
        (fun tree pOrder -> (tree,pOrder))
        //[1] Exp -> prod Identifier rarrow ProdRight Exp 
        (fun tree pOrder ->
            match tree with
            | (RSide rSide)::(LeafId pName)::tree ->
                data_add2prods pName rSide
                let newProdOrder =
                    if List.exists (fun x -> x = pName) pOrder then pOrder
                    else pName::pOrder
                (tree,newProdOrder)
            | _ -> (tree,pOrder)
            )
        //[2] Exp -> prec String Num Exp 
        (fun tree pOrder ->
            match tree with
            | (LeafNum n)::(LeafStr token)::tree ->
                data_add2prec token n
                (tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[3] Exp -> assoc String direction Exp 
        (fun tree pOrder ->
            match tree with
            | (LeafDir dir)::(LeafStr tName)::tree ->
                data_add2assoc tName dir
                (tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[4] Exp -> token Token Exp 
        (fun tree pOrder ->
            match tree with
            | (TokenAtt [LeafStr tName;LeafStr tVal])::tree ->
                data_add2tokens tName tVal
                (tree,pOrder)
            | (TokenAtt [LeafStr tName])::tree ->
                data_add2tokens tName ""
                (tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[5] Exp -> bang token BangToken Exp 
        (fun tree pOrder -> (tree,pOrder))
        //[6] Exp -> group Group Exp 
        (fun tree pOrder -> (tree,pOrder))
        //[7] Exp -> 
        (fun tree pOrder ->
            match tree with
            | _ -> (tree,pOrder)
            )
        //[8] ProdRight -> String ProdRight 
        (fun tree pOrder ->
            match tree with
            | (RSide rSide)::(LeafStr term)::tree ->
                ((RSide ((GrammarParser.Term term)::rSide))::tree,pOrder)
            | (LeafStr term)::tree -> ((RSide [GrammarParser.Term term])::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[9] ProdRight -> Identifier ProdRight 
        (fun tree pOrder ->
            match tree with
            | (RSide rSide)::(LeafId nt)::tree ->
                ((RSide ((GrammarParser.NonTerm nt)::rSide))::tree,pOrder)
            | (LeafId nt)::tree -> ((RSide [GrammarParser.NonTerm nt])::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[10] ProdRight -> mid ProdRight 
        (fun tree pOrder ->
            match tree with
            | (RSide rSide)::tree ->
                ((RSide (GrammarParser.Delim::rSide))::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[11] ProdRight -> 
        (fun tree pOrder -> ((RSide [])::tree,pOrder))
        //[12] Token -> String as String 
        (fun tree pOrder ->
            match tree with
            | tVal::tName::tree ->
                ((TokenAtt [tName;tVal])::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[13] Token -> String 
        (fun tree pOrder ->
            match tree with
            | tName::tree ->
                ((TokenAtt [tName])::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[14] BangToken -> StringTuple 
        (fun tree pOrder ->
            match tree with
            | (TupleArgs args)::tree ->
                data_add2btokens args
                (tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[15] Group -> String lbrace StringTuple rbrace 
        (fun tree pOrder ->
            match tree with
            | (TupleArgs gElms)::(LeafStr gName)::tree ->
                data_add2groups gName gElms
                (tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[16] Identifier -> identifier 
        (fun tree pOrder -> (tree,pOrder))
        //[17] StringTuple -> String comma StringTuple 
        (fun tree pOrder ->
            match tree with
            | (TupleArgs args)::arg::tree ->
                ((TupleArgs (arg::args))::tree,pOrder)
            | arg::tree ->
                ((TupleArgs [arg])::tree,pOrder)
            | _ -> (tree,pOrder)
            )
        //[18] StringTuple -> String 
        (fun tree pOrder ->
            match tree with
            | (TupleArgs args)::arg::tree ->
                ((TupleArgs (arg::args))::tree,pOrder)
            | arg::tree ->
                ((TupleArgs [arg])::tree,pOrder)
            | _  -> (tree,pOrder)
            )
        //[19] String -> string 
        (fun tree pOrder -> (tree,pOrder))
        //[20] Num -> num 
        (fun tree pOrder -> (tree,pOrder))
        |]
    let lexer inStr =
        let tokensL = new List<Tokens>()
        let addToken (tGroup : GroupCollection) =
            if tGroup.[1].Value <> "" then
                tokensL.Add(Token_string tGroup.[1].Value)
            elif tGroup.[2].Value <> "" then
                tokensL.Add(Token_identifier tGroup.[2].Value)
            elif tGroup.[3].Value <> "" then
                tokensL.Add(Token_num tGroup.[3].Value)
            elif tGroup.[4].Value <> "" then
                tokensL.Add(Token_prod)
            elif tGroup.[5].Value <> "" then
                tokensL.Add(Token_prec)
            elif tGroup.[6].Value <> "" then
                tokensL.Add(Token_assoc)
            elif tGroup.[7].Value <> "" then
                tokensL.Add(Token_group)
            elif tGroup.[8].Value <> "" then
                tokensL.Add(Token_token)
            elif tGroup.[9].Value <> "" then
                tokensL.Add(Token_rarrow)
            elif tGroup.[10].Value <> "" then
                tokensL.Add(Token_as)
            elif tGroup.[11].Value <> "" then
                tokensL.Add(Token_comma)
            elif tGroup.[12].Value <> "" then
                tokensL.Add(Token_mid)
            elif tGroup.[13].Value <> "" then
                tokensL.Add(Token_lbrace)
            elif tGroup.[14].Value <> "" then
                tokensL.Add(Token_rbrace)
            elif tGroup.[15].Value <> "" then
                tokensL.Add(Token_direction tGroup.[14].Value)
            elif tGroup.[16].Value <> "" then
                tokensL.Add(Token_bang)
            else ()
        //reproduce regex by machine
        let regToken =
            "\"([^\"]*)\"|"+
            "([A-Z][a-zA-Z']*)|"+
            "([0-9]+)|"+
            "(prod)|"+
            "(prec)|"+
            "(assoc)|"+
            "(group)|"+
            "(token)|"+
            "(->)|"+
            "(as)|"+
            "(,)|"+
            "(\\|)|"+
            "(\\{)|"+
            "(\\})|"+
            "(left|right)|"+
            "(!)|"+
            "#[^\\n]*\\n|"+
            "[\\n\\t\\r ]+"
        let mfun (m : Match) =
            addToken m.Groups
            ""
        let residueStr = Regex.Replace(inStr,regToken,mfun)
        if residueStr <> "" then
            failwith (sprintf "garbage in expression: %c" residueStr.[0])
        else
            tokensL.Add(Token_dollar)
            Array.init tokensL.Count (fun i -> tokensL.[i])
    let actionTable = [|
        //s0
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s1
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Accept)
            dict
            )(new Dictionary<string,Action>())
        //s2
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s3
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 10)
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s4
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s5
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s6
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Shift 15)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s7
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s8
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Shift 18)
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s9
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Reduce 19)
            dict.Add("assoc",Reduce 19)
            dict.Add("bang",Reduce 19)
            dict.Add("comma",Reduce 19)
            dict.Add("direction",Reduce 19)
            dict.Add("group",Reduce 19)
            dict.Add("identifier",Reduce 19)
            dict.Add("lbrace",Reduce 19)
            dict.Add("mid",Reduce 19)
            dict.Add("num",Reduce 19)
            dict.Add("prec",Reduce 19)
            dict.Add("prod",Reduce 19)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Reduce 19)
            dict.Add("string",Reduce 19)
            dict.Add("token",Reduce 19)
            dict.Add("$",Reduce 19)
            dict
            )(new Dictionary<string,Action>())
        //s10
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s11
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s12
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Shift 23)
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s13
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Shift 25)
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s14
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Shift 26)
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s15
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 16)
            dict.Add("bang",Reduce 16)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 16)
            dict.Add("identifier",Reduce 16)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Reduce 16)
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 16)
            dict.Add("prod",Reduce 16)
            dict.Add("rarrow",Reduce 16)
            dict.Add("rbrace",Error "")
            dict.Add("string",Reduce 16)
            dict.Add("token",Reduce 16)
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s16
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Shift 27)
            dict.Add("assoc",Reduce 13)
            dict.Add("bang",Reduce 13)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 13)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 13)
            dict.Add("prod",Reduce 13)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 13)
            dict.Add("$",Reduce 13)
            dict
            )(new Dictionary<string,Action>())
        //s17
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s18
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s19
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s20
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 18)
            dict.Add("bang",Reduce 18)
            dict.Add("comma",Shift 31)
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 18)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 18)
            dict.Add("prod",Reduce 18)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Reduce 18)
            dict.Add("string",Error "")
            dict.Add("token",Reduce 18)
            dict.Add("$",Reduce 18)
            dict
            )(new Dictionary<string,Action>())
        //s21
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 14)
            dict.Add("bang",Reduce 14)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 14)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 14)
            dict.Add("prod",Reduce 14)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 14)
            dict.Add("$",Reduce 14)
            dict
            )(new Dictionary<string,Action>())
        //s22
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 6)
            dict
            )(new Dictionary<string,Action>())
        //s23
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s24
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s25
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 20)
            dict.Add("bang",Reduce 20)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 20)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 20)
            dict.Add("prod",Reduce 20)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 20)
            dict.Add("$",Reduce 20)
            dict
            )(new Dictionary<string,Action>())
        //s26
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 11)
            dict.Add("bang",Reduce 11)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 11)
            dict.Add("identifier",Shift 15)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Shift 37)
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 11)
            dict.Add("prod",Reduce 11)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Reduce 11)
            dict.Add("$",Reduce 11)
            dict
            )(new Dictionary<string,Action>())
        //s27
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s28
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 4)
            dict
            )(new Dictionary<string,Action>())
        //s29
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 3)
            dict
            )(new Dictionary<string,Action>())
        //s30
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 5)
            dict
            )(new Dictionary<string,Action>())
        //s31
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s32
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Shift 40)
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Error "")
            dict
            )(new Dictionary<string,Action>())
        //s33
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 2)
            dict
            )(new Dictionary<string,Action>())
        //s34
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 11)
            dict.Add("bang",Reduce 11)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 11)
            dict.Add("identifier",Shift 15)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Shift 37)
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 11)
            dict.Add("prod",Reduce 11)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Reduce 11)
            dict.Add("$",Reduce 11)
            dict
            )(new Dictionary<string,Action>())
        //s35
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Shift 2)
            dict.Add("bang",Shift 3)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Shift 4)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Shift 5)
            dict.Add("prod",Shift 6)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Shift 7)
            dict.Add("$",Reduce 7)
            dict
            )(new Dictionary<string,Action>())
        //s36
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 11)
            dict.Add("bang",Reduce 11)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 11)
            dict.Add("identifier",Shift 15)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Shift 37)
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 11)
            dict.Add("prod",Reduce 11)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Reduce 11)
            dict.Add("$",Reduce 11)
            dict
            )(new Dictionary<string,Action>())
        //s37
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 11)
            dict.Add("bang",Reduce 11)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 11)
            dict.Add("identifier",Shift 15)
            dict.Add("lbrace",Error "")
            dict.Add("mid",Shift 37)
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 11)
            dict.Add("prod",Reduce 11)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Shift 9)
            dict.Add("token",Reduce 11)
            dict.Add("$",Reduce 11)
            dict
            )(new Dictionary<string,Action>())
        //s38
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 12)
            dict.Add("bang",Reduce 12)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 12)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 12)
            dict.Add("prod",Reduce 12)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 12)
            dict.Add("$",Reduce 12)
            dict
            )(new Dictionary<string,Action>())
        //s39
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 17)
            dict.Add("bang",Reduce 17)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 17)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 17)
            dict.Add("prod",Reduce 17)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Reduce 17)
            dict.Add("string",Error "")
            dict.Add("token",Reduce 17)
            dict.Add("$",Reduce 17)
            dict
            )(new Dictionary<string,Action>())
        //s40
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 15)
            dict.Add("bang",Reduce 15)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 15)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 15)
            dict.Add("prod",Reduce 15)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 15)
            dict.Add("$",Reduce 15)
            dict
            )(new Dictionary<string,Action>())
        //s41
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 9)
            dict.Add("bang",Reduce 9)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 9)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 9)
            dict.Add("prod",Reduce 9)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 9)
            dict.Add("$",Reduce 9)
            dict
            )(new Dictionary<string,Action>())
        //s42
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Error "")
            dict.Add("bang",Error "")
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Error "")
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Error "")
            dict.Add("prod",Error "")
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Error "")
            dict.Add("$",Reduce 1)
            dict
            )(new Dictionary<string,Action>())
        //s43
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 8)
            dict.Add("bang",Reduce 8)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 8)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 8)
            dict.Add("prod",Reduce 8)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 8)
            dict.Add("$",Reduce 8)
            dict
            )(new Dictionary<string,Action>())
        //s44
        (fun (dict : Dictionary<string,Action>) ->
            dict.Add("as",Error "")
            dict.Add("assoc",Reduce 10)
            dict.Add("bang",Reduce 10)
            dict.Add("comma",Error "")
            dict.Add("direction",Error "")
            dict.Add("group",Reduce 10)
            dict.Add("identifier",Error "")
            dict.Add("lbrace",Error "")
            dict.Add("mid",Error "")
            dict.Add("num",Error "")
            dict.Add("prec",Reduce 10)
            dict.Add("prod",Reduce 10)
            dict.Add("rarrow",Error "")
            dict.Add("rbrace",Error "")
            dict.Add("string",Error "")
            dict.Add("token",Reduce 10)
            dict.Add("$",Reduce 10)
            dict
            )(new Dictionary<string,Action>())
        |]
    let gotoTable = [|
        //s0
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 1)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s1
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s2
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 8)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s3
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s4
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",Some 11)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 12)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s5
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 13)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s6
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",Some 14)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s7
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 16)
            dict.Add("StringTuple",None)
            dict.Add("Token",Some 17)
            dict
            )(new Dictionary<string,Option<int>>())
        //s8
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s9
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s10
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",Some 19)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 20)
            dict.Add("StringTuple",Some 21)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s11
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 22)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s12
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s13
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",Some 24)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s14
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s15
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s16
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s17
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 28)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s18
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 29)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s19
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 30)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s20
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s21
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s22
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s23
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 20)
            dict.Add("StringTuple",Some 32)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s24
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 33)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s25
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s26
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",Some 34)
            dict.Add("Num",None)
            dict.Add("ProdRight",Some 35)
            dict.Add("String",Some 36)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s27
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 38)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s28
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s29
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s30
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s31
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",Some 20)
            dict.Add("StringTuple",Some 39)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s32
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s33
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s34
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",Some 34)
            dict.Add("Num",None)
            dict.Add("ProdRight",Some 41)
            dict.Add("String",Some 36)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s35
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",Some 42)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s36
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",Some 34)
            dict.Add("Num",None)
            dict.Add("ProdRight",Some 43)
            dict.Add("String",Some 36)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s37
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",Some 34)
            dict.Add("Num",None)
            dict.Add("ProdRight",Some 44)
            dict.Add("String",Some 36)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s38
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s39
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s40
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s41
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s42
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s43
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        //s44
        (fun (dict : Dictionary<string,Option<int>>) ->
            dict.Add("BangToken",None)
            dict.Add("Exp",None)
            dict.Add("Group",None)
            dict.Add("Identifier",None)
            dict.Add("Num",None)
            dict.Add("ProdRight",None)
            dict.Add("String",None)
            dict.Add("StringTuple",None)
            dict.Add("Token",None)
            dict
            )(new Dictionary<string,Option<int>>())
        |]
    let productions_str = [|
        ("__",[|"Exp"|]) //0
        ("Exp",[|"prod";"Identifier";"rarrow";"ProdRight";"Exp"|]) //1
        ("Exp",[|"prec";"String";"Num";"Exp"|]) //2
        ("Exp",[|"assoc";"String";"direction";"Exp"|]) //3
        ("Exp",[|"token";"Token";"Exp"|]) //4
        ("Exp",[|"bang";"token";"BangToken";"Exp"|]) //5
        ("Exp",[|"group";"Group";"Exp"|]) //6
        ("Exp",[||]) //7
        ("ProdRight",[|"String";"ProdRight"|]) //8
        ("ProdRight",[|"Identifier";"ProdRight"|]) //9
        ("ProdRight",[|"mid";"ProdRight"|]) //10
        ("ProdRight",[||]) //11
        ("Token",[|"String";"as";"String"|]) //12
        ("Token",[|"String"|]) //13
        ("BangToken",[|"StringTuple"|]) //14
        ("Group",[|"String";"lbrace";"StringTuple";"rbrace"|]) //15
        ("Identifier",[|"identifier"|]) //16
        ("StringTuple",[|"String";"comma";"StringTuple"|]) //17
        ("StringTuple",[|"String"|]) //18
        ("String",[|"string"|]) //19
        ("Num",[|"num"|]) //20
        |]
    let parser tokens =
        let tLen = Array.length tokens
        let popOne = function
            | s::stack -> (s,stack)
            | _ -> failwith "parser: popOne error"
        let popN n stack =
            let rec exec n0 acc = function
                | stack when n0 = n -> (acc,stack) 
                | s::stack -> exec (n0 + 1) (acc @ [s]) stack
                | [] ->
                    failwith "parser: popN error"
            exec 0 [] stack
        let pushGoto stack = function
            | Some g -> g::stack
            | None -> stack
        let getNextFromInput i =
            if i < tLen - 1 then
                (i + 1,tokens.[i+1])
            else failwith "parser: getNextFromInputError"
        let token2lookup = function
            | Token_as -> "as"
            | Token_assoc -> "assoc"
            | Token_bang -> "bang"
            | Token_comma -> "comma"
            | Token_direction _ -> "direction"
            | Token_group -> "group"
            | Token_identifier _ -> "identifier"
            | Token_lbrace -> "lbrace"
            | Token_mid -> "mid"
            | Token_num _ -> "num"
            | Token_prec -> "prec"
            | Token_prod -> "prod"
            | Token_rarrow -> "rarrow"
            | Token_rbrace -> "rbrace"
            | Token_string _ -> "string"
            | Token_token -> "token"
            | Token_dollar -> "$"
        let rec exec (i,a) sStack tree pOrder =
            let (s,_) = popOne sStack
            match actionTable.[s].[token2lookup a] with
            | Shift t ->
                let newStack = t::sStack
                let newTree = addLeaf2tree tree a
                let (i,a) = getNextFromInput i
                exec (i,a) newStack newTree pOrder
            | Reduce r ->
                let (prod,rSide,prodF) =
                    let (a,b) = productions_str.[r]
                    let f = productions_fun.[r]
                    (a,b,f)
                let (newTree,newPOrder) = prodF tree pOrder
                let betaLen = Array.length rSide
                let (_,newStack) = popN betaLen sStack
                let (t,_) = popOne newStack
                let newStack = pushGoto newStack gotoTable.[t].[prod]
                exec (i,a) newStack newTree newPOrder
            | Accept -> (tree,pOrder)
            | Error msg ->
                failwith (sprintf "syntax error: %s" msg)
        exec (0,tokens.[0]) [0] initTreeStack []
    let parse str =
        let lexed = lexer str
        let parsed = parser lexed
        parsed