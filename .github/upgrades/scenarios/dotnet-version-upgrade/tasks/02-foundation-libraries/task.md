# 02-foundation-libraries: Foundation Libraries

Upgrade the dependency chain made up of `pylorak.Utilities`, `pylorak.Windows`, and `pylorak.Windows.Services`. This group starts with `pylorak.Utilities`, then moves to `pylorak.Windows`, and finishes with `pylorak.Windows.Services` so that each dependent project builds against an already-upgraded foundation.

Key concerns include removing framework-included packages where appropriate, preserving Windows Forms and GDI+ behaviour in `pylorak.Windows`, and replacing or removing unsupported Code Access Security patterns.

**Done when**: The three foundation projects target their .NET 10 frameworks, required package changes are applied, source compatibility issues are resolved, and the group builds successfully in dependency order.
