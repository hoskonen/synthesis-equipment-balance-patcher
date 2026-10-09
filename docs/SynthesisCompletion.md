# Read-only completion investigation (v0.1b)

## What was established

Synthesis's patcher library is pinned to 0.36.6. The copied reports identify that library version, **not the installed GUI version**. There was no GUI log/process trace accompanying the reports, so the original incident cannot be reproduced exactly from those files.

The tagged [SynthesisPipeline](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Mutagen.Bethesda.Synthesis/Pipeline/SynthesisPipeline.cs) awaits the patch callback, prints `Finished patch.`, and returns before export when `PatcherPreferences.NoPatch` is true. Its CLI `run-patcher` handler then returns success. The state is disposed on return. No output-file polling occurs in this library path.

There is a separate runner/UI gap in Synthesis tag **0.36.6**, commit `d8150fb29d544216081e37e2704e56e3b48460f7`:

1. [RunAPatcher](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Synthesis.Bethesda.Execution/Running/Runner/RunAPatcher.cs) reports starting, awaits the child runner, then calls `FinalizePatcherRun`.
2. [FinalizePatcherRun](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Synthesis.Bethesda.Execution/Running/Runner/FinalizePatcherRun.cs) reports success only when the expected output file or split output exists. With neither, it returns null without a success event.
3. [RunVm](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Synthesis.Bethesda.GUI/ViewModels/Profiles/Running/RunVm.cs) changes a patcher's state from Started to Finished on that success event. Its overall Running flag can still become false when the pipeline returns.
4. [PatcherRunVm](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Synthesis.Bethesda.GUI/ViewModels/Profiles/Running/PatcherRunVm.cs) runs the per-patcher timer while its state remains Started.

Consequently a successful no-export process can leave a patcher row looking indefinitely active. This is a source-supported explanation for the reported symptom, not evidence of an infinite loop in the auditor. A real child process that remains alive after completion would be a different failure requiring its command line, GUI version and log. Placing the auditor after another patcher does not guarantee success reporting: the preceding source plugin and this invocation's expected output path may differ.

## Changes and verification

The auditor logs three boundaries: audit start, reports published/callback returning, and Synthesis pipeline returned with its exit code. Lifecycle output is restricted to audit invocations; settings/version queries retain their machine-readable stdout. No custom shutdown, `Environment.Exit`, dummy plugin, export, background task, or lifecycle preference override was introduced.

Automated integration tests invoke both the pinned in-process CLI pipeline and the real executable in a separate `dotnet` process. Each has a 30-second completion bound and must return code 0. Fixtures cover absent output and an existing preceding output plugin. They check all five reports, input plugin bytes, existing output bytes and write time, and the absence of newly generated ESPs. Process tests additionally check both completion messages. These tests establish patcher process completion, not the behavior of an installed GUI.

Report writing is synchronous, closes every stream before returning, honors cancellation before publication, and propagates failures. No report file handle or pending audit task keeps the process alive.

## Safe operational workaround

For a GUI affected by this runner gap, execute the built auditor's `run-patcher` command directly from the same mod-manager environment. Preserve the GUI log's data/load-order/source paths when reproducing a real run. Launch through MO2 if the load order requires its virtual filesystem. Example with explicit paths:

```powershell
dotnet .\EquipmentBalancePatcher\bin\Debug\net10.0\EquipmentBalancePatcher.dll run-patcher --GameRelease SkyrimSE --DataFolderPath "F:\Skyrim\Data" --LoadOrderFilePath "F:\Profiles\plugins.txt" --OutputPath "F:\Audit\EBP.esp" --ExtraDataFolder "F:\Audit\Settings" --ModKey EBP.esp --LoadOrderIncludesCreationClub true
$LASTEXITCODE
```

Replace paths with the intended environment. The output argument is required by Synthesis's CLI even though NoPatch skips export. Create the supplemental settings folder beforehand; optional `settings.json` there can specify `OutputDirectory`. Use `LoadOrderIncludesCreationClub true` only when the supplied list includes the intended CC plugins; otherwise use false. An optional `--SourcePath` may point to a real preceding patcher's result for read-only inspection. Success requires exit code 0, the final lifecycle message, and the five completed reports. Do not create a placeholder plugin to satisfy GUI completion.

If the GUI's overall run has completed and Back is available, its lingering row timer can be treated as this display issue. For an overall run that remains active or a child still running, capture the GUI version, command and last log lines before diagnosing further. A GUI update is only a fix after its no-export completion behavior has been verified; upgrading the patcher's NuGet dependency alone does not change the GUI runner.
