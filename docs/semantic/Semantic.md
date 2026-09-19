# Source semantics and fingerprints

Per-file records are stored under this directory, mirroring project-relative source paths. The File field is relative to the repository root, not the metadata file. No metadata is placed beside source code.

LastWriteTimeUtc is the observed filesystem modification time; RecordedAtUtc is the metadata observation time. TimeThenHash permits a quick timestamp comparison with SHA256 fallback. A timestamp match is not byte-integrity proof; release checks should use Hash mode. Git checkout may change timestamps or line endings; an actual byte change must not be hidden by rewriting metadata without review.

Run `scripts/Verify-Semantic.ps1 -Mode Hash`. Session recorder validation is documented in `docs/validation/2026-09-19-e3d.md`: E3D 2.1 live recording passed, with separate recorded defects and NET35 live-coverage limits. These records describe source, not certification of all product functionality.
