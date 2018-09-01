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
    let [<Literal>] REPOSITORY = "https://github.com/Kimserey/hello-world-nuget.git"

module GitVersion =
    module Process =
        let exec f =
            Process.execWithResult f (System.TimeSpan.FromMinutes 2.)

    let private exec commit args =
        Process.exec (fun info -> { info with FileName = "gitversion"; Arguments = sprintf "/url %s /b master /dynamicRepoLocation .\gitversion /c %s %s" Environment.REPOSITORY commit args })

    let private getResult (result: ProcessResult) =
        result.Messages |> List.head

    let get =
        let mutable value = None

        fun () ->
            match value with
            | None ->
                let commit =
                    match Environment.environVarOrNone Environment.APPVEYOR_REPO_COMMIT with
                    | Some c -> c
                    | None -> Process.exec (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" }) |> getResult

                printfn "Executing gitversion on detached HEAD. %s." commit

                match Environment.environVarOrNone Environment.APPVEYOR_REPO_TAG_NAME with
                | Some v ->
                    printfn "Full sementic versioning: '%s', NuGet sementic versioning: '%s'" v v
                    value <- Some ((fun () -> exec commit "/updateassemblyinfo"), v, v)
                | None ->
                    let fullSemVer = exec commit "/showvariable FullSemVer" |> getResult
                    let nuGetVer = exec commit "/showvariable NuGetVersionV2" |> getResult
                    printfn "Full sementic versioning: '%s', NuGet sementic versioning: '%s'" fullSemVer nuGetVer
                    value <- Some ((fun () -> exec commit "/updateassemblyinfo"), fullSemVer, nuGetVer)

                Option.get value
            | Some v -> v

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "artifacts"
    ++ "gitversion"
    |> Seq.map(fun x -> printfn "%s" x; x)
    |> Shell.cleanDirs
)

Target.create "PatchAssemblyInfo" (fun _ ->
    let (updateAssemblyInfo, _, _) = GitVersion.get()

    updateAssemblyInfo()
    |> fun res -> res.Messages
    |> List.iter (printfn "%s")
)

Target.create "UpdateBuildVersion" (fun _ ->
    let (_, fullSemVer, _) = GitVersion.get()

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" fullSemVer)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }))
)

Target.create "Pack" (fun _ ->
    let (_, _, nuGetVer) = GitVersion.get()

    !! "**/Groomgy.*.*proj" -- "**/Groomgy.*Test.*proj"
    |> Seq.toList
    |> List.iter (fun proj ->
        DotNet.pack
            (fun opts ->
                { opts with
                    Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug
                    OutputPath = Some "../artifacts"
                    NoBuild = true
                    Common = { opts.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" nuGetVer) }
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