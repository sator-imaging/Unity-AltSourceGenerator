# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased](https://github.com/sator-imaging/Unity-AltSourceGenerator)

- nothing yet



## [3.0.0](https://github.com/sator-imaging/Unity-AltSourceGenerator/releases/tag/v3.0.0)


### API Changes 😉

#### `USGEngine.ProcessFile()` will be removed

use `USGUtility.ForceGenerateByType(typeof(...))` instead.

#### `USGUtility.**ByName()` will be removed

methods still exist but obsolete. use `USGUtility.**ByType()` instead.



## [2.0.0](https://github.com/sator-imaging/Unity-AltSourceGenerator/releases/tag/v2.0.0)


### Breaking Changes ;-)

#### USGEngine.ProcessFile(string assetsRelPath)

signature changed:
- `ProcessFile(string assetsRelPath, bool ignoreOverwriteSettingOnAttribute, bool autoRunReferencingEmittersNow = false)`

#### ~~public~~ static bool USGEngine.IgnoreOverwriteSettingByAttribute

now private. use `ProcessFile(path, *true*)` instead.

#### USGUtility.ForceGenerateByName(string clsName, bool showInProjectPanel = *false*)

`showInProjectPanel` now false by default.

#### usg\<T>(params string[] memberNames)

`global::` namespace will be added.

#### usg(Type cls, params string[] memberNames)

signature changed:
- `usg(object valueOrType, bool isFullName = true)`
