# 06.02-warning-remediation: Remediate build warnings

## Objective
Remediate build warnings remaining after the .NET 10 upgrade where safe and practical.

## Scope
- Capture current warning categories from a full build.
- Fix high-DPI manifest warning.
- Remove obsolete API warnings where safe.
- Fix nullable warnings where safe and localised.
- Document any warnings that must remain.

## Done When
The solution builds with warnings remediated or with only explicitly documented retained warnings.
