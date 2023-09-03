# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased]


## [2.0.0]()

### Breaking Changes ;-)

#### USGEngine.ProcessFile(string assetsRelPath)

signature changed:
- `ProcessFile(string assetsRelPath, bool ignoreOverwriteSettingOnAttribute, bool autoRunReferencingEmittersNow = false)`

#### ~~public~~ static bool USGEngine.IgnoreOverwriteSettingByAttribute

now private. use `ProcessFile(path, *true*)` instead.

#### USGUtility.ForceGenerateByName(string clsName, bool showInProjectPanel = *false*)

`showInProjectPanel` now false by default.

#### usg<T>(params string[] memberNames)

`global::` namespace will be added.

#### usg(Type cls, params string[] memberNames)

signature changed:
- `usg(object valueOrType, bool isFullName = true)`
