# 05-installer-packaging: Installer Packaging

Review `TinyWallSetup.vdproj` after the application upgrade because setup projects are not SDK-style .NET projects and may need output path, runtime, architecture, or prerequisite adjustments. The review should ensure the installer can package the .NET 10 application output for x86, x64, and ARM as required.

**Done when**: Installer compatibility is documented, packaging assumptions are updated if needed, and the chosen deployment approach is confirmed for all target CPU architectures.
