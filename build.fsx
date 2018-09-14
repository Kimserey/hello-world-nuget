#load ".fake/build.fsx/intellisense.fsx"
#nowarn "3180"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

type FullSemVer = string
type AssemblySemVer = string
type NuGetVersionV2 = string
type Tag = string
type StableReleaseFlag = bool
type Versioning = FullSemVer * AssemblySemVer * NuGetVersionV2 * Tag * StableReleaseFlag

module Environment =
    let [<Literal>] APPVEYOR = "APPVEYOR"
    let [<Literal>] APPVEYOR_BUILD_NUMBER = "APPVEYOR_BUILD_NUMBER"
    let [<Literal>] APPVEYOR_PULL_REQUEST_NUMBER = "APPVEYOR_PULL_REQUEST_NUMBER"
    let [<Literal>] APPVEYOR_REPO_BRANCH = "APPVEYOR_REPO_BRANCH"
    let [<Literal>] APPVEYOR_REPO_COMMIT = "APPVEYOR_REPO_COMMIT"
    let [<Literal>] APPVEYOR_REPO_TAG_NAME = "APPVEYOR_REPO_TAG_NAME"
    let [<Literal>] BUILD_CONFIGURATION = "BuildConfiguration"
    let [<Literal>] REPOSITORY = "https://github.com/Kimserey/hello-world-nuget.git"

module Process =
    let private timeout =
        System.TimeSpan.FromMinutes 2.

    let execWithMultiResult f =
        Process.execWithResult f timeout
        |> fun r -> r.Messages

    let execWithSingleResult f =
        let result = execWithMultiResult f

        match result with
        | [ value ] -> value
        | messages -> failwith <| sprintf "Expected single result but received multiple responses. %s" (messages |> String.concat "\n")

module Git =
    open System.Text.RegularExpressions

    let [<Literal>] private REGEX_ANY_RELEASE_TAG = ".*"
    let [<Literal>] private REGEX_STABLE_RELEASE_TAG = "^([0-9]+)\.([0-9]+)\.([0-9]+)$"

    let private getPreviousTagMatching pattern =
        Process.execWithMultiResult (fun info -> { info with FileName = "git"; Arguments = "for-each-ref refs/tags/ --count=20 --sort=-committerdate --format=\"%(refname:short)\"" })
        |> List.filter (fun tag -> Regex.Match(tag, pattern).Success)
        |> List.skip 1
        |> List.tryHead
        |> Option.defaultValue ""

    let getPreviousTag() = getPreviousTagMatching REGEX_ANY_RELEASE_TAG
    let getPreviousStableTag() = getPreviousTagMatching REGEX_STABLE_RELEASE_TAG

module GitVersion =
    let showVariable =
        let commit =
            match Environment.environVarOrNone Environment.APPVEYOR_REPO_COMMIT with
            | Some c -> c
            | None -> Process.execWithSingleResult (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" })

        let gitVersionDynamic branch variable =
            Process.execWithSingleResult (fun info ->
                { info with
                    FileName = "gitversion"
                    Arguments = sprintf "/showvariable %s /url %s /b remote-%s /dynamicRepoLocation .\gitversion /c %s" variable Environment.REPOSITORY branch commit })

        printfn "Executing gitversion from commit '%s'." commit

        fun variable ->
            let (branch, tag, pr) =
                Environment.environVarOrNone Environment.APPVEYOR_REPO_BRANCH,
                Environment.environVarOrNone Environment.APPVEYOR_REPO_TAG_NAME,
                Environment.environVarOrNone Environment.APPVEYOR_PULL_REQUEST_NUMBER

            printfn "Get variable '%s' for branch '%A' or PR '%A'" variable branch pr

            match branch, tag with
            | Some branch, None when branch = "master" -> gitVersionDynamic branch variable
            | Some branch, Some tag when branch = tag -> gitVersionDynamic branch variable
            | _ -> Process.execWithSingleResult (fun info -> { info with FileName = "gitversion"; Arguments = sprintf "/showvariable %s" variable })

    let get =
        let mutable value: Option<Versioning> = None

        fun () ->
            match value with
            | None ->
                let isStableRelease =
                    String.isNullOrWhiteSpace(showVariable "PreReleaseTag")

                let previousTag =
                    if isStableRelease then
                        Git.getPreviousStableTag()
                    else
                        Git.getPreviousTag()

                value <- Some (
                    showVariable "FullSemVer",
                    showVariable "AssemblySemVer",
                    showVariable "NuGetVersionV2",
                    previousTag,
                    isStableRelease
                )

                Target.createFinal "ClearGitVersionRepositoryLocation" (fun _ ->
                    Shell.deleteDir "gitversion"
                )

                Target.activateFinal "ClearGitVersionRepositoryLocation"

                Option.get value
            | Some v -> v

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/artifacts"
    ++ "gitversion"
    |> Shell.deleteDirs
)

Target.create "PrintVersion" (fun _ ->
    let (fullSemVer, assemblyVer, nugetVer, previousVersion, stableReleaseFlag) = GitVersion.get()
    printfn "Full sementic version: '%s'`" fullSemVer
    printfn "Assembyly version: '%s'" assemblyVer
    printfn "NuGet sementic version: '%s'" nugetVer
    printfn "Previous version: '%s'" previousVersion
    printfn "Stable Release Flag: '%s'" (string stableReleaseFlag)
)

Target.create "UpdateBuildVersion" (fun _ ->
    let (fullSemVer, _, _, _, _) = GitVersion.get()

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s (%s)\"" fullSemVer (Environment.environVar Environment.APPVEYOR_BUILD_NUMBER))
    |> ignore
)

Target.create "GatherReleaseNotes" (fun _ ->
    let (_, _, _, previousTag, _) = GitVersion.get()

    let releaseNotes =
        Process.execWithMultiResult (fun info -> { info with FileName = "git"; Arguments = sprintf "log --pretty=format:\"%%h %%s\" %s..HEAD" previousTag })
        |> String.concat(System.Environment.NewLine)

    printfn "Gathered release notes:"
    printfn "%s" releaseNotes

    Shell.Exec("appveyor", sprintf "SetVariable -Name release_notes -Value \"%s\"" releaseNotes)
    |> ignore
)

Target.create "Build" (fun _ ->
    let (fullSemVer, assemblyVer, _, _, _) = GitVersion.get()

    let setParams (buildOptions: DotNet.BuildOptions) =
        { buildOptions with
            Common = { buildOptions.Common with DotNet.CustomParams = Some (sprintf "/p:Version=%s /p:FileVersion=%s" fullSemVer assemblyVer) }
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }

    !! "**/*.*proj"
    -- "**/Groomgy.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.build setParams)
)

Target.create "Pack" (fun _ ->
    let (_, _, nuGetVer, _, _) = GitVersion.get()

    let setParams (packOptions: DotNet.PackOptions) =
        { packOptions with
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug
            OutputPath = Some "../artifacts"
            NoBuild = true
            Common = { packOptions.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" nuGetVer) } }

    !! "**/*.*proj"
    -- "**/Groomgy.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.pack setParams)
)

Target.create "All" ignore

"Clean"
  ==> "PrintVersion"
  =?> ("UpdateBuildVersion", Environment.environVarAsBool Environment.APPVEYOR)
  =?> ("GatherReleaseNotes", not <| String.isNullOrWhiteSpace(Environment.environVar Environment.APPVEYOR_REPO_TAG_NAME))
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"