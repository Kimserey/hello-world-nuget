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

    let fullSemVer () =
        let result = exec "/showvariable fullsemver"
        result.Messages |> List.head

    let semVer () =
        let result = exec "/showvariable semver"
        result.Messages |> List.head


let configuration =
    DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "artifacts"
    |> Shell.cleanDirs
)

Target.create "Versions" (fun _ ->
    Environment.setEnvironVar "appveyor_build_version" (GitVersion.fullSemVer())
)

Target.create "AssemblyInfo" (fun _ ->
    GitVersion.exec "/updateassemblyinfo" |> ignore
)

Target.create "DotNetBuild" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = configuration }))
)

Target.create "Pack" (fun _ ->
    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some <| sprintf "/p:PackageVersion=%s" (GitVersion.semVer()) }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  ==> "Versions"
  ==> "AssemblyInfo"
  ==> "DotNetBuild"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "All"