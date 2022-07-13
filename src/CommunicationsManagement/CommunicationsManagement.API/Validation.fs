module CommunicationsManagement.API.Validation

open System.Threading.Tasks
open FsToolkit.ErrorHandling

type ValidationError = { FieldName: string; Error: string }

type TaskValidation<'a> = Task<Result<'a, ValidationError list>>

type TaskValidationBuilder() =
  member inline _.Return(value: 'ok) : TaskValidation<'ok> = TaskResult.ok value

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
    task {
      let! l = left
      let! r = right
      return
        match l with
        | Ok lv ->
          match r with
          | Ok rv -> Ok (lv, rv)
          | Error re -> Error re
        | Error le ->
          match r with
          | Ok _ -> Error le
          | Error re -> Error (le @ re)
    }
  
  member inline _.Source(tr: Task<Result<'a, ValidationError>>): TaskValidation<'a> =
    tr |> TaskResult.mapError (fun e -> [e])

let taskValidation = TaskValidationBuilder()
