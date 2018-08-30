#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

module GitVersion =
    let exec args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = args })
            (System.TimeSpan.FromMinutes 2.)

    let updateAssemblyInfo () =
        exec "/updateassemblyinfo"
        |> ignore

    let private getVar var =
        let result = exec <| sprintf "/showvariable %s" var
        result.Messages |> List.head

    let fullSemVer () = getVar "FullSemVer"
    let nugetVer () = getVar "NuGetVersionV2"

module AppVeyor =
    let updateBuildVersion version =
        Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" version)
        |> ignore

module Environment =
    let configuration =
        DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "artifacts"
    |> Shell.cleanDirs
)

Target.create "Version" (fun _ ->
    GitVersion.updateAssemblyInfo()
    AppVeyor.updateBuildVersion (GitVersion.fullSemVer())
)

Target.create "DotNetBuild" (fun _ ->
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
                Common = { opts.Common with CustomParams = Some <| sprintf "/p:PackageVersion=%s" (GitVersion.nugetVer()) }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  ==> "Version"
  ==> "DotNetBuild"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "All"