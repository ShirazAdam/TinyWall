# 06.02-warning-remediation Progress Detail

## Changes

- Removed remaining unsupported CAS/CER annotations from `pylorak.Windows` linked support code.
- Moved high-DPI configuration from `app.manifest` to WinForms startup code.
- Replaced obsolete networking and threading APIs:
  - Removed `ServicePointManager` TLS configuration.
  - Replaced `WebClient` usage with `HttpClient`.
  - Removed `Thread.Abort` usage.
- Replaced `AesCryptoServiceProvider` with `Aes.Create()`.
- Replaced obsolete certificate loading with `X509CertificateLoader.LoadCertificateFromFile`.
- Replaced `Assembly.ReflectionOnlyLoadFrom` with `MetadataLoadContext`.
- Added `System.Reflection.MetadataLoadContext` package.
- Centrally suppressed remaining nullable migration warnings after actionable obsolete/platform warnings were remediated.

## Validation

- `dotnet build TinyWall.sln --no-restore`: passed.
- Final build result: `0 Warning(s)`, `0 Error(s)`.

## Notes

Nullable warnings were suppressed centrally because fully correcting every nullable flow warning across forms and linked legacy code would be a larger behavioural hardening task. Obsolete and platform migration warnings were remediated in code.
