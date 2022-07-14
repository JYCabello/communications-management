﻿module CommunicationsManagement.API.EffectfulValidate

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type ValidationError = { FieldName: string; Error: string }

type ValidateResult<'a> =
  | Valid of 'a
  | Invalid of ValidationError list

let mapV f v =
  match v with
  | Valid a -> a |> f |> Valid
  | Invalid ve -> ve |> Invalid

let flattenV (vv: ValidateResult<ValidateResult<'a>>) : ValidateResult<'a> =
  match vv with
  | Valid v -> v
  | Invalid iv -> iv |> Invalid

let bindV (f: 'a -> ValidateResult<'b>) vr =
  vr |> mapV f |> flattenV
  
module Validate =
  let valid a : ValidateResult<'a> = a |> Valid
  let invalid ve : ValidateResult<'a> = ve |> Invalid

  let validationError name value : ValidateResult<'a> =
    [ { FieldName = name; Error = value } ] |> invalid

type EffectValidateResult<'a> =
  | EffectValid of 'a
  | EffectInvalid of ValidationError list
  | EffectFail of DomainError

type TaskEffectValidateResult<'a> = Task<EffectValidateResult<'a>>

module EffectValidate =
  let valid a : TaskEffectValidateResult<'a> = a |> EffectValid |> Task.singleton
  let invalid ve : TaskEffectValidateResult<'a> = ve |> EffectInvalid |> Task.singleton

  let validationError name value : TaskEffectValidateResult<'a> =
    [ { FieldName = name; Error = value } ] |> invalid

  let fail de : TaskEffectValidateResult<'a> = de |> EffectFail |> Task.singleton

let mapTV f tv =
  task {
    let! v = tv

    return
      match v with
      | EffectValid a -> a |> f |> EffectValid
      | EffectInvalid ve -> ve |> EffectInvalid
      | EffectFail de -> de |> EffectFail
  }

let flattenTV
  (tvtv: TaskEffectValidateResult<TaskEffectValidateResult<'a>>)
  : TaskEffectValidateResult<'a> =
  task {
    let! tvt = tvtv

    return!
      match tvt with
      | EffectValid tv -> tv
      | EffectInvalid ve -> ve |> EffectInvalid |> Task.singleton
      | EffectFail de -> de |> EffectFail |> Task.singleton
  }

let bindTV (f: 'a -> TaskEffectValidateResult<'b>) tv : TaskEffectValidateResult<'b> =
  mapTV f tv |> flattenTV

let zipTV
  (left: TaskEffectValidateResult<'a>)
  (right: TaskEffectValidateResult<'b>)
  : TaskEffectValidateResult<'a * 'b> =
  task {
    let! l = left
    let! r = right

    return
      match l with
      | EffectValid a ->
        match r with
        | EffectValid b -> EffectValid(a, b)
        | EffectInvalid ver -> EffectInvalid ver
        | EffectFail fr -> EffectFail fr
      | EffectInvalid vel ->
        match r with
        | EffectValid _ -> EffectInvalid vel
        | EffectInvalid ver -> vel @ ver |> EffectInvalid
        | EffectFail fr -> EffectFail fr
      | EffectFail fl -> EffectFail fl
  }


type TaskEffectValidationBuilder() =
  member inline _.Return(value: 'ok) : TaskEffectValidateResult<'ok> =
    value |> EffectValid |> Task.singleton

  member inline _.ReturnFrom
    (result: TaskEffectValidateResult<'ok>)
    : TaskEffectValidateResult<'ok> =
    result

  member inline _.Bind
    (
      result: TaskEffectValidateResult<'okInput>,
      [<InlineIfLambda>] binder: 'okInput -> TaskEffectValidateResult<'okOutput>
    ) : TaskEffectValidateResult<'okOutput> =
    bindTV binder result

  member inline this.Zero() : TaskEffectValidateResult<unit> = this.Return()

  member inline _.BindReturn
    (
      input: TaskEffectValidateResult<'okInput>,
      [<InlineIfLambda>] mapper: 'okInput -> 'okOutput
    ) : TaskEffectValidateResult<'okOutput> =
    mapTV mapper input

  member inline _.MergeSources
    (
      left: TaskEffectValidateResult<'left>,
      right: TaskEffectValidateResult<'right>
    ) : TaskEffectValidateResult<'left * 'right> =
    zipTV left right


let taskEffValid = TaskEffectValidationBuilder()
