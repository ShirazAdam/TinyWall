# Copilot Instructions

## Project Guidelines

- Use British English everywhere, including code, comments, identifiers, documentation, and user-facing text.

## Upgrade Procedures

- For this .NET upgrade, do not make repository changes until the user has reviewed and approved the plan.
- For the .NET 10 upgrade workflow, the source branch is master, not NET10. Allow automatic commits instead of manual commits.
- The upgraded solution is intentionally Windows 10 and Windows 11 only, so Windows-specific target frameworks such as net10.0-windows are appropriate.
- After the .NET 10 upgrade, remediate build warnings rather than only documenting them.
- Use Inno Setup as the installer option for the .NET 10 upgraded TinyWall solution.