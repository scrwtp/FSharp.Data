﻿namespace FSharp.Data.RuntimeImplementation

open System
open System.Globalization
open FSharp.Data.Json
open FSharp.Data.RuntimeImplementation.TypeInference

/// Underlying representation of the generated JSON types
type JsonDocument (json:JsonValue) =
  /// Returns the raw JSON value that is represented by the generated type
  member x.JsonValue = json

type JsonOperations = 
  // Trivial operations that return primitive values
  static member GetString(value) = JsonValue.asString value
  static member GetBoolean(value) = JsonValue.asBoolean value
  static member GetFloat(value, culture) = JsonValue.asFloat(value, Operations.GetCulture(culture))
  static member GetDecimal(value, culture) = JsonValue.asDecimal(value, Operations.GetCulture(culture))
  static member GetInteger(value, culture) = JsonValue.asInteger(value, Operations.GetCulture(culture))
  static member GetInteger64(value, culture) = JsonValue.asInteger64(value, Operations.GetCulture(culture))
  static member GetProperty(value, name) = JsonValue.getProperty name value

  /// Converts JSON array to array of target types
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  static member ConvertArray(value, packer:Func<_,_>, f:Func<_,_>) = 
    value |> JsonValue.asArray |> Array.map (packer.Invoke >> f.Invoke)

  /// Get optional property of a specified type
  static member ConvertOptionalProperty(value, name, packer:Func<_,_>, f:Func<_,_>) =     
    value |> JsonValue.tryGetProperty name |> Option.map (packer.Invoke >> f.Invoke)

  /// Returns all array values that match the specified tag
  /// (Follows the same pattern as ConvertXyz functions above)
  static member GetArrayChildrenByTypeTag(doc:JsonValue, tag, pack:Func<_,_>, f:Func<_,_>) = 
    let tag = InferedTypeTag.ParseCode tag
    let matchesTag = function
      | JsonValue.Null -> false
      | JsonValue.Boolean _ -> tag = InferedTypeTag.Boolean
      | JsonValue.Number _ -> tag = InferedTypeTag.Number
      | JsonValue.BigNumber _ -> tag = InferedTypeTag.Number
      | JsonValue.Array _ -> tag = InferedTypeTag.Collection
      | JsonValue.Object _ -> tag = InferedTypeTag.Record None
      | JsonValue.String _ -> tag = InferedTypeTag.String
    match doc with
    | JsonValue.Array ar ->
        ar 
        |> Array.filter matchesTag 
        |> Array.map (pack.Invoke >> f.Invoke)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns single or no value from an array matching the specified tag
  static member TryGetArrayChildByTypeTag(doc:JsonValue, tag, pack:Func<_,_>, f:Func<_,_>) = 
    match JsonOperations.GetArrayChildrenByTypeTag(doc, tag, pack, f) with
    | [| the |] -> Some the
    | [| |] -> None
    | _ -> failwith "JSON mismatch: Expected Array with single or no elements."

  /// Returns a single array children that matches the specified tag
  static member GetArrayChildByTypeTag(value:JsonValue, tag) = 
    match JsonOperations.GetArrayChildrenByTypeTag(value, tag, new Func<_,_>(id), new Func<_,_>(id)) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

  /// Returns a single or no value by tag type
  static member TryGetValueByTypeTag(value:JsonValue, tag, pack, f) = 
    // Build a fake array and reuse `GetArrayChildByTypeTag`
    let arrayValue = JsonValue.Array [| value |]
    JsonOperations.TryGetArrayChildByTypeTag(arrayValue, tag, pack, f) 
