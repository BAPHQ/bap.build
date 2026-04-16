# BAP Build Pipeline

DevOps build pipeline tools for Unity projects.

## Features

- **Extensible Architecture**: Override specific build steps by inheriting from `BuildPipelineBase`.
- **Automatic Discovery**: Automatically detects project-specific build pipeline implementations via reflection.
- **Environment Driven**: Configured via environment variables for easy CI/CD integration.
- **Unity Cloud Build Support**: Includes hooks for Unity Cloud Build pre-export steps.

## Usage

### Command Line Build

Execute Unity with the following method as the entry point:

```bash
Unity -projectPath . -executeMethod BAP.Build.Build.BUILD_WIN -quit -batchmode
```

Supported methods:
- `BAP.Build.Build.BUILD_WIN`
- `BAP.Build.Build.BUILD_OSX`
- `BAP.Build.Build.BUILD_LINUX`
- `BAP.Build.Build.BUILD_ANDROID`
- `BAP.Build.Build.BUILD_IOS`

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OUTPUT_PATH` | Full path where the build artifact will be saved. | **Required** |
| `BUILD_NUMBER` | The current build version number. | **Required** |
| `COMMIT_HASH` | The git commit hash (used for versioning). | **Required** |
| `SCRIPTING_BACKEND` | `MONO` or `IL2CPP`. | `MONO` |
| `TARGET_ENV` | If set to `PRODUCTION`, adds `ENV_PRODUCTION` to scripting defines. | N/A |

## Extending the Pipeline

To customize the build process, create a class that inherits from `BuildPipelineBase` anywhere in your project:

```csharp
using BAP.Build;
using UnityEditor;

public class MyCustomPipeline : BuildPipelineBase
{
    protected override void ApplyScriptingDefines(NamedBuildTarget namedTarget, BuildTargetGroup targetGroup)
    {
        base.ApplyScriptingDefines(namedTarget, targetGroup);
        // Add your custom defines here
    }

    protected override BuildPlayerOptions GetBuildPlayerOptions(BuildTarget target, string[] scenes, string outputPath)
    {
        var options = base.GetBuildPlayerOptions(target, scenes, outputPath);
        options.options |= BuildOptions.Development;
        return options;
    }
}
```

The static `Build` class will automatically find and use your custom implementation.
