module CommunicationsManagement.API.EffectfulValidate

open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type ValidateError = { FieldName: string; Error: string }

type ValidateResult<'a> =
  | Valid of 'a
  | Invalid of ValidateError list

let mapV f v =
  match v with
  | Valid a -> a |> f |> Valid
  | Invalid ve -> ve |> Invalid

let flattenV (vv: ValidateResult<ValidateResult<'a>>) : ValidateResult<'a> =
  match vv with
  | Valid v -> v
  | Invalid iv -> iv |> Invalid

let bindV (f: 'a -> ValidateResult<'b>) vr = vr |> mapV f |> flattenV

let zipV left right =
  match left with
  | Valid a ->
    match right with
    | Valid b -> (a, b) |> Valid
    | Invalid ver -> ver |> Invalid
  | Invalid vel ->
    match right with
    | Valid _ -> vel |> Invalid
    | Invalid ver -> vel @ ver |> Invalid

module Validate =
  let valid a : ValidateResult<'a> = a |> Valid
  let invalid ve : ValidateResult<'a> = ve |> Invalid

  let validationError name value : ValidateResult<'a> =
    [ { FieldName = name; Error = value } ] |> invalid

type ValidateBuilder() =
  member inline _.Return(value: 'ok) : ValidateResult<'ok> = value |> Valid

  member inline _.ReturnFrom(result: ValidateResult<'ok>) : ValidateResult<'ok> = result

  member inline _.Bind
    (
      result: ValidateResult<'okInput>,
      [<InlineIfLambda>] binder: 'okInput -> ValidateResult<'okOutput>
    ) : ValidateResult<'okOutput> =
    bindV binder result

  member inline this.Zero() : ValidateResult<unit> = this.Return()

  member inline _.BindReturn
    (
      input: ValidateResult<'okInput>,
      [<InlineIfLambda>] mapper: 'okInput -> 'okOutput
    ) : ValidateResult<'okOutput> =
    mapV mapper input

  member inline _.MergeSources
    (
      left: ValidateResult<'left>,
      right: ValidateResult<'right>
    ) : ValidateResult<'left * 'right> =
    zipV left right


let validate = ValidateBuilder()

type EffectValidateResult<'a> =
  | EffectValid of 'a
  | EffectInvalid of ValidateError list
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

let validateNotExisting<'a, 'b> q name v (p: IPorts) : TaskEffectValidateResult<'b> =
  task {
    let! r = p.query<'a> q

    return!
      match r with
      | Ok _ -> EffectValidate.validationError name "AlreadyInUse"
      | Error e ->
        match e with
        | NotFound _ -> EffectValidate.valid v
        | err -> EffectValidate.fail err
  }

type TaskEffectValidateBuilder() =
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

  member inline _.Source(vr: ValidateResult<'a>) : TaskEffectValidateResult<'a> =
    match vr with
    | Valid a -> EffectValidate.valid a
    | Invalid ve -> EffectValidate.invalid ve

  member inline _.Source(vr: Task<EffectValidateResult<'a>>) : TaskEffectValidateResult<'a> = vr

let taskEffValid = TaskEffectValidateBuilder()
