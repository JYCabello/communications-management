module CommunicationsManagement.API.EffectfulValidate

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type ValidationError = { FieldName: string; Error: string }


type ValidateResult<'a> =
| Valid of 'a
| Invalid of ValidationError list

type EffectValidateResult<'a> =
| Valid of 'a
| Invalid of ValidationError list
| Fail of DomainError

type TaskEffectValidateResult<'a> = Task<EffectValidateResult<'a>>

module EffectValidate =
  let valid a : TaskEffectValidateResult<'a> = a |> Valid |> Task.singleton
  let invalid ve : TaskEffectValidateResult<'a> = ve |> Invalid |> Task.singleton
  let fail de : TaskEffectValidateResult<'a> = de |> Fail |> Task.singleton

let mapTV f tv =
  task {
    let! v = tv
    return
      match v with
      | Valid a -> a |> f |> Valid
      | Invalid ve -> ve |> Invalid
      | Fail de -> de |> Fail
  }

let flatten (tvtv: TaskEffectValidateResult<TaskEffectValidateResult<'a>>) : TaskEffectValidateResult<'a> =
  task {
    let! tvt = tvtv
    return!
      match tvt with
      | Valid tv -> tv
      | Invalid ve -> ve |> Invalid |> Task.singleton
      | Fail de -> de |> Fail |> Task.singleton
  }
  
let bindTV (f: 'a -> TaskEffectValidateResult<'b>) tv : TaskEffectValidateResult<'b> =
  mapTV f tv |> flatten

let zipTV (left: TaskEffectValidateResult<'a>) (right: TaskEffectValidateResult<'b>) : TaskEffectValidateResult<'a * 'b> =
  task {
    let! l = left
    let! r = right
    return
      match l with
      | Valid a ->
        match r with
        | Valid b -> Valid (a, b)
        | Invalid ver -> Invalid ver
        | Fail fr -> Fail fr
      | Invalid vel ->
        match r with
        | Valid _ -> Invalid vel
        | Invalid ver -> vel @ ver |> Invalid
        | Fail fr -> Fail fr
      | Fail fl -> Fail fl
  }


type TaskValidationBuilder() =
  member inline _.Return(value: 'ok) : TaskEffectValidateResult<'ok> = value |> Valid |> Task.singleton

  member inline _.ReturnFrom(result: TaskEffectValidateResult<'ok>) : TaskEffectValidateResult<'ok> = result

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


let taskValidation = TaskValidationBuilder()
