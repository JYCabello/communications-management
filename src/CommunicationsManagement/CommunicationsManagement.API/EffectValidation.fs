module CommunicationsManagement.API.EffectValidation

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type ValidationError = { FieldName: string; Error: string }

type EffectValidationResult<'a> =
| Valid of 'a
| Invalid of ValidationError list
| Fail of DomainError

type TaskValidation<'a> = Task<EffectValidationResult<'a>>

let mapTV ()

let zipTV (left: TaskValidation<'a>) (right: TaskValidation<'b>) : TaskValidation<'a * 'b> =
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
  member inline _.Return(value: 'ok) : TaskValidation<'ok> = value |> Valid |> Task.singleton

  member inline _.ReturnFrom(result: TaskValidation<'ok>) : TaskValidation<'ok> = result

  member inline _.Bind
    (
      result: TaskValidation<'okInput>,
      [<InlineIfLambda>] binder: 'okInput -> TaskValidation<'okOutput>
    ) : TaskValidation<'okOutput> =
      taskResult {
        let! okIn = result
        return! binder okIn
      }

  member inline this.Zero() : TaskValidation<unit> = this.Return()

  member inline _.BindReturn
    (
      input: TaskValidation<'okInput>,
      [<InlineIfLambda>] mapper: 'okInput -> 'okOutput
    ) : TaskValidation<'okOutput> =
    TaskResult.map mapper input

  member inline _.MergeSources
    (
      left: TaskValidation<'left>,
      right: TaskValidation<'right>
    ) : TaskValidation<'left * 'right> =
    zipTV left right


let taskValidation = TaskValidationBuilder()
