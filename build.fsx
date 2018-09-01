#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let [<Literal>] commit = "APPVEYOR_REPO_COMMIT"
let [<Literal>] tag = "APPVEYOR_REPO_TAG_NAME"

type UpdateAssemblyInfo = unit -> unit
type FullSemVer = string
type NuGetVer = string

module GitVersion =
    let private exec args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = args })
            (System.TimeSpan.FromMinutes 2.)

    let private execRemote args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = (sprintf "/url https://github.com/Kimserey/hello-world-nuget.git /c %s %s" (Environment.environVar commit) args) })
            (System.TimeSpan.FromMinutes 2.)

    let private version (result: ProcessResult) =
        result.Messages |> List.head

    let (updateAssemblyInfo, fullSemVer, nuGetVer): UpdateAssemblyInfo * FullSemVer * NuGetVer =
        match Environment.environVarOrNone tag with
        | Some v -> ((fun () -> execRemote "/updateassemblyinfo" |> ignore), v, v)
        | None ->
            ((fun () -> exec "/updateassemblyinfo" |> ignore),
                exec "/showvariable FullSemVer" |> version,
                exec "/showvariable NuGetVersionV2" |> version
        )

module Environment =
    let configuration =
        DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun t ->
    !! "**/bin"
    ++ "**/obj"
    |> Shell.cleanDirs
)

Target.create "PatchAssemblyInfo" (fun _ ->
    GitVersion.updateAssemblyInfo()
)

Target.create "UpdateBuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" GitVersion.fullSemVer)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Environment.configuration }))
)

Target.create "Pack" (fun _ ->
    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = Environment.configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" GitVersion.nuGetVer) }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

Target.create "x" ignore

"Clean"
  ==> "PatchAssemblyInfo"
  ==> "UpdateBuildVersion"
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"