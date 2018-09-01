#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

module Environment =
    let [<Literal>] APPVEYOR = "APPVEYOR"
    let [<Literal>] APPVEYOR_REPO_COMMIT = "APPVEYOR_REPO_COMMIT"
    let [<Literal>] APPVEYOR_REPO_TAG_NAME = "APPVEYOR_REPO_TAG_NAME"
    let [<Literal>] BUILD_CONFIGURATION = "BuildConfiguration"

type UpdateAssemblyInfo = unit -> ProcessResult
type FullSemVer = string
type NuGetVer = string

module GitVersion =
    let private exec args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = args })
            (System.TimeSpan.FromMinutes 2.)

    let private execRemote commit args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = (sprintf "/url https://github.com/Kimserey/hello-world-nuget.git /b master /c %s %s" commit args) })
            (System.TimeSpan.FromMinutes 2.)

    let private version (result: ProcessResult) =
        result.Messages |> List.head

    let (updateAssemblyInfo, fullSemVer, nuGetVer): UpdateAssemblyInfo * FullSemVer * NuGetVer =
        let commit =
            Environment.environVar Environment.APPVEYOR_REPO_COMMIT

        match Environment.environVarOrNone Environment.APPVEYOR_REPO_TAG_NAME with
        | Some v ->
            printfn "Executing gitversion on detached HEAD. %s." commit
            ((fun () -> execRemote commit "/updateassemblyinfo"), v, v)
        | None ->
            printfn "Executing gitversion on master branch. %s" commit
            ((fun () -> exec "/updateassemblyinfo"),
                exec "/showvariable FullSemVer" |> version,
                exec "/showvariable NuGetVersionV2" |> version
        )

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/artifacts"
    |> Shell.cleanDirs
)

Target.create "PatchAssemblyInfo" (fun _ ->
    GitVersion.updateAssemblyInfo()
    |> fun res -> res.Messages
    |> List.iter (printfn "%s")
)

Target.create "UpdateBuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" GitVersion.fullSemVer)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }))
)

Target.create "Pack" (fun _ ->
    !! "**/Groomgy.*.*proj" -- "**/Groomgy.*Test.*proj"
    |> Seq.toList
    |> List.iter (fun proj ->
        DotNet.pack
            (fun opts ->
                { opts with
                    Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug
                    OutputPath = Some "../artifacts"
                    NoBuild = true
                    Common = { opts.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" GitVersion.nuGetVer) }
                })
            proj
    )
)

Target.create "All" ignore

"Clean"
  =?> ("PatchAssemblyInfo", Environment.environVarAsBool Environment.APPVEYOR)
  =?> ("UpdateBuildVersion", Environment.environVarAsBool Environment.APPVEYOR)
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"