open System
open Nethermind.Core
open Nethermind.Core.Crypto
open Nethermind.Core.Encoding

let convertIntToBytes (ar : int array) = 
  ar |> Array.collect (fun x  -> BitConverter.GetBytes (x) )


let convertStrsToBytes (ar : string array) = 
  ar |> Array.collect (fun x -> System.Text.Encoding.ASCII.GetBytes(x)  )

(*
let usingKeccak () =
  let a : int array = Array.init 31 (fun x -> x ) 
  new Keccak(a)
*)

let encodeRlp (arb : byte array) = 
  Rlp.Encode(arb).ToString(true)



[<EntryPoint>]
let main argv =
  //let a : int array = [||]   // Rlp returning with 0x80 
  let a : int array = Array.init 19 (fun x -> x ) 

  let intsRlp = convertIntToBytes >> encodeRlp
  let strsRlp = convertStrsToBytes >> encodeRlp

  a
  |> intsRlp
  |> printfn "Result: %s"


  let b : string array = Array.init 20 (fun x -> x |> sprintf "%d" ) 
  b
  |> strsRlp
  |> printfn "Result: %s"

  0 // return an integer exit code
