module CommunicationsManagement.API.Validation

open System.Threading.Tasks
open FsToolkit.ErrorHandling

type ValidationError = { FieldName: string; Error: string }

type ValidationResult<'a> = Task<Result<'a, ValidationError list>>

type TaskValidationBuilder() =
  member inline _.Return(value: 'ok) : ValidationResult<'ok> = TaskResult.ok value

  member inline _.ReturnFrom(result: ValidationResult<'ok>) : ValidationResult<'ok> = result

  member inline _.Bind
    (
      result: ValidationResult<'okInput>,
      [<InlineIfLambda>] binder: 'okInput -> ValidationResult<'okOutput>
    ) : ValidationResult<'okOutput> =
      taskResult {
        let! okIn = result
        return! binder okIn
      }

  member inline this.Zero() : ValidationResult<unit> = this.Return()

  member inline _.BindReturn
    (
      input: ValidationResult<'okInput>,
      [<InlineIfLambda>] mapper: 'okInput -> 'okOutput
    ) : ValidationResult<'okOutput> =
    TaskResult.map mapper input

  member inline _.MergeSources
    (
      left: ValidationResult<'left>,
      right: ValidationResult<'right>
    ) : ValidationResult<'left * 'right> =
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

let taskValidation = TaskValidationBuilder()
